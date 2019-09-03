using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public interface ITransactionImpactSet {
		Type ActingType { get; }

		Func<List<AccountId>, bool> IsAnyAccountTracked { get; set; }
		Func<List<AccountId>, List<AccountId>> GetTrackedAccounts { get; set; }

		Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, bool> IsAnyAccountKeysTracked { get; set; }
		Func<List<int>, bool> IsAnyAccreditationCertificateTracked { get; set; }
		Func<List<int>, bool> IsAnyChainOptionTracked { get; set; }

		SnapshotKeySet GetImpactedSnapshots(ITransaction transaction);
	}

	public interface ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionImpactSet
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

		void InterpretTransaction(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys, ChainConfigurations.FastKeyTypes enabledFastKeyTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocal, bool isDispatched, Action<TransactionId, RejectionCode> TransactionRejected);
	}

	public interface ITransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where T : ITransaction
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

		Action<T, SnapshotKeySet> GetImpactedSnapshotsFunc { get; set; }

		Action<T, long, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionAccountsFunc { get; set; }
		Action<T, long, IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionStandardAccountKeysFunc { get; set; }
		Action<T, long, IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionAccreditationCertificatesFunc { get; set; }
		Action<T, long, IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionChainOptionsFunc { get; set; }

		Func<T, long, ChainConfigurations.FastKeyTypes, List<FastKeyMetadata>> CollectStandardAccountFastKeysFunc { get; set; }

		Func<T, bool, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, (bool result, RejectionCode code)> InterpretTransactionVerificationFunc { get; set; }

		void AddInterpretTransactionVerificationHandler(Func<T, bool, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, (bool result, RejectionCode code)> action);
	}

	public static class TransactionImpactSet {
		public enum OperationModes {
			Real,
			Simulated
		}
	}

	public class TransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ITransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where T : ITransaction
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

		public Func<List<AccountId>, bool> IsAnyAccountTracked { get; set; } = ids => false;
		public Func<List<AccountId>, List<AccountId>> GetTrackedAccounts { get; set; } = ids => new List<AccountId>();

		public Func<List<(long AccountId, byte OrdinalId)>, List<AccountId>, bool> IsAnyAccountKeysTracked { get; set; }
		public Func<List<int>, bool> IsAnyAccreditationCertificateTracked { get; set; }
		public Func<List<int>, bool> IsAnyChainOptionTracked { get; set; }

		public Action<T, SnapshotKeySet> GetImpactedSnapshotsFunc { get; set; }

		public Action<T, long, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionAccountsFunc { get; set; }
		public Action<T, long, IAccountkeysSnapshotCacheSet<STANDARD_ACCOUNT_KEY_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionStandardAccountKeysFunc { get; set; }
		public Action<T, long, IAccreditationCertificateSnapshotCacheSet<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionAccreditationCertificatesFunc { get; set; }
		public Action<T, long, IChainOptionsSnapshotCacheSet<CHAIN_OPTIONS_SNAPSHOT>, TransactionImpactSet.OperationModes> InterpretTransactionChainOptionsFunc { get; set; }

		public Func<T, long, ChainConfigurations.FastKeyTypes, List<FastKeyMetadata>> CollectStandardAccountFastKeysFunc { get; set; }

		public Func<T, bool, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, (bool result, RejectionCode code)> InterpretTransactionVerificationFunc { get; set; }

		public virtual void InterpretTransaction(ITransaction transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys, ChainConfigurations.FastKeyTypes enabledFastKeyTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected) {

			this.Run(transaction, t => {
				// if we must run a simulation, we do so
				bool accept = true;
				RejectionCode code = RejectionCodes.Instance.NONE;

				if(this.InterpretTransactionVerificationFunc != null) {

					// ok, for the simulation, we build a cache set proxy

					// run the simulation
					bool exception = false;

					try {
						snapshotCache.BeginTransaction();

						this.RunInterpretationFunctions(t, blockId, impactedSnapshots, null, enabledFastKeyTypes, TransactionImpactSet.OperationModes.Simulated, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected);

						(accept, code) = this.InterpretTransactionVerificationFunc(t, exception, snapshotCache);
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

						// ok, lets run the real thing
						this.RunInterpretationFunctions(t, blockId, impactedSnapshots, fastKeys, enabledFastKeyTypes, operationMode, snapshotCache, isLocalAccount, isDispatchedAccount, TransactionRejected);

						// if we operate in local mode, then lets do it here
						snapshotCache.CommitTransaction();
					} catch {
						//TODO: do anything here?
						snapshotCache.RollbackTransaction();
					}
				} else {
					TransactionRejected?.Invoke(transaction.TransactionId, code);
				}
			});
		}

		public virtual SnapshotKeySet GetImpactedSnapshots(ITransaction transaction) {

			SnapshotKeySet results = new SnapshotKeySet();

			this.Run(transaction, t => {
				SnapshotKeySet currentResults = new SnapshotKeySet();
				this.GetImpactedSnapshotsFunc?.Invoke(t, currentResults);
				results.Add(currentResults);
			});

			return results;
		}

		public void AddInterpretTransactionVerificationHandler(Func<T, bool, ISnapshotCacheSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, (bool result, RejectionCode code)> action) {

			// override the existing method
			var source = this.InterpretTransactionVerificationFunc;

			this.InterpretTransactionVerificationFunc = (t, isException, results) => {

				// call the parent
				var result = source?.Invoke(t, isException, results);

				if(result.HasValue && !result.Value.result) {
					return result.Value;
				}

				result = action?.Invoke(t, isException, results);

				if(result.HasValue) {
					return result.Value;
				}

				return (true, RejectionCodes.Instance.NONE);
			};
		}

		public Type ActingType => typeof(T);

		private void Run(ITransaction transaction, Action<T> action) {
			if(transaction is T castedTransaction) {
				action(castedTransaction);
			}
		}

		private void RunInterpretationFunctions(T transaction, long blockId, SnapshotKeySet impactedSnapshots, Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys, ChainConfigurations.FastKeyTypes enabledFastKeyTypes, TransactionImpactSet.OperationModes operationMode, ISnapshotCacheSetInternal<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotCache, bool isLocalAccount, bool isDispatchedAccount, Action<TransactionId, RejectionCode> TransactionRejected) {

			var accounts = impactedSnapshots.standardAccounts.ToList();
			accounts.AddRange(impactedSnapshots.jointAccounts);

			if(this.IsAnyAccountTracked(accounts)) {
				this.InterpretTransactionAccountsFunc?.Invoke(transaction, blockId, snapshotCache, operationMode);
			}

			if(this.IsAnyAccountKeysTracked(impactedSnapshots.accountKeys, impactedSnapshots.standardAccounts)) {
				this.InterpretTransactionStandardAccountKeysFunc?.Invoke(transaction, blockId, snapshotCache, operationMode);
			}

			if((fastKeys != null) && (operationMode == TransactionImpactSet.OperationModes.Real)) {
				var fastKeysdatas = this.CollectStandardAccountFastKeysFunc?.Invoke(transaction, blockId, enabledFastKeyTypes);

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

			if(this.IsAnyAccreditationCertificateTracked(impactedSnapshots.accreditationCertificates)) {
				this.InterpretTransactionAccreditationCertificatesFunc?.Invoke(transaction, blockId, snapshotCache, operationMode);
			}

			if(this.IsAnyChainOptionTracked(impactedSnapshots.chainOptions)) {
				this.InterpretTransactionChainOptionsFunc?.Invoke(transaction, blockId, snapshotCache, operationMode);
			}
		}
	}

	public interface ISupersetTransactionImpactSet : ITransactionImpactSet {

		Type ParentActingType { get; }
		void RegisterOverrides();
	}

	public interface ISupersetTransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISupersetTransactionImpactSet, ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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

		ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> Parent { get; set; }
	}

	public interface ISupersetTransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISupersetTransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, ITransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where T : ITransaction
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
	}

	public class SupersetTransactionImpactSet<T, T_PARENT, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : TransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>, ISupersetTransactionImpactSet<T, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where T : ITransaction, T_PARENT
		where T_PARENT : ITransaction
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

		public ITransactionImpactSet<ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> Parent { get; set; }

		public Type ParentActingType => typeof(T_PARENT);

		public void RegisterOverrides() {
			var castedParent = (ITransactionImpactSet<T_PARENT, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_FEATURE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>) this.Parent;

			var source = this.GetImpactedSnapshotsFunc;

			this.GetImpactedSnapshotsFunc = (t, snapshotKeys) => {

				// class the parent
				castedParent.GetImpactedSnapshotsFunc?.Invoke(t, snapshotKeys);

				source?.Invoke(t, snapshotKeys);
			};

			var source2 = this.InterpretTransactionAccountsFunc;

			this.InterpretTransactionAccountsFunc = (t, blockId, snapshotCache, mode) => {

				// class the parent
				castedParent.InterpretTransactionAccountsFunc?.Invoke(t, blockId, snapshotCache, mode);

				source2?.Invoke(t, blockId, snapshotCache, mode);
			};

			var source3 = this.InterpretTransactionStandardAccountKeysFunc;

			this.InterpretTransactionStandardAccountKeysFunc = (t, blockId, snapshotCache, mode) => {

				// class the parent
				castedParent.InterpretTransactionStandardAccountKeysFunc?.Invoke(t, blockId, snapshotCache, mode);

				source3?.Invoke(t, blockId, snapshotCache, mode);
			};

			var source4 = this.InterpretTransactionAccreditationCertificatesFunc;

			this.InterpretTransactionAccreditationCertificatesFunc = (t, blockId, snapshotCache, mode) => {

				// class the parent
				castedParent.InterpretTransactionAccreditationCertificatesFunc?.Invoke(t, blockId, snapshotCache, mode);

				source4?.Invoke(t, blockId, snapshotCache, mode);
			};

			var source5 = this.InterpretTransactionChainOptionsFunc;

			this.InterpretTransactionChainOptionsFunc = (t, blockId, snapshotCache, mode) => {

				// class the parent
				castedParent.InterpretTransactionChainOptionsFunc?.Invoke(t, blockId, snapshotCache, mode);

				source5?.Invoke(t, blockId, snapshotCache, mode);
			};

			var source6 = this.InterpretTransactionVerificationFunc;

			this.InterpretTransactionVerificationFunc = (t, isException, results) => {

				// class the parent
				var result = castedParent.InterpretTransactionVerificationFunc?.Invoke(t, isException, results);

				if(result.HasValue && !result.Value.result) {
					return result.Value;
				}

				result = source6?.Invoke(t, isException, results);

				if(result.HasValue) {
					return result.Value;
				}

				return (true, RejectionCodes.Instance.NONE);
			};

			var source7 = this.CollectStandardAccountFastKeysFunc;

			this.CollectStandardAccountFastKeysFunc = (t, blockId, types) => {

				// class the parent
				var results = castedParent.CollectStandardAccountFastKeysFunc?.Invoke(t, blockId, types);

				Dictionary<(AccountId AccountId, byte Ordinal), byte[]> dictionaryResults = null;

				if(results != null) {
					dictionaryResults = results.ToDictionary(e => (e.AccountId, e.Ordinal), e => e.PublicKey);
				}

				var subresults = source7?.Invoke(t, blockId, types);

				if(subresults != null) {
					if(dictionaryResults == null) {
						dictionaryResults = new Dictionary<(AccountId AccountId, byte Ordinal), byte[]>();
					}

					foreach(FastKeyMetadata entry in subresults) {
						(AccountId AccountId, byte Ordinal) key = (entry.AccountId, entry.Ordinal);

						if(dictionaryResults.ContainsKey(key)) {
							dictionaryResults[key] = entry.PublicKey;
						} else {
							dictionaryResults.Add(key, entry.PublicKey);
						}
					}
				}

				return dictionaryResults?.Select(e => new FastKeyMetadata {AccountId = e.Key.AccountId, Ordinal = e.Key.Ordinal, PublicKey = e.Value}).ToList();
			};
		}
	}

	public class FastKeyMetadata {
		public AccountId AccountId { get; set; }
		public byte Ordinal { get; set; }
		public byte[] PublicKey { get; set; }
	}
}