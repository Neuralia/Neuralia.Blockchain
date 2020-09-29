using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public abstract partial class TransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {

		protected abstract CardsUtils CardsUtils { get; }

		protected DateTime TrxDt(ITransaction t) {
			return this.centralCoordinator.BlockchainServiceSet.TimeService.GetTimestampDateTime(t.TransactionId.Timestamp.Value, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		protected virtual void FillStandardAccountSnapshot(STANDARD_ACCOUNT_SNAPSHOT newSnapshot, IStandardPresentationTransaction presentationTransaction) {
	
			newSnapshot.AccountId = presentationTransaction.AssignedAccountId.ToLongRepresentation();
			newSnapshot.Correlated = presentationTransaction.CorrelationId.HasValue;

			foreach(ITransactionAccountAttribute entry in presentationTransaction.Attributes) {

				STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT newEntry = new STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT();

				this.CardsUtils.Copy(entry, newEntry);

				newSnapshot.AppliedAttributes.Add(newEntry);
			}
		}
		
		protected virtual void FillJointAccountSnapshot(JOINT_ACCOUNT_SNAPSHOT newSnapshot, IJointPresentationTransaction presentationTransaction) {
	
			newSnapshot.AccountId = presentationTransaction.AssignedAccountId.ToLongRepresentation();
			newSnapshot.Correlated = presentationTransaction.CorrelationId.HasValue;

			foreach(ITransactionAccountAttribute entry in presentationTransaction.Attributes) {

				JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT newEntry = new JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT();

				this.CardsUtils.Copy(entry, newEntry);

				newSnapshot.AppliedAttributes.Add(newEntry);
			}

			foreach(ITransactionJointAccountMember entry in presentationTransaction.MemberAccounts) {

				JOINT_ACCOUNT_MEMBERS_SNAPSHOT newEntry = new JOINT_ACCOUNT_MEMBERS_SNAPSHOT();

				this.CardsUtils.Copy(entry, newEntry);

				newSnapshot.MemberAccounts.Add(newEntry);
			}
		}

		/// <summary>
		///     Register all the base transaction types and their behaviors
		/// </summary>
		protected virtual async Task RegisterTransactionImpactSets() {

			this.TransactionImpactSets.RegisterTransactionImpactSet<IStandardPresentationTransaction>(async (t, affectedSnapshots, lockContext) => {
				affectedSnapshots.standardAccounts.Add(t.AssignedAccountId);

				foreach(byte ordinal in t.Keyset.Keys.Keys) {
					affectedSnapshots.accountKeys.Add((t.AssignedAccountId.ToLongRepresentation(), ordinal));
				}
			}, async (t, parameters, lockContext) => {

				STANDARD_ACCOUNT_SNAPSHOT newSnapshot = await parameters.snapshotCache.CreateNewStandardAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account, lockContext).ConfigureAwait(false);

				this.FillStandardAccountSnapshot(newSnapshot, t);
				
				newSnapshot.InceptionBlockId = parameters.blockId;
			}, async (t, parameters, lockContext) => {

				if((parameters.operationModes == TransactionImpactSet.OperationModes.Real) && await this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.AssignedAccountId).ConfigureAwait(false)) {
					// we dont need to set the keys in sumlated mode.
					STANDARD_ACCOUNT_KEY_SNAPSHOT key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.TransactionCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);

					string transactionId = t.TransactionId.ToString();
					key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.MessageCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.ChangeCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.SuperCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					if(t.AccountType == Enums.AccountTypes.Server) {
						
					}
				}
			}, async (t, parameters, lockContext) => {
				List<KeyDictionaryMetadata> keys = new List<KeyDictionaryMetadata>();

				if(parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) {
					keys.Add(new KeyDictionaryMetadata {AccountId = t.AssignedAccountId, Ordinal = t.TransactionCryptographicKey.Ordinal, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
				}

				if(parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages)) {
					keys.Add(new KeyDictionaryMetadata {AccountId = t.AssignedAccountId, Ordinal = t.MessageCryptographicKey.Ordinal, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
				}

				parameters.results = keys;
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IJointPresentationTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.jointAccounts.Add(t.AssignedAccountId);
			}, async (t, parameters, lockContext) => {

				JOINT_ACCOUNT_SNAPSHOT newSnapshot = await parameters.snapshotCache.CreateNewJointAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account, lockContext).ConfigureAwait(false);

				this.FillJointAccountSnapshot(newSnapshot, t);
				newSnapshot.InceptionBlockId = parameters.blockId;
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IStandardAccountKeyChangeTransaction>(async (t, affectedSnapshots, lockContext) => {

				AccountId accountId = t.TransactionId.Account;
				affectedSnapshots.accountKeys.Add((accountId.ToLongRepresentation(), t.NewCryptographicKey.Ordinal));
				affectedSnapshots.standardAccounts.Add(accountId);

			}, interpretTransactionStandardAccountKeysFunc: async (t, parameters, lockContext) => {

				if((parameters.operationModes == TransactionImpactSet.OperationModes.Real) && await this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.TransactionId.Account).ConfigureAwait(false)) {
					(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Ordinal);

					string transactionId = t.TransactionId.ToString();

					STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = await parameters.snapshotCache.GetAccountKeySnapshotModify(key, lockContext).ConfigureAwait(false);

					accountKeySnapshot.PublicKey = this.Dehydratekey(t.NewCryptographicKey);
					accountKeySnapshot.DeclarationTransactionId = transactionId;
				}
			}, collectStandardAccountKeyDictionaryFunc: async (t, parameters, lockContext) => {
				List<KeyDictionaryMetadata> keys = new List<KeyDictionaryMetadata>();

				if(((t.NewCryptographicKey.Ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) || ((t.NewCryptographicKey.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages))) {
					keys.Add(new KeyDictionaryMetadata {AccountId = t.TransactionId.Account, Ordinal = t.NewCryptographicKey.Ordinal, PublicKey = this.Dehydratekey(t.NewCryptographicKey)});
				}

				parameters.results = keys;
			});

			// this.TransactionImpactSets.RegisterTransactionImpactSet<ISetAccountRecoveryTransaction>(getImpactedSnapshotsFunc: async (t, affectedSnapshots, lockContext) => {
			//
			// 	affectedSnapshots.AddAccountId(t.TransactionId.Account);
			// }, interpretTransactionAccountsFunc: async (t, parameters, lockContext) => {
			//
			// 	ACCOUNT_SNAPSHOT accountSnapshot = parameters.snapshotCache.GetAccountSnapshotModify(t.TransactionId.Account);
			//
			// 	if(t.Operation == SetAccountRecoveryTransaction.OperationTypes.Create) {
			// 		IAccountAttribute attribute = accountSnapshot.GetCollectionEntry(entry => entry.AttributeType == AccountAttributesTypes.Instance.RESETABLE_ACCOUNT);
			//
			// 		if(attribute == null) {
			// 			accountSnapshot.CreateNewCollectionEntry(out attribute);
			//
			// 			attribute.AttributeType = AccountAttributesTypes.Instance.RESETABLE_ACCOUNT.Value;
			//
			// 			accountSnapshot.AddCollectionEntry(attribute);
			//
			// 		}
			//
			// 		attribute.Context = t.AccountRecoveryHash.ToExactByteArrayCopy();
			// 	} else if(t.Operation == SetAccountRecoveryTransaction.OperationTypes.Revoke) {
			// 		accountSnapshot.RemoveCollectionEntry(entry => entry.AttributeType == AccountAttributesTypes.Instance.RESETABLE_ACCOUNT);
			// 	}
			// });
			//
			this.TransactionImpactSets.RegisterTransactionImpactSet<IGatedJudgementTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.AddAccounts(t.ImpactedAccounts);
			}, async (t, parameters, lockContext) => {

				//lockContext: anything here?
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IThreeWayGatedTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.AddAccounts(t.ImpactedAccounts);
			}, async (t, parameters, lockContext) => {

				//lockContext: anything here?
			});

			//----------------------- moderator transactions ----------------------------

			this.TransactionImpactSets.RegisterTransactionImpactSet<IAccountResetWarningTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.standardAccounts.Add(t.Account);

			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IAccountResetTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.standardAccounts.Add(t.Account);

				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Ordinal));

			}, async (t, parameters, lockContext) => {

				ACCOUNT_SNAPSHOT accountSnapshot = await parameters.snapshotCache.GetAccountSnapshotModify(t.Account, lockContext).ConfigureAwait(false);

				IAccountAttribute attribute = accountSnapshot.GetCollectionEntry(entry => entry.AttributeType == AccountAttributesTypes.Instance.RESETABLE_ACCOUNT);

				if(attribute != null) {
					attribute.Context = t.NextRecoveryHash.ToExactByteArrayCopy();
				}
			}, async (t, parameters, lockContext) => {

				if((parameters.operationModes == TransactionImpactSet.OperationModes.Real) && await this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.Account).ConfigureAwait(false)) {
					// we dont need to set the keys in sumlated mode.

					string transactionId = t.TransactionId.ToString();

					STANDARD_ACCOUNT_KEY_SNAPSHOT key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Ordinal), lockContext).ConfigureAwait(false);
					key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
					key.DeclarationTransactionId = transactionId;
				}
			}, async (t, parameters, lockContext) => {
				List<KeyDictionaryMetadata> keys = new List<KeyDictionaryMetadata>();

				if(parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) {
					keys.Add(new KeyDictionaryMetadata {AccountId = t.Account, Ordinal = t.TransactionCryptographicKey.Ordinal, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
				}

				if(parameters.types.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages)) {
					keys.Add(new KeyDictionaryMetadata {AccountId = t.Account, Ordinal = t.MessageCryptographicKey.Ordinal, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
				}

				parameters.results = keys;
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IChainAccreditationCertificateTransaction>(async (t, affectedSnapshots, lockContext) => {

				if(t.CertificateOperation != ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
					affectedSnapshots.accreditationCertificates.Add((int) t.CertificateId.Value);
				}
			}, interpretTransactionAccreditationCertificatesFunc: async (t, parameters, lockContext) => {

				ACCREDITATION_CERTIFICATE_SNAPSHOT certificate = null;

				if(t.CertificateOperation == ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
					certificate = await parameters.snapshotCache.CreateNewAccreditationCertificateSnapshot((int) t.CertificateId.Value, lockContext).ConfigureAwait(false);
				} else {
					certificate = await parameters.snapshotCache.GetAccreditationCertificateSnapshotModify((int) t.CertificateId.Value, lockContext).ConfigureAwait(false);
				}

				certificate.CertificateId = (int) t.CertificateId.Value;

				if(t.CertificateOperation == ChainAccreditationCertificateTransaction.CertificateOperationTypes.Revoke) {
					certificate.CertificateState = Enums.CertificateStates.Revoked;
				} else {
					certificate.CertificateState = Enums.CertificateStates.Active;
				}

				certificate.CertificateType = t.CertificateType.Value;
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
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IChainOperatingRulesTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.chainOptions.Add(1);

			}, interpretTransactionChainOptionsFunc: async (t, parameters, lockContext) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {

					CHAIN_OPTIONS_SNAPSHOT options = null;

					if(await parameters.snapshotCache.CheckChainOptionsSnapshotExists(1, lockContext).ConfigureAwait(false)) {
						options = await parameters.snapshotCache.GetChainOptionsSnapshotModify(1, lockContext).ConfigureAwait(false);
					} else {
						options = await parameters.snapshotCache.CreateNewChainOptionsSnapshot(1, lockContext).ConfigureAwait(false);
					}

					options.MaximumVersionAllowed = t.MaximumVersionAllowed.ToString();
					options.MinimumWarningVersionAllowed = t.MinimumWarningVersionAllowed.ToString();
					options.MinimumVersionAllowed = t.MinimumVersionAllowed.ToString();
					options.MaxBlockInterval = t.MaxBlockInterval;
					options.AllowGossipPresentations = t.AllowGossipPresentations;
				}
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IGenesisModeratorAccountPresentationTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.AddAccounts(t.ImpactedAccounts);

				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.CommunicationsCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.ValidatorSecretsCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksXmssCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksChangeCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksChangeCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.GossipCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BinaryCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.SuperChangeCryptographicKey.Ordinal));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.PtahCryptographicKey.Ordinal));

			}, interpretTransactionStandardAccountKeysFunc: async (t, parameters, lockContext) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {

					async Task SetAccountEntry(ICryptographicKey key) {
						STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = await parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key.Ordinal), lockContext).ConfigureAwait(false);
						accountKeySnapshot.PublicKey = this.Dehydratekey(key);
						accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();
					}

					await SetAccountEntry(t.CommunicationsCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.ValidatorSecretsCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.BlocksXmssCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.BlocksChangeCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.DigestBlocksCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.DigestBlocksChangeCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.BinaryCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.SuperChangeCryptographicKey).ConfigureAwait(false);
					await SetAccountEntry(t.PtahCryptographicKey).ConfigureAwait(false);
				}
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IGenesisAccountPresentationTransaction>(async (t, affectedSnapshots, lockContext) => {
				affectedSnapshots.AddAccounts(t.ImpactedAccounts);
			}, interpretTransactionStandardAccountKeysFunc: async (t, parameters, lockContext) => {

			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IModeratorKeyChangeTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.accountKeys.Add((t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Ordinal));

			}, interpretTransactionStandardAccountKeysFunc: async (t, parameters, lockContext) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {
					(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Ordinal);

					STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = await parameters.snapshotCache.GetAccountKeySnapshotModify(key, lockContext).ConfigureAwait(false);

					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
					t.NewCryptographicKey.Dehydrate(dehydrator);
					using SafeArrayHandle bytes = dehydrator.ToArray();
					accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					//lockContext: what else?
				}
			});

			// this.TransactionImpactSets.RegisterTransactionImpactSet<IReclaimAccountsTransaction>(async (t, affectedSnapshots, lockContext) => {
			//
			// 	affectedSnapshots.AddAccounts(t.Accounts.Select(e => e.Account).ToList());
			//
			// }, async (t, parameters, lockContext) => {
			// 	
			// 	foreach(ReclaimAccountsTransaction.AccountReset accountset in t.Accounts) {
			// 		//ACCOUNT_SNAPSHOT accountSnapshot = await parameters.snapshotCache.GetAccountSnapshotModify(accountset.Account);
			//
			// 		//lockContext: what to do here? perhaps we need to delete the accounts
			// 		
			// 	}
			// });

			this.TransactionImpactSets.RegisterTransactionImpactSet<IAssignAccountCorrelationsTransaction>(async (t, affectedSnapshots, lockContext) => {

				affectedSnapshots.AddAccounts(t.EnableAccounts);
				affectedSnapshots.AddAccounts(t.DisableAccounts);

			}, async (t, parameters, lockContext) => {

				foreach(AccountId accountId in t.EnableAccounts) {
					ACCOUNT_SNAPSHOT accountSnapshot = await parameters.snapshotCache.GetAccountSnapshotModify(accountId, lockContext).ConfigureAwait(false);

					if(accountSnapshot != null) {
						accountSnapshot.Correlated = true;
					}
				}

				foreach(AccountId accountId in t.DisableAccounts) {
					ACCOUNT_SNAPSHOT accountSnapshot = await parameters.snapshotCache.GetAccountSnapshotModify(accountId, lockContext).ConfigureAwait(false);

					if(accountSnapshot != null) {
						accountSnapshot.Correlated = false;
					}
				}
			});
		}
	}
}