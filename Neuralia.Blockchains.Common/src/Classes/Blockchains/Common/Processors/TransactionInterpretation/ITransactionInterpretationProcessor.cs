using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation {

	public interface ITransactionInterpretationProcessor {

		void EnableLocalMode(bool value);
		void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts, ImmutableList<AccountId> dispatchedAccounts);
		void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts);
		void ClearLocalAccounts();
		void Reset();
		void Initialize();

		(List<ITransaction> impactingLocals, List<(ITransaction transaction, AccountId targetAccount)> impactingExternals, Dictionary<AccountId, List<TransactionId>> accountsTransactions) GetImpactingTransactionsList(List<ITransaction> transactions);
	}

	public interface ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ITransactionInterpretationProcessor
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public interface ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
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

		TransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> TransactionImpactSets { get; }
		
		Func<List<AccountId>, bool> IsAnyAccountTracked { get; set; }
		Func<List<AccountId>, List<AccountId>> GetTrackedAccounts { get; set; }

		Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, bool> IsAnyAccountKeysTracked { get; set; }
		Func<List<int>, bool> IsAnyAccreditationCertificateTracked { get; set; }
		Func<List<int>, bool> IsAnyChainOptionTracked { get; set; }

		Action<bool, List<AccountId>, List<AccountId>, ITransaction> AccountInfluencingTransactionFound { get; set; }

		void InterpretTransactions(List<ITransaction> transactions, long blockId, Action<int> step = null);
		void InterpretTransactionStream(List<ITransaction> transactions, long blockId, Action<int> step = null);
		void ApplyBlockElectionsInfluence(List<IFinalElectionResults> publicationResult, Dictionary<TransactionId, ITransaction> transactions);
		void ApplyBlockElectionsInfluence(List<SynthesizedBlock.SynthesizedElectionResult> finalElectionResults, Dictionary<TransactionId, ITransaction> transactions);

		event Action<TransactionId, RejectionCode> TransactionRejected;

		event Func<List<AccountId>, Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>> RequestStandardAccountSnapshots;
		event Func<List<AccountId>, Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>> RequestJointAccountSnapshots;
		event Func<List<(long AccountId, byte OrdinalId)>, Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestStandardAccountKeySnapshots;
		event Func<List<int>, Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestAccreditationCertificateSnapshots;
		event Func<List<int>, Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>> RequestChainOptionSnapshots;

		event Func<STANDARD_ACCOUNT_SNAPSHOT> RequestCreateNewStandardAccountSnapshot;
		event Func<JOINT_ACCOUNT_SNAPSHOT> RequestCreateNewJointAccountSnapshot;
		event Func<STANDARD_ACCOUNT_KEY_SNAPSHOT> RequestCreateNewAccountKeySnapshot;
		event Func<ACCREDITATION_CERTIFICATE_SNAPSHOT> RequestCreateNewAccreditationCertificateSnapshot;
		event Func<CHAIN_OPTIONS_SNAPSHOT> RequestCreateNewChainOptionSnapshot;

		SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack();
		Dictionary<(AccountId accountId, byte ordinal), byte[]> GetImpactedFastKeys();
	}
}