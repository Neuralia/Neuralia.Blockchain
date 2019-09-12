using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public abstract partial class TransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_FEATURE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_FEATURE_SNAPSHOT : class, IAccountFeature, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {

		/// <summary>
		///     Register all the base transaction types and their behaviors
		/// </summary>
		protected virtual void RegisterTransactionImpactSets() {

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IStandardPresentationTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.standardAccounts.Add(t.TransactionId.Account);

				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					STANDARD_ACCOUNT_SNAPSHOT snapshot = snapshotCache.CreateNewStandardAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account);

					STANDARD_ACCOUNT_SNAPSHOT newSnapshot = snapshot;

					newSnapshot.AccountId = t.AssignedAccountId.ToLongRepresentation();

					newSnapshot.InceptionBlockId = blockId;
					newSnapshot.CorrelationId = t.CorrelationId;

					foreach(ITransactionAccountFeature entry in t.Features) {

						STANDARD_ACCOUNT_FEATURE_SNAPSHOT newEntry = new STANDARD_ACCOUNT_FEATURE_SNAPSHOT();

						newEntry.FeatureType = entry.FeatureType;
						newEntry.CertificateId = entry.CertificateId;
						newEntry.Data = entry.Data;

						newSnapshot.AppliedFeatures.Add(newEntry);
					}
				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {
					
					if(mode == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.AssignedAccountId)) {
						// we dont need to set the keys in sumlated mode.
						STANDARD_ACCOUNT_KEY_SNAPSHOT key = snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.TransactionCryptographicKey.Id));

						string transactionId = t.TransactionId.ToExtendedString();
						key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
						key.DeclarationTransactionId = transactionId;

						key = snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.MessageCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
						key.DeclarationTransactionId = transactionId;

						key = snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
						key.DeclarationTransactionId = transactionId;
						
						key = snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.SuperCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
						key.DeclarationTransactionId = transactionId;
					}
				},
				CollectStandardAccountFastKeysFunc = (t, blockId, types) => {
					List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

					if(types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
						keys.Add(new FastKeyMetadata() {AccountId = t.AssignedAccountId, Ordinal = t.TransactionCryptographicKey.Id, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
					}

					if(types.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
						keys.Add(new FastKeyMetadata() {AccountId = t.AssignedAccountId, Ordinal = t.MessageCryptographicKey.Id, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
					}

					return keys;
				}
			});
			
			this.RegisterTransactionImpactSet(new TransactionImpactSet<IJointPresentationTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.jointAccounts.Add(t.TransactionId.Account);

				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					JOINT_ACCOUNT_SNAPSHOT newSnapshot = snapshotCache.CreateNewJointAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account);

					newSnapshot.AccountId = t.AssignedAccountId.ToLongRepresentation();
					newSnapshot.InceptionBlockId = blockId;
					newSnapshot.CorrelationId = t.CorrelationId;

					foreach(ITransactionAccountFeature entry in t.Features) {

						JOINT_ACCOUNT_FEATURE_SNAPSHOT newEntry = new JOINT_ACCOUNT_FEATURE_SNAPSHOT();

						newEntry.FeatureType = entry.FeatureType;
						newEntry.CertificateId = entry.CertificateId;
						newEntry.Data = entry.Data;

						newSnapshot.AppliedFeatures.Add(newEntry);
					}

					foreach(ITransactionJointAccountMember entry in t.MemberAccounts) {

						JOINT_ACCOUNT_MEMBERS_SNAPSHOT newEntry = new JOINT_ACCOUNT_MEMBERS_SNAPSHOT();

						newEntry.AccountId = entry.AccountId;

						newSnapshot.MemberAccounts.Add(newEntry);
					}

				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IStandardAccountKeyChangeTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.accountKeys.Add((t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id));

				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

					if(mode == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.TransactionId.Account)) {
						(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id);

						string transactionId = t.TransactionId.ToExtendedString();
						
						STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = snapshotCache.GetAccountKeySnapshotModify(key);
	
						accountKeySnapshot.PublicKey = this.Dehydratekey(t.NewCryptographicKey);
						accountKeySnapshot.DeclarationTransactionId = transactionId;

						if(t.IsChangingChangeKey) {
							(long SequenceId, byte SUPER_KEY_ORDINAL_ID) superKey = (t.TransactionId.Account.ToLongRepresentation(), t.NextSuperCryptographicKey.Id);

							STANDARD_ACCOUNT_KEY_SNAPSHOT accountSuperKeySnapshot = snapshotCache.GetAccountKeySnapshotModify(superKey);
							accountSuperKeySnapshot.PublicKey = this.Dehydratekey(t.NextSuperCryptographicKey);
							accountSuperKeySnapshot.DeclarationTransactionId = transactionId;
						}
					}
				},
				CollectStandardAccountFastKeysFunc = (t, blockId, types) => {
					List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

					if((t.NewCryptographicKey.Id == GlobalsService.TRANSACTION_KEY_ORDINAL_ID && types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) || (t.NewCryptographicKey.Id == GlobalsService.MESSAGE_KEY_ORDINAL_ID && types.HasFlag(ChainConfigurations.FastKeyTypes.Messages))) {
						keys.Add(new FastKeyMetadata(){AccountId = t.TransactionId.Account, Ordinal = t.NewCryptographicKey.Id, PublicKey = this.Dehydratekey(t.NewCryptographicKey)});
					} 

					return keys;
				}
			});
			
			this.RegisterTransactionImpactSet(new TransactionImpactSet<ISetAccountCorrelationIdTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.AddAccountId(t.TransactionId.Account);
				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					ACCOUNT_SNAPSHOT accountSnapshot = snapshotCache.GetAccountSnapshotModify(t.TransactionId.Account);

					accountSnapshot.CorrelationId = t.CorrelationId;
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<ISetAccountRecoveryTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.AddAccountId(t.TransactionId.Account);
				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					ACCOUNT_SNAPSHOT accountSnapshot = snapshotCache.GetAccountSnapshotModify(t.TransactionId.Account);

					if(t.Operation == SetAccountRecoveryTransaction.OperationTypes.Create) {
						IAccountFeature feature = accountSnapshot.GetCollectionEntry(entry => entry.FeatureType == AccountFeatureTypes.Instance.RESETABLE_ACCOUNT);

						if(feature == null) {
							accountSnapshot.CreateNewCollectionEntry(out feature);

							feature.FeatureType = AccountFeatureTypes.Instance.RESETABLE_ACCOUNT.Value;

							accountSnapshot.AddCollectionEntry(feature);

						}

						feature.Data = t.AccountRecoveryHash.ToExactByteArrayCopy();
					} else if(t.Operation == SetAccountRecoveryTransaction.OperationTypes.Revoke) {
						accountSnapshot.RemoveCollectionEntry(entry => entry.FeatureType == AccountFeatureTypes.Instance.RESETABLE_ACCOUNT);
					}
				}
			});

			//----------------------- moderator transactions ----------------------------

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IAccountResetWarningTransaction>());

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IAccountResetTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.standardAccounts.Add(t.Account);

					affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Id));

				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					ACCOUNT_SNAPSHOT accountSnapshot = snapshotCache.GetAccountSnapshotModify(t.Account);

					IAccountFeature feature = accountSnapshot.GetCollectionEntry(entry => entry.FeatureType == AccountFeatureTypes.Instance.RESETABLE_ACCOUNT);

					if(feature != null) {
						feature.Data = t.NextRecoveryHash.ToExactByteArrayCopy();
					}
				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

					if(mode == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.Account)) {
						// we dont need to set the keys in sumlated mode.

						string transactionId = t.TransactionId.ToExtendedString();
						
						STANDARD_ACCOUNT_KEY_SNAPSHOT key = snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
						key.DeclarationTransactionId = transactionId;

						key = snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
						key.DeclarationTransactionId = transactionId;

						key = snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
						key.DeclarationTransactionId = transactionId;
						
						key = snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Id));
						key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
						key.DeclarationTransactionId = transactionId;
					}
				},
				CollectStandardAccountFastKeysFunc = (t, blockId, types) => {
					List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

					if(types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
						keys.Add(new FastKeyMetadata() {AccountId = t.Account, Ordinal = t.TransactionCryptographicKey.Id, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
					}

					if(types.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
						keys.Add(new FastKeyMetadata() {AccountId = t.Account, Ordinal = t.MessageCryptographicKey.Id, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
					}

					return keys;
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IChainAccreditationCertificateTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					if(t.CertificateOperation != ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
						affectedSnapshots.accreditationCertificates.Add((int) t.CertificateId.Value);
					}
				},
				InterpretTransactionAccreditationCertificatesFunc = (t, blockId, snapshotCache, mode) => {

					ACCREDITATION_CERTIFICATE_SNAPSHOT certificate = null;

					if(t.CertificateOperation == ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
						certificate = snapshotCache.CreateNewAccreditationCertificateSnapshot((int) t.CertificateId.Value);
					} else {
						certificate = snapshotCache.GetAccreditationCertificateSnapshotModify((int) t.CertificateId.Value);
					}

					certificate.CertificateId = (int) t.CertificateId.Value;

					if(t.CertificateOperation == ChainAccreditationCertificateTransaction.CertificateOperationTypes.Revoke) {
						certificate.CertificateState = Enums.CertificateStates.Revoked;
					} else {
						certificate.CertificateState = Enums.CertificateStates.Active;
					}

					certificate.CertificateType = t.CertificateType;
					certificate.CertificateVersion = t.CertificateVersion;

					certificate.EmissionDate = t.EmissionDate;
					certificate.ValidUntil = t.ValidUntil;

					certificate.AssignedAccount = t.AssignedAccount.ToLongRepresentation();
					certificate.Application = t.Application;
					certificate.Organisation = t.Organisation;
					certificate.Url = t.Url;

					certificate.CertificateAccountPermissionType = t.AccountPermissionType;
					certificate.PermittedAccountCount = t.PermittedAccounts.Count;

					foreach(AccountId entry in t.PermittedAccounts) {
						ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT newEntry = new ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT();

						newEntry.AccountId = entry.ToLongRepresentation();

						certificate.PermittedAccounts.Add(newEntry);
					}
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IChainOperatingRulesTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.chainOptions.Add(1);

				},
				InterpretTransactionChainOptionsFunc = (t, blockId, snapshotCache, mode) => {

					if(mode == TransactionImpactSet.OperationModes.Real) {

						CHAIN_OPTIONS_SNAPSHOT accountKeySnapshot = null;

						if(snapshotCache.CheckChainOptionsSnapshotExists(1)) {
							accountKeySnapshot = snapshotCache.GetChainOptionsSnapshotModify(1);
						} else {
							accountKeySnapshot = snapshotCache.CreateNewChainOptionsSnapshot(1);
						}

						accountKeySnapshot.MaximumVersionAllowed = t.MaximumVersionAllowed.ToString();
						accountKeySnapshot.MinimumWarningVersionAllowed = t.MinimumWarningVersionAllowed.ToString();
						accountKeySnapshot.MinimumVersionAllowed = t.MinimumVersionAllowed.ToString();
						accountKeySnapshot.MaxBlockInterval = t.MaxBlockInterval;
					}
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IGenesisModeratorAccountPresentationTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.CommunicationsCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksXmssMTCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksChangeCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksChangeCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BinaryCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.SuperChangeCryptographicKey.Id));
					affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.PtahCryptographicKey.Id));

				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

					if(mode == TransactionImpactSet.OperationModes.Real) {
						NtruCryptographicKey key = t.CommunicationsCryptographicKey;
						STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key.Id));
						IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
						key.Dehydrate(dehydrator);
						IByteArray bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						XmssmtCryptographicKey key3 = t.BlocksXmssMTCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key3.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						SecretPentaCryptographicKey key2 = t.BlocksChangeCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key2.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						key3 = t.DigestBlocksCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key3.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						key2 = t.DigestBlocksChangeCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key2.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						key3 = t.BinaryCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key3.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						key2 = t.SuperChangeCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key2.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						key2 = t.PtahCryptographicKey;
						accountKeySnapshot = snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));
						dehydrator = DataSerializationFactory.CreateDehydrator();
						key2.Dehydrate(dehydrator);
						bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();
					}
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IGenesisAccountPresentationTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IModeratorKeyChangeTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					affectedSnapshots.accountKeys.Add((t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id));

				},
				InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

					if(mode == TransactionImpactSet.OperationModes.Real) {
						(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id);

						STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = snapshotCache.GetAccountKeySnapshotModify(key);

						IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
						t.NewCryptographicKey.Dehydrate(dehydrator);
						IByteArray bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
						bytes.Return();
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToExtendedString();

						//TODO: what else?
					}
				}
			});

			this.RegisterTransactionImpactSet(new TransactionImpactSet<IReclaimAccountsTransaction> {
				GetImpactedSnapshotsFunc = (t, affectedSnapshots) => {

					foreach(ReclaimAccountsTransaction.AccountReset accountset in t.Accounts) {
						affectedSnapshots.AddAccountId(accountset.Account);
					}
				},
				InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

					foreach(ReclaimAccountsTransaction.AccountReset accountset in t.Accounts) {
						ACCOUNT_SNAPSHOT accountSnapshot = snapshotCache.GetAccountSnapshotModify(accountset.Account);

						//TODO: what to do here?
					}
				}
			});

		}
	}
}