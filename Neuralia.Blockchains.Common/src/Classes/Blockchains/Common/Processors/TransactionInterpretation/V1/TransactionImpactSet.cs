using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public static class TransactionImpactSet {
		public enum OperationModes {
			Real,
			Simulated
		}
	}

	public class TransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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

		private readonly BlockchainType blockchainType;
		private readonly string chainName;

		public TransactionImpactSet(BlockchainType blockchainType, string chainName) {
			this.blockchainType = blockchainType;
			this.chainName = chainName;
		}

		public Func<List<AccountId>, Task<bool>> IsAnyAccountTracked { get; set; } = async ids => false;
		public Func<List<AccountId>, Task<List<AccountId>>> GetTrackedAccounts { get; set; } = async ids => new List<AccountId>();

		public Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, Task<bool>> IsAnyAccountKeysTracked { get; set; }
		public Func<List<int>, Task<bool>> IsAnyAccreditationCertificateTracked { get; set; }
		public Func<List<int>, Task<bool>> IsAnyChainOptionTracked { get; set; }

		//public OverrideSetAction<SnapshotKeySet> GeneralOverrideSetAction { get; } = new OverrideSetAction<SnapshotKeySet>();
		public OverrideSetAction<SnapshotKeySet> GetImpactedSnapshotsFuncOverrideSetAction { get; } = new OverrideSetAction<SnapshotKeySet>();

		public OverrideSetAction<InterpretTransactionAccountsFuncParameter> InterpretTransactionAccountsFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionAccountsFuncParameter>();
		public OverrideSetAction<InterpretTransactionStandardAccountKeysFuncParameter> InterpretTransactionStandardAccountKeysFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionStandardAccountKeysFuncParameter>();
		public OverrideSetAction<CollectStandardAccountKeyDictionaryFuncParameter> CollectStandardAccountKeyDictionaryFuncOverrideSetAction { get; } = new OverrideSetAction<CollectStandardAccountKeyDictionaryFuncParameter>();
		public OverrideSetAction<InterpretTransactionAccreditationCertificatesFuncParameter> InterpretTransactionAccreditationCertificatesFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionAccreditationCertificatesFuncParameter>();
		public OverrideSetAction<InterpretTransactionChainOptionsFuncParameter> InterpretTransactionChainOptionsFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionChainOptionsFuncParameter>();

		public OverrideSetFunc<InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)> InterpretTransactionVerificationFuncOverrideSetAction { get; } = new OverrideSetFunc<InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)>();

		public async Task InterpretTransaction(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> keyDictionary, ChainConfigurations.KeyDictionaryTypes enabledKeyDictionaryTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected, LockContext lockContext) {
			// if we must run a simulation, we do so
			bool accept = true;
			RejectionCode code = RejectionCodes.Instance.NONE;

			if(this.InterpretTransactionVerificationFuncOverrideSetAction.Any(transaction)) {

				// ok, for the simulation, we build a cache set proxy

				// run the simulation
				bool exception = false;

				try {
					snapshotCache.BeginTransaction();

					await GetImpactedSnapshots(transaction, lockContext).ConfigureAwait(false);
					bool? results = await this.RunInterpretationFunctions(transaction, blockId, impactedSnapshots, null, enabledKeyDictionaryTypes, TransactionImpactSet.OperationModes.Simulated, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected, lockContext).ConfigureAwait(false);

					if(results.HasValue && (results.Value == false)) {
						//throw new UnrecognizedElementException(this.blockchainType, this.chainName);
					}

					InterpretTransactionVerificationFuncParameter parameter = new InterpretTransactionVerificationFuncParameter();
					parameter.isException = exception;
					parameter.entryCache = snapshotCache;
					bool hasRun = false;

					((accept, code), hasRun) = await this.InterpretTransactionVerificationFuncOverrideSetAction.Run(transaction, parameter, lockContext, (InterpretTransactionVerificationFuncParameter a, (bool valid, RejectionCode rejectionCode) last, ref (bool valid, RejectionCode rejectionCode) final) => {

						final = last;

						//  continue only if it went well
						return final.Item1;
					}).ConfigureAwait(false);

				} catch {
					//TODO: do anything here?
					exception = true;
					code = RejectionCodes.Instance.OTHER;
					accept = false;
				} finally {
					snapshotCache.RollbackTransaction();
				}

			}

			if(accept) {
				try {
					snapshotCache.BeginTransaction();

					bool? results = await this.RunInterpretationFunctions(transaction, blockId, impactedSnapshots, keyDictionary, enabledKeyDictionaryTypes, operationMode, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected, lockContext).ConfigureAwait(false);

					// ok, lets run the real thing
					if(results.HasValue && (results.Value == false)) {
						//throw new UnrecognizedElementException(this.blockchainType, this.chainName);
					}

					// if we operate in local mode, then lets do it here
					snapshotCache.CommitTransaction();
				} catch {
					//TODO: do anything here?
					snapshotCache.RollbackTransaction();
				}
			} else {
				if(TransactionRejected != null) {
					TransactionRejected(transaction.TransactionId, code);
				}
			}
		}

		private async Task<bool?> RunInterpretationFunctions(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> keyDictionary, ChainConfigurations.KeyDictionaryTypes enabledKeyDictionaryTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected, LockContext lockContext) {

			bool? hasRunAny = null;
			List<AccountId> accounts = impactedSnapshots.standardAccounts.ToList();
			accounts.AddRange(impactedSnapshots.jointAccounts);

			if(isLocalAccount && isDispatchedAccount && transaction is IPresentationTransaction presentationTransaction) {
				// presentation transactions are special, we add the hash directly instead of the public id
				accounts.Add(presentationTransaction.TransactionId.Account);
			}

			if(accounts.Any() && await this.IsAnyAccountTracked(accounts).ConfigureAwait(false)) {
				InterpretTransactionAccountsFuncParameter parameter = new InterpretTransactionAccountsFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;

				bool hasRun = await this.InterpretTransactionAccountsFuncOverrideSetAction.Run(transaction, parameter, lockContext).ConfigureAwait(false);

				hasRunAny = hasRun;
			}

			if(impactedSnapshots.accountKeys.Any() && await this.IsAnyAccountKeysTracked(impactedSnapshots.accountKeys, impactedSnapshots.standardAccounts).ConfigureAwait(false)) {

				InterpretTransactionStandardAccountKeysFuncParameter parameter = new InterpretTransactionStandardAccountKeysFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;
				bool hasRun = await this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.Run(transaction, parameter, lockContext).ConfigureAwait(false);

				if(!hasRunAny.HasValue || (hasRunAny.Value != true)) {
					hasRunAny = hasRun;
				}
			}

			if((keyDictionary != null) && (operationMode == TransactionImpactSet.OperationModes.Real)) {

				CollectStandardAccountKeyDictionaryFuncParameter parameter = new CollectStandardAccountKeyDictionaryFuncParameter();
				parameter.blockId = blockId;
				parameter.types = enabledKeyDictionaryTypes;

				bool hasRun = await this.CollectStandardAccountKeyDictionaryFuncOverrideSetAction.Run(transaction, parameter, lockContext).ConfigureAwait(false);

				//TODO: should this one be considered in the run bool?
				// if(!hasRunAny.HasValue || hasRunAny.Value != true) {
				// 	hasRunAny = hasRun;
				// }
				List<KeyDictionaryMetadata> fastKeysdatas = parameter.results;

				// update the public keys, if any
				if(fastKeysdatas != null) {
					foreach(KeyDictionaryMetadata entry in fastKeysdatas) {
						(AccountId AccountId, byte Ordinal) key = (entry.AccountId, entry.Ordinal);

						if(keyDictionary.ContainsKey(key)) {
							keyDictionary[key] = entry.PublicKey;
						} else {
							keyDictionary.Add(key, entry.PublicKey);
						}
					}
				}
			}

			if(impactedSnapshots.accreditationCertificates.Any() && await this.IsAnyAccreditationCertificateTracked(impactedSnapshots.accreditationCertificates).ConfigureAwait(false)) {
				InterpretTransactionAccreditationCertificatesFuncParameter parameter = new InterpretTransactionAccreditationCertificatesFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;
				bool hasRun = await this.InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.Run(transaction, parameter, lockContext).ConfigureAwait(false);

				if(!hasRunAny.HasValue || (hasRunAny.Value != true)) {
					hasRunAny = hasRun;
				}
			}

			if(impactedSnapshots.chainOptions.Any() && await this.IsAnyChainOptionTracked(impactedSnapshots.chainOptions).ConfigureAwait(false)) {

				InterpretTransactionChainOptionsFuncParameter parameter = new InterpretTransactionChainOptionsFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;

				bool hasRun = await this.InterpretTransactionChainOptionsFuncOverrideSetAction.Run(transaction, parameter, lockContext).ConfigureAwait(false);

				if(!hasRunAny.HasValue || (hasRunAny.Value != true)) {
					hasRunAny = hasRun;
				}
			}

			return hasRunAny;
		}

		public async Task<SnapshotKeySet> GetImpactedSnapshots(ITransaction transaction, LockContext lockContext) {

			SnapshotKeySet result = new SnapshotKeySet();
			bool hasRun = await this.GetImpactedSnapshotsFuncOverrideSetAction.Run(transaction, result, lockContext).ConfigureAwait(false);

			if(!hasRun) {
				// if a transaction found no interpretation, then we most probably have an old version. lets stop here.
				throw new UnrecognizedElementException(this.blockchainType, this.chainName);
			}

			return result;
		}

		public void RegisterInterpretTransactionVerificationHandler<T>(Func<T, InterpretTransactionVerificationFuncParameter, LockContext, Task<(bool valid, RejectionCode rejectionCode)>> interpretTransactionVerificationFuncOverrideSetAction = null) {

			this.InterpretTransactionVerificationFuncOverrideSetAction.AddSet(interpretTransactionVerificationFuncOverrideSetAction);
		}

		public void RegisterInterpretTransactionVerificationHandlerOverride<C, P>(Func<C, InterpretTransactionVerificationFuncParameter, Func<P, InterpretTransactionVerificationFuncParameter, LockContext, Task<(bool valid, RejectionCode rejectionCode)>>, LockContext, Task<(bool valid, RejectionCode rejectionCode)>> interpretTransactionVerificationFuncOverrideSetAction = null)
			where C : P {

			this.InterpretTransactionVerificationFuncOverrideSetAction.AddOverrideSet(interpretTransactionVerificationFuncOverrideSetAction);
		}

		public void RegisterTransactionImpactSet<T>(Func<T, SnapshotKeySet, LockContext, Task> getImpactedSnapshotsFunc, Func<T, InterpretTransactionAccountsFuncParameter, LockContext, Task> interpretTransactionAccountsFunc = null, Func<T, InterpretTransactionStandardAccountKeysFuncParameter, LockContext, Task> interpretTransactionStandardAccountKeysFunc = null, Func<T, CollectStandardAccountKeyDictionaryFuncParameter, LockContext, Task> collectStandardAccountKeyDictionaryFunc = null, Func<T, InterpretTransactionChainOptionsFuncParameter, LockContext, Task> interpretTransactionChainOptionsFunc = null, Func<T, InterpretTransactionAccreditationCertificatesFuncParameter, LockContext, Task> interpretTransactionAccreditationCertificatesFunc = null) {

			if(getImpactedSnapshotsFunc == null) {
				throw new ArgumentNullException($"{nameof(getImpactedSnapshotsFunc)} cannot be null");
			}

			//TODO: all these override sets can be grouped into one, to save space and not duplicate the type hierarchy sets.
			// so, optimize and group into one...
			this.GetImpactedSnapshotsFuncOverrideSetAction.AddSet(getImpactedSnapshotsFunc);
			this.InterpretTransactionAccountsFuncOverrideSetAction.AddSet(interpretTransactionAccountsFunc);
			this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.AddSet(interpretTransactionStandardAccountKeysFunc);
			this.CollectStandardAccountKeyDictionaryFuncOverrideSetAction.AddSet(collectStandardAccountKeyDictionaryFunc);

			this.InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.AddSet(interpretTransactionAccreditationCertificatesFunc);
			this.InterpretTransactionChainOptionsFuncOverrideSetAction.AddSet(interpretTransactionChainOptionsFunc);
		}

		public void RegisterTransactionImpactSetOverride<C, P>(Func<C, SnapshotKeySet, Func<P, SnapshotKeySet, LockContext, Task>, LockContext, Task> getImpactedSnapshotsFunc = null, Func<C, InterpretTransactionAccountsFuncParameter, Func<P, InterpretTransactionAccountsFuncParameter, LockContext, Task>, LockContext, Task> interpretTransactionAccountsFunc = null, Func<C, InterpretTransactionStandardAccountKeysFuncParameter, Func<P, InterpretTransactionStandardAccountKeysFuncParameter, LockContext, Task>, LockContext, Task> interpretTransactionStandardAccountKeysFunc = null, Func<C, CollectStandardAccountKeyDictionaryFuncParameter, Func<P, CollectStandardAccountKeyDictionaryFuncParameter, LockContext, Task>, LockContext, Task> collectStandardAccountKeyDictionaryFunc = null, Func<C, InterpretTransactionChainOptionsFuncParameter, Func<P, InterpretTransactionChainOptionsFuncParameter, LockContext, Task>, LockContext, Task> interpretTransactionChainOptionsFunc = null, Func<C, InterpretTransactionAccreditationCertificatesFuncParameter, Func<P, InterpretTransactionAccreditationCertificatesFuncParameter, LockContext, Task>, LockContext, Task> interpretTransactionAccreditationCertificatesFunc = null)
			where C : P {

			this.GetImpactedSnapshotsFuncOverrideSetAction.AddSet(getImpactedSnapshotsFunc);
			this.InterpretTransactionAccountsFuncOverrideSetAction.AddSet(interpretTransactionAccountsFunc);
			this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.AddSet(interpretTransactionStandardAccountKeysFunc);
			this.CollectStandardAccountKeyDictionaryFuncOverrideSetAction.AddSet(collectStandardAccountKeyDictionaryFunc);

			this.InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.AddSet(interpretTransactionAccreditationCertificatesFunc);
			this.InterpretTransactionChainOptionsFuncOverrideSetAction.AddSet(interpretTransactionChainOptionsFunc);
		}

		public class InterpretTransactionAccountsFuncParameter {
			public long blockId { get; set; }
			public ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}

		public class InterpretTransactionStandardAccountKeysFuncParameter {
			public long blockId { get; set; }
			public IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}

		public class CollectStandardAccountKeyDictionaryFuncParameter {
			public long blockId { get; set; }
			public ChainConfigurations.KeyDictionaryTypes types { get; set; }
			public List<KeyDictionaryMetadata> results { get; set; }
		}

		public class InterpretTransactionChainOptionsFuncParameter {

			public long blockId { get; set; }
			public IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}

		public class InterpretTransactionVerificationFuncParameter {

			public bool isException { get; set; }
			public ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> entryCache { get; set; }
			public (bool result, RejectionCode code) results { get; set; }
		}

		public class InterpretTransactionAccreditationCertificatesFuncParameter {

			public long blockId { get; set; }
			public ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}
	}

	public class KeyDictionaryMetadata {
		public AccountId AccountId { get; set; }
		public byte Ordinal { get; set; }
		public byte[] PublicKey { get; set; }
	}
}