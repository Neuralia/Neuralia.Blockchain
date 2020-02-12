using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;

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
		public void InterpretTransaction(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys, ChainConfigurations.FastKeyTypes enabledFastKeyTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected) {
			// if we must run a simulation, we do so
			bool accept = true;
			RejectionCode code = RejectionCodes.Instance.NONE;

			if(this.InterpretTransactionVerificationFuncOverrideSetAction.Any(transaction)) {

				// ok, for the simulation, we build a cache set proxy

				// run the simulation
				bool exception = false;

				try {
					snapshotCache.BeginTransaction();

					var results = this.RunInterpretationFunctions(transaction, blockId, impactedSnapshots, null, enabledFastKeyTypes, TransactionImpactSet.OperationModes.Simulated, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected);
					if(results.HasValue && results.Value == false){
						throw new UnrecognizedElementException(this.blockchainType, this.chainName);
					}

					var parameter = new InterpretTransactionVerificationFuncParameter();
					parameter.isException = exception;
					parameter.entryCache = snapshotCache;
					(accept, code) = InterpretTransactionVerificationFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun, (InterpretTransactionVerificationFuncParameter a, (bool valid, RejectionCode rejectionCode) last, ref (bool valid, RejectionCode rejectionCode) final) => {

						final = last;
						
						//  continue only if it went well
						return final.Item1 == true;
					});

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

					var results = this.RunInterpretationFunctions(transaction, blockId, impactedSnapshots, fastKeys, enabledFastKeyTypes, operationMode, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected);
					// ok, lets run the real thing
					if(results.HasValue && results.Value == false){
						throw new UnrecognizedElementException(this.blockchainType, this.chainName);
					}

					// if we operate in local mode, then lets do it here
					snapshotCache.CommitTransaction();
				} catch {
					//TODO: do anything here?
					snapshotCache.RollbackTransaction();
				}
			} else {
				TransactionRejected?.Invoke(transaction.TransactionId, code);
			}
		}
		
		private bool? RunInterpretationFunctions(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys, ChainConfigurations.FastKeyTypes enabledFastKeyTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected) {

			bool? hasRunAny = null;
			var accounts = impactedSnapshots.standardAccounts.ToList();
			accounts.AddRange(impactedSnapshots.jointAccounts);

			if(isLocalAccount && isDispatchedAccount && transaction is IPresentationTransaction presentationTransaction) {
				// presentation transactions are special, we add the hash directly instead of the public id
				accounts.Add(presentationTransaction.TransactionId.Account);
			}
			
			if(accounts.Any() && this.IsAnyAccountTracked(accounts)) {
				InterpretTransactionAccountsFuncParameter parameter = new InterpretTransactionAccountsFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;
				
				this.InterpretTransactionAccountsFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun);

				hasRunAny = hasRun;
			}

			if(impactedSnapshots.accountKeys.Any() && this.IsAnyAccountKeysTracked(impactedSnapshots.accountKeys, impactedSnapshots.standardAccounts)) {
				
				InterpretTransactionStandardAccountKeysFuncParameter parameter = new InterpretTransactionStandardAccountKeysFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;
				this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun);
				
				if(!hasRunAny.HasValue || hasRunAny.Value != true) {
					hasRunAny = hasRun;
				}
			}

			if((fastKeys != null) && (operationMode == TransactionImpactSet.OperationModes.Real)) {
				
				CollectStandardAccountFastKeysFuncParameter parameter = new CollectStandardAccountFastKeysFuncParameter();
				parameter.blockId = blockId;
				parameter.types = enabledFastKeyTypes;

				this.CollectStandardAccountFastKeysFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun);
				
				//TODO: should this one be considered in the run bool?
				// if(!hasRunAny.HasValue || hasRunAny.Value != true) {
				// 	hasRunAny = hasRun;
				// }
				var fastKeysdatas = parameter.results;

				// update the public keys, if any
				if(fastKeysdatas != null) {
					foreach(FastKeyMetadata entry in fastKeysdatas) {
						(AccountId AccountId, byte Ordinal) key = (entry.AccountId, entry.Ordinal);

						if(fastKeys.ContainsKey(key)) {
							fastKeys[key] = entry.PublicKey;
						} else {
							fastKeys.Add(key, entry.PublicKey);
						}
					}
				}
			}

			if(impactedSnapshots.accreditationCertificates.Any() && this.IsAnyAccreditationCertificateTracked(impactedSnapshots.accreditationCertificates)) {
				InterpretTransactionAccreditationCertificatesFuncParameter parameter = new InterpretTransactionAccreditationCertificatesFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;
				this.InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun);
				
				if(!hasRunAny.HasValue || hasRunAny.Value != true) {
					hasRunAny = hasRun;
				}
			}

			if(impactedSnapshots.chainOptions.Any() && this.IsAnyChainOptionTracked(impactedSnapshots.chainOptions)) {
				
				InterpretTransactionChainOptionsFuncParameter parameter = new InterpretTransactionChainOptionsFuncParameter();
				parameter.blockId = blockId;
				parameter.snapshotCache = snapshotCache;
				parameter.operationModes = operationMode;

				this.InterpretTransactionChainOptionsFuncOverrideSetAction.Run(transaction, parameter, out bool hasRun);
				
				if(!hasRunAny.HasValue || hasRunAny.Value != true) {
					hasRunAny = hasRun;
				}
			}

			return hasRunAny;
		}
		
		public Func<List<AccountId>, bool> IsAnyAccountTracked { get; set; } = ids => false;
		public Func<List<AccountId>, List<AccountId>> GetTrackedAccounts { get; set; } = ids => new List<AccountId>();

		public Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, bool> IsAnyAccountKeysTracked { get; set; }
		public Func<List<int>, bool> IsAnyAccreditationCertificateTracked { get; set; }
		public Func<List<int>, bool> IsAnyChainOptionTracked { get; set; }
		public SnapshotKeySet GetImpactedSnapshots(ITransaction transaction) {

			SnapshotKeySet result = new SnapshotKeySet();
			this.GetImpactedSnapshotsFuncOverrideSetAction.Run(transaction, result, out bool hasRun);

			if(!hasRun) {
				// if a transaction found no interpretation, then we most probably have an old version. lets stop here.
				throw new UnrecognizedElementException(this.blockchainType, this.chainName);
			}
			return result;
		}

		//public OverrideSetAction<SnapshotKeySet> GeneralOverrideSetAction { get; } = new OverrideSetAction<SnapshotKeySet>();
		public OverrideSetAction<SnapshotKeySet> GetImpactedSnapshotsFuncOverrideSetAction { get; } = new OverrideSetAction<SnapshotKeySet>();
		
		public OverrideSetAction<InterpretTransactionAccountsFuncParameter> InterpretTransactionAccountsFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionAccountsFuncParameter>();
		public OverrideSetAction<InterpretTransactionStandardAccountKeysFuncParameter> InterpretTransactionStandardAccountKeysFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionStandardAccountKeysFuncParameter>();
		public OverrideSetAction<CollectStandardAccountFastKeysFuncParameter> CollectStandardAccountFastKeysFuncOverrideSetAction { get; } = new OverrideSetAction<CollectStandardAccountFastKeysFuncParameter>();
		public OverrideSetAction<InterpretTransactionAccreditationCertificatesFuncParameter> InterpretTransactionAccreditationCertificatesFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionAccreditationCertificatesFuncParameter>();
		public OverrideSetAction<InterpretTransactionChainOptionsFuncParameter> InterpretTransactionChainOptionsFuncOverrideSetAction { get; } = new OverrideSetAction<InterpretTransactionChainOptionsFuncParameter>();

		public OverrideSetFunc<InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)> InterpretTransactionVerificationFuncOverrideSetAction { get; } = new OverrideSetFunc<InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)>();

		public void RegisterInterpretTransactionVerificationHandler<T>(Func<T, InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)> interpretTransactionVerificationFuncOverrideSetAction = null) {

			this.InterpretTransactionVerificationFuncOverrideSetAction.AddSet<T>(interpretTransactionVerificationFuncOverrideSetAction);
		}
		
		public void RegisterInterpretTransactionVerificationHandlerOverride<C, P>(Func<C, InterpretTransactionVerificationFuncParameter, Func<P, InterpretTransactionVerificationFuncParameter, (bool valid, RejectionCode rejectionCode)>, (bool valid, RejectionCode rejectionCode)> interpretTransactionVerificationFuncOverrideSetAction= null) 
			where C : P{

			this.InterpretTransactionVerificationFuncOverrideSetAction.AddOverrideSet<C,P>(interpretTransactionVerificationFuncOverrideSetAction);
		}
		
		public void RegisterTransactionImpactSet<T>(Action<T, SnapshotKeySet> getImpactedSnapshotsFunc = null, Action<T, InterpretTransactionAccountsFuncParameter> interpretTransactionAccountsFunc = null, Action<T, InterpretTransactionStandardAccountKeysFuncParameter> interpretTransactionStandardAccountKeysFunc = null, Action<T, CollectStandardAccountFastKeysFuncParameter> collectStandardAccountFastKeysFunc = null, Action<T, InterpretTransactionChainOptionsFuncParameter> interpretTransactionChainOptionsFunc = null, Action<T, InterpretTransactionAccreditationCertificatesFuncParameter> interpretTransactionAccreditationCertificatesFunc = null) {
			
			//TODO: all these override sets can be grouped into one, to save space and not duplicate the type hierarchy sets.
			// so, optimize and group into one...
			this.GetImpactedSnapshotsFuncOverrideSetAction.AddSet<T>(getImpactedSnapshotsFunc);
			this.InterpretTransactionAccountsFuncOverrideSetAction.AddSet<T>(interpretTransactionAccountsFunc);
			this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.AddSet<T>(interpretTransactionStandardAccountKeysFunc);
			this.CollectStandardAccountFastKeysFuncOverrideSetAction.AddSet<T>(collectStandardAccountFastKeysFunc);
			
			InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.AddSet<T>(interpretTransactionAccreditationCertificatesFunc);
			InterpretTransactionChainOptionsFuncOverrideSetAction.AddSet<T>(interpretTransactionChainOptionsFunc);
		}
		
		public void RegisterTransactionImpactSetOverride<C, P>(Action<C, SnapshotKeySet, Action<P, SnapshotKeySet>> getImpactedSnapshotsFunc= null, Action<C, InterpretTransactionAccountsFuncParameter, Action<P, InterpretTransactionAccountsFuncParameter>> interpretTransactionAccountsFunc= null, Action<C, InterpretTransactionStandardAccountKeysFuncParameter,  Action<P, InterpretTransactionStandardAccountKeysFuncParameter>> interpretTransactionStandardAccountKeysFunc= null, Action<C, CollectStandardAccountFastKeysFuncParameter, Action<P, CollectStandardAccountFastKeysFuncParameter>> collectStandardAccountFastKeysFunc= null, Action<C, InterpretTransactionChainOptionsFuncParameter, Action<P, InterpretTransactionChainOptionsFuncParameter>> interpretTransactionChainOptionsFunc = null, Action<C, InterpretTransactionAccreditationCertificatesFuncParameter, Action<P, InterpretTransactionAccreditationCertificatesFuncParameter>> interpretTransactionAccreditationCertificatesFunc= null) 
			where C : P{
			
			this.GetImpactedSnapshotsFuncOverrideSetAction.AddSet<C, P>(getImpactedSnapshotsFunc);
			this.InterpretTransactionAccountsFuncOverrideSetAction.AddSet<C, P>(interpretTransactionAccountsFunc);
			this.InterpretTransactionStandardAccountKeysFuncOverrideSetAction.AddSet<C, P>(interpretTransactionStandardAccountKeysFunc);
			this.CollectStandardAccountFastKeysFuncOverrideSetAction.AddSet<C, P>(collectStandardAccountFastKeysFunc);
			
			InterpretTransactionAccreditationCertificatesFuncOverrideSetAction.AddSet<C, P>(interpretTransactionAccreditationCertificatesFunc);
			InterpretTransactionChainOptionsFuncOverrideSetAction.AddSet<C, P>(interpretTransactionChainOptionsFunc);
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

		public class CollectStandardAccountFastKeysFuncParameter {
			public long blockId { get; set; }
			public ChainConfigurations.FastKeyTypes types { get; set; }
			public List<FastKeyMetadata> results { get; set; }
		}
		
		public class InterpretTransactionChainOptionsFuncParameter {
			
			public long blockId { get; set; }
			public IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}
		
		public class InterpretTransactionVerificationFuncParameter {

			public bool isException { get; set; }
			public ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> entryCache{ get; set; }
			public (bool result, RejectionCode code) results{ get; set; }
		}
		
		public class InterpretTransactionAccreditationCertificatesFuncParameter {
			
			public long blockId { get; set; }
			public ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache { get; set; }
			public TransactionImpactSet.OperationModes operationModes { get; set; }
		}
		
		
	}
	
	public class FastKeyMetadata {
		public AccountId AccountId { get; set; }
		public byte Ordinal { get; set; }
		public byte[] PublicKey { get; set; }
	}
}