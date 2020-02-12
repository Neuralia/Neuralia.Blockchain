using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated;
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

		protected DateTime TrxDt(ITransaction t) {
			return this.centralCoordinator.BlockchainServiceSet.TimeService.GetTimestampDateTime(t.TransactionId.Timestamp.Value, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		protected abstract CardsUtils CardsUtils { get; }

		/// <summary>
		///     Register all the base transaction types and their behaviors
		/// </summary>
		protected virtual void RegisterTransactionImpactSets() {

			this.TransactionImpactSets.RegisterTransactionImpactSet<IStandardPresentationTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {
				affectedSnapshots.standardAccounts.Add(t.AssignedAccountId);

				foreach(var ordinal in t.Keyset.Keys.Keys) {
					affectedSnapshots.accountKeys.Add((t.AssignedAccountId.ToLongRepresentation(), ordinal));
				}
			}, interpretTransactionAccountsFunc: (t, parameters) => {

				STANDARD_ACCOUNT_SNAPSHOT snapshot = parameters.snapshotCache.CreateNewStandardAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account);

				STANDARD_ACCOUNT_SNAPSHOT newSnapshot = snapshot;

				newSnapshot.AccountId = t.AssignedAccountId.ToLongRepresentation();

				newSnapshot.InceptionBlockId = parameters.blockId;
				newSnapshot.CorrelationId = t.CorrelationId;

				foreach(ITransactionAccountAttribute entry in t.Attributes) {

					STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT newEntry = new STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT();

					this.CardsUtils.Copy(entry, newEntry);

					newSnapshot.AppliedAttributes.Add(newEntry);
				}
			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.AssignedAccountId)) {
					// we dont need to set the keys in sumlated mode.
					STANDARD_ACCOUNT_KEY_SNAPSHOT key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.TransactionCryptographicKey.Id));

					string transactionId = t.TransactionId.ToString();
					key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.MessageCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.AssignedAccountId.ToLongRepresentation(), t.SuperCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
					key.DeclarationTransactionId = transactionId;
				}
			}, collectStandardAccountFastKeysFunc: (t, parameters) => {
				List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

				if(parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
					keys.Add(new FastKeyMetadata() {AccountId = t.AssignedAccountId, Ordinal = t.TransactionCryptographicKey.Id, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
				}

				if(parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
					keys.Add(new FastKeyMetadata() {AccountId = t.AssignedAccountId, Ordinal = t.MessageCryptographicKey.Id, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
				}

				parameters.results = keys;
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IJointPresentationTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.jointAccounts.Add(t.AssignedAccountId);
			}, interpretTransactionAccountsFunc: (t, parameters) => {

				JOINT_ACCOUNT_SNAPSHOT newSnapshot = parameters.snapshotCache.CreateNewJointAccountSnapshot(t.AssignedAccountId, t.TransactionId.Account);

				newSnapshot.AccountId = t.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = parameters.blockId;
				newSnapshot.CorrelationId = t.CorrelationId;

				foreach(ITransactionAccountAttribute entry in t.Attributes) {

					JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT newEntry = new JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT();

					this.CardsUtils.Copy(entry, newEntry);

					newSnapshot.AppliedAttributes.Add(newEntry);
				}

				foreach(ITransactionJointAccountMember entry in t.MemberAccounts) {

					JOINT_ACCOUNT_MEMBERS_SNAPSHOT newEntry = new JOINT_ACCOUNT_MEMBERS_SNAPSHOT();

					this.CardsUtils.Copy(entry, newEntry);

					newSnapshot.MemberAccounts.Add(newEntry);
				}

			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IStandardAccountKeyChangeTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				AccountId accountId = t.TransactionId.Account;
				affectedSnapshots.accountKeys.Add((accountId.ToLongRepresentation(), t.NewCryptographicKey.Id));
				affectedSnapshots.standardAccounts.Add(accountId);

			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.TransactionId.Account)) {
					(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id);

					string transactionId = t.TransactionId.ToString();

					STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = parameters.snapshotCache.GetAccountKeySnapshotModify(key);

					accountKeySnapshot.PublicKey = this.Dehydratekey(t.NewCryptographicKey);
					accountKeySnapshot.DeclarationTransactionId = transactionId;

					if(t.IsChangingChangeKey) {
						(long SequenceId, byte SUPER_KEY_ORDINAL_ID) superKey = (t.TransactionId.Account.ToLongRepresentation(), t.NextSuperCryptographicKey.Id);

						STANDARD_ACCOUNT_KEY_SNAPSHOT accountSuperKeySnapshot = parameters.snapshotCache.GetAccountKeySnapshotModify(superKey);
						accountSuperKeySnapshot.PublicKey = this.Dehydratekey(t.NextSuperCryptographicKey);
						accountSuperKeySnapshot.DeclarationTransactionId = transactionId;
					}
				}
			}, collectStandardAccountFastKeysFunc: (t, parameters) => {
				List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

				if((t.NewCryptographicKey.Id == GlobalsService.TRANSACTION_KEY_ORDINAL_ID && parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) || (t.NewCryptographicKey.Id == GlobalsService.MESSAGE_KEY_ORDINAL_ID && parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Messages))) {
					keys.Add(new FastKeyMetadata() {AccountId = t.TransactionId.Account, Ordinal = t.NewCryptographicKey.Id, PublicKey = this.Dehydratekey(t.NewCryptographicKey)});
				}

				parameters.results = keys;
			});

			// this.TransactionImpactSets.RegisterTransactionImpactSet<ISetAccountCorrelationIdTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {
			//
			// 	affectedSnapshots.AddAccountId(t.TransactionId.Account);
			// }, interpretTransactionAccountsFunc: (t, parameters) => {
			//
			// 	ACCOUNT_SNAPSHOT accountSnapshot = parameters.snapshotCache.GetAccountSnapshotModify(t.TransactionId.Account);
			//
			// 	if(accountSnapshot != null && !accountSnapshot.CorrelationId.HasValue) {
			// 		accountSnapshot.CorrelationId = t.CorrelationId;
			// 	}
			// });

			// this.TransactionImpactSets.RegisterTransactionImpactSet<ISetAccountRecoveryTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {
			//
			// 	affectedSnapshots.AddAccountId(t.TransactionId.Account);
			// }, interpretTransactionAccountsFunc: (t, parameters) => {
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
			this.TransactionImpactSets.RegisterTransactionImpactSet<IGatedJudgementTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.AddAccounts(t.TargetAccounts);
			}, interpretTransactionAccountsFunc: (t, parameters) => {


				//TODO: anything here?
			});
			
			this.TransactionImpactSets.RegisterTransactionImpactSet<IThreeWayGatedTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.AddAccounts(t.TargetAccounts);
			}, interpretTransactionAccountsFunc: (t, parameters) => {

				//TODO: anything here?
			});

			//----------------------- moderator transactions ----------------------------

			this.TransactionImpactSets.RegisterTransactionImpactSet<IAccountResetWarningTransaction>();

			this.TransactionImpactSets.RegisterTransactionImpactSet<IAccountResetTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.standardAccounts.Add(t.Account);

				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Id));

			}, interpretTransactionAccountsFunc: (t, parameters) => {

				ACCOUNT_SNAPSHOT accountSnapshot = parameters.snapshotCache.GetAccountSnapshotModify(t.Account);

				IAccountAttribute attribute = accountSnapshot.GetCollectionEntry(entry => entry.AttributeType == AccountAttributesTypes.Instance.RESETABLE_ACCOUNT);

				if(attribute != null) {
					attribute.Context = t.NextRecoveryHash.ToExactByteArrayCopy();
				}
			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real && this.centralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase.IsAccountTracked(t.Account)) {
					// we dont need to set the keys in sumlated mode.

					string transactionId = t.TransactionId.ToString();

					STANDARD_ACCOUNT_KEY_SNAPSHOT key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.TransactionCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.TransactionCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.MessageCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.MessageCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.ChangeCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.ChangeCryptographicKey);
					key.DeclarationTransactionId = transactionId;

					key = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.Account.ToLongRepresentation(), t.SuperCryptographicKey.Id));
					key.PublicKey = this.Dehydratekey(t.SuperCryptographicKey);
					key.DeclarationTransactionId = transactionId;
				}
			}, collectStandardAccountFastKeysFunc: (t, parameters) => {
				List<FastKeyMetadata> keys = new List<FastKeyMetadata>();

				if(parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
					keys.Add(new FastKeyMetadata() {AccountId = t.Account, Ordinal = t.TransactionCryptographicKey.Id, PublicKey = this.Dehydratekey(t.TransactionCryptographicKey)});
				}

				if(parameters.types.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
					keys.Add(new FastKeyMetadata() {AccountId = t.Account, Ordinal = t.MessageCryptographicKey.Id, PublicKey = this.Dehydratekey(t.MessageCryptographicKey)});
				}

				parameters.results = keys;
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IChainAccreditationCertificateTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				if(t.CertificateOperation != ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
					affectedSnapshots.accreditationCertificates.Add((int) t.CertificateId.Value);
				}
			}, interpretTransactionAccreditationCertificatesFunc: (t, parameters) => {

				ACCREDITATION_CERTIFICATE_SNAPSHOT certificate = null;

				if(t.CertificateOperation == ChainAccreditationCertificateTransaction.CertificateOperationTypes.Create) {
					certificate = parameters.snapshotCache.CreateNewAccreditationCertificateSnapshot((int) t.CertificateId.Value);
				} else {
					certificate = parameters.snapshotCache.GetAccreditationCertificateSnapshotModify((int) t.CertificateId.Value);
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

			this.TransactionImpactSets.RegisterTransactionImpactSet<IChainOperatingRulesTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.chainOptions.Add(1);

			}, interpretTransactionChainOptionsFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {

					CHAIN_OPTIONS_SNAPSHOT options = null;

					if(parameters.snapshotCache.CheckChainOptionsSnapshotExists(1)) {
						options = parameters.snapshotCache.GetChainOptionsSnapshotModify(1);
					} else {
						options = parameters.snapshotCache.CreateNewChainOptionsSnapshot(1);
					}

					options.MaximumVersionAllowed = t.MaximumVersionAllowed.ToString();
					options.MinimumWarningVersionAllowed = t.MinimumWarningVersionAllowed.ToString();
					options.MinimumVersionAllowed = t.MinimumVersionAllowed.ToString();
					options.MaxBlockInterval = t.MaxBlockInterval;
					options.AllowGossipPresentations = t.AllowGossipPresentations;
				}
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IGenesisModeratorAccountPresentationTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.AddAccounts(t.TargetAccounts);
				
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.CommunicationsCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksXmssMTCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BlocksChangeCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.DigestBlocksChangeCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.BinaryCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.SuperChangeCryptographicKey.Id));
				affectedSnapshots.accountKeys.Add((t.ModeratorAccountId.ToLongRepresentation(), t.PtahCryptographicKey.Id));

			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {
					NtruCryptographicKey key = t.CommunicationsCryptographicKey;
					STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}
					
					
					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					XmssmtCryptographicKey key3 = t.BlocksXmssMTCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key3.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					
					
					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					SecretPentaCryptographicKey key2 = t.BlocksChangeCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key2.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					key3 = t.DigestBlocksCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key3.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					key2 = t.DigestBlocksChangeCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key2.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					key3 = t.BinaryCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key3.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key3.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					key2 = t.SuperChangeCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key2.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					key2 = t.PtahCryptographicKey;
					accountKeySnapshot = parameters.snapshotCache.CreateNewAccountKeySnapshot((t.ModeratorAccountId.ToLongRepresentation(), key2.Id));

					using(var dehydrator = DataSerializationFactory.CreateDehydrator()) {
						key2.Dehydrate(dehydrator);
						using SafeArrayHandle bytes = dehydrator.ToArray();
						accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					}

					

					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();
				}
			});

			this.TransactionImpactSets.RegisterTransactionImpactSet<IGenesisAccountPresentationTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {
				affectedSnapshots.AddAccounts(t.TargetAccounts);
			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

			});
			
			this.TransactionImpactSets.RegisterTransactionImpactSet<IModeratorKeyChangeTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {

				affectedSnapshots.accountKeys.Add((t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id));

			}, interpretTransactionStandardAccountKeysFunc: (t, parameters) => {

				if(parameters.operationModes == TransactionImpactSet.OperationModes.Real) {
					(long SequenceId, byte Id) key = (t.TransactionId.Account.ToLongRepresentation(), t.NewCryptographicKey.Id);

					STANDARD_ACCOUNT_KEY_SNAPSHOT accountKeySnapshot = parameters.snapshotCache.GetAccountKeySnapshotModify(key);

					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
					t.NewCryptographicKey.Dehydrate(dehydrator);
					using SafeArrayHandle bytes = dehydrator.ToArray();
					accountKeySnapshot.PublicKey = bytes.ToExactByteArrayCopy();
					accountKeySnapshot.DeclarationTransactionId = t.TransactionId.ToString();

					//TODO: what else?
				}
			});

			// this.TransactionImpactSets.RegisterTransactionImpactSet<IReclaimAccountsTransaction>(getImpactedSnapshotsFunc: (t, affectedSnapshots) => {
			//
			// 	foreach(ReclaimAccountsTransaction.AccountReset accountset in t.Accounts) {
			// 		affectedSnapshots.AddAccountId(accountset.Account);
			// 	}
			// }, interpretTransactionAccountsFunc: (t, parameters) => {
			//
			// 	foreach(ReclaimAccountsTransaction.AccountReset accountset in t.Accounts) {
			// 		ACCOUNT_SNAPSHOT accountSnapshot = parameters.snapshotCache.GetAccountSnapshotModify(accountset.Account);
			//
			// 		//TODO: what to do here?
			// 	}
			// });
			

		}
	}
}