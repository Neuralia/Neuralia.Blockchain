using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation {

	public interface ITransactionInterpretationProcessor {

		void EnableLocalMode(bool value);
		void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts, ImmutableList<AccountId> dispatchedAccounts);
		void SetLocalAccounts(ImmutableList<AccountId> publishedAccounts);
		void ClearLocalAccounts();
		void Reset();
		Task Initialize();

		Task<(List<ITransaction> impactingLocals, List<(ITransaction transaction, AccountId targetAccount)> impactingExternals, Dictionary<AccountId, List<TransactionId>> accountsTransactions)> GetImpactingTransactionsList(List<ITransaction> transactions, LockContext lockContext);
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

		Func<List<AccountId>, Task<bool>> IsAnyAccountTracked { get; set; }
		Func<List<AccountId>, Task<List<AccountId>>> GetTrackedAccounts { get; set; }

		Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, Task<bool>> IsAnyAccountKeysTracked { get; set; }
		Func<List<int>, Task<bool>> IsAnyAccreditationCertificateTracked { get; set; }
		Func<List<int>, Task<bool>> IsAnyChainOptionTracked { get; set; }

		Func<bool, List<AccountId>, List<AccountId>, ITransaction, BlockId, LockContext, Task> AccountInfluencingTransactionFound { get; set; }

		Task InterpretTransactions(List<ITransaction> transactions, long blockId, LockContext lockContext, Action<int> step = null);
		Task InterpretTransactionStream(List<ITransaction> transactions, long blockId, LockContext lockContext, Action<int> step = null);
		Task ApplyBlockElectionsInfluence(List<IFinalElectionResults> publicationResult, Dictionary<TransactionId, ITransaction> transactions, LockContext lockContext);
		Task ApplyBlockElectionsInfluence(List<SynthesizedBlock.SynthesizedElectionResult> finalElectionResults, Dictionary<TransactionId, ITransaction> transactions, LockContext lockContext);

		event Action<TransactionId, RejectionCode> TransactionRejected;

		event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, STANDARD_ACCOUNT_SNAPSHOT>>> RequestStandardAccountSnapshots;
		event Func<List<AccountId>, LockContext, Task<Dictionary<AccountId, JOINT_ACCOUNT_SNAPSHOT>>> RequestJointAccountSnapshots;
		event Func<List<(long AccountId, byte OrdinalId)>, LockContext, Task<Dictionary<(long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT>>> RequestStandardAccountKeySnapshots;
		event Func<List<int>, LockContext, Task<Dictionary<int, ACCREDITATION_CERTIFICATE_SNAPSHOT>>> RequestAccreditationCertificateSnapshots;
		event Func<List<int>, LockContext, Task<Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>>> RequestChainOptionSnapshots;

		event Func<LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> RequestCreateNewStandardAccountSnapshot;
		event Func<LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> RequestCreateNewJointAccountSnapshot;
		event Func<LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> RequestCreateNewAccountKeySnapshot;
		event Func<LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> RequestCreateNewAccreditationCertificateSnapshot;
		event Func<LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> RequestCreateNewChainOptionSnapshot;

		SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> GetEntriesModificationStack();
		Dictionary<(AccountId accountId, byte ordinal), byte[]> GetImpactedFastKeys();
	}
}