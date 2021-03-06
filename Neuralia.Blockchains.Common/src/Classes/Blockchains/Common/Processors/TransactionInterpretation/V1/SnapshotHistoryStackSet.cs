using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1 {

	public interface ISnapshotHistoryStackSet {
	}

	public interface ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotHistoryStackSet
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {

		List<long> CompileStandardAccountHistoryImpactedIds();
		List<long> CompileJointAccountHistoryImpactedIds();
		List<long> CompileStandardAccountKeysHistoryImpactedIds();
		List<long> CompileAccreditationCertificatesHistoryImpactedIds();
		List<long> CompileChainOptionsHistoryImpactedIds();

		Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileStandardAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, STANDARD_ACCOUNT_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> create, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> update, Func<CONTEXT, AccountId, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> delete)
			where CONTEXT : DbContext;

		Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileJointAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, JOINT_ACCOUNT_SNAPSHOT, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> create, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> update, Func<CONTEXT, AccountId, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> delete)
			where CONTEXT : DbContext;

		Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileStandardAccountKeysHistorySets<CONTEXT>(Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> create, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> update, Func<CONTEXT, (long AccountId, byte OrdinalId), LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Func<CONTEXT, LockContext, Task>>> CompileAccreditationCertificatesHistorySets<CONTEXT>(Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> create, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> update, Func<CONTEXT, int, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Func<CONTEXT, LockContext, Task>>> CompileChainOptionsHistorySets<CONTEXT>(Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> create, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> update, Func<CONTEXT, int, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> delete)
			where CONTEXT : DbContext;
	}

	public class SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : ISnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshot<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshot<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshot, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshot<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccount, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshot, new() {
		public Dictionary<int, List<(ACCREDITATION_CERTIFICATE_SNAPSHOT entry, SnapshotCache.EntryStatus status)>> accreditationCertificates;
		public Dictionary<int, List<(CHAIN_OPTIONS_SNAPSHOT entry, SnapshotCache.EntryStatus status)>> chainOptions;
		public Dictionary<AccountId, List<(JOINT_ACCOUNT_SNAPSHOT entry, AccountId subKey, SnapshotCache.EntryStatus status)>> jointAccounts;

		public Dictionary<AccountId, List<(STANDARD_ACCOUNT_SNAPSHOT entry, AccountId subKey, SnapshotCache.EntryStatus status)>> standardAccounts;
		public Dictionary<(long AccountId, byte OrdinalId), List<(STANDARD_ACCOUNT_KEY_SNAPSHOT entry, SnapshotCache.EntryStatus status)>> standardAccountKeys;

		public List<long> CompileStandardAccountHistoryImpactedIds() {
			return this.standardAccounts.Keys.Select(k => k.ToLongRepresentation()).Distinct().ToList();
		}

		public List<long> CompileJointAccountHistoryImpactedIds() {
			return this.jointAccounts.Keys.Select(k => k.ToLongRepresentation()).Distinct().ToList();
		}

		public List<long> CompileStandardAccountKeysHistoryImpactedIds() {
			return this.standardAccountKeys.Keys.Select(k => k.AccountId).Distinct().ToList();
		}

		public List<long> CompileAccreditationCertificatesHistoryImpactedIds() {
			return this.accreditationCertificates.Keys.Select(k => (long) k).Distinct().ToList();
		}

		public List<long> CompileChainOptionsHistoryImpactedIds() {
			return this.chainOptions.Keys.Select(k => (long) k).Distinct().ToList();
		}

		public Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileStandardAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, STANDARD_ACCOUNT_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> create, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> update, Func<CONTEXT, AccountId, LockContext, Task<STANDARD_ACCOUNT_SNAPSHOT>> delete)
			where CONTEXT : DbContext {
			return this.CompileSubkeyHistorySets(this.standardAccounts, create, update, delete).ToDictionary(e => e.Key, e => e.Value);
		}

		public Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileJointAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, JOINT_ACCOUNT_SNAPSHOT, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> create, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> update, Func<CONTEXT, AccountId, LockContext, Task<JOINT_ACCOUNT_SNAPSHOT>> delete)
			where CONTEXT : DbContext {
			return this.CompileSubkeyHistorySets(this.jointAccounts, create, update, delete).ToDictionary(e => e.Key, e => e.Value);
		}

		public Dictionary<AccountId, List<Func<CONTEXT, LockContext, Task>>> CompileStandardAccountKeysHistorySets<CONTEXT>(Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> create, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> update, Func<CONTEXT, (long AccountId, byte OrdinalId), LockContext, Task<STANDARD_ACCOUNT_KEY_SNAPSHOT>> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.standardAccountKeys, create, update, delete).GroupBy(e => e.Key.AccountId.ToAccountId()).ToDictionary(e => e.Key, e => e.SelectMany(e2 => e2.Value).ToList());
		}

		public Dictionary<long, List<Func<CONTEXT, LockContext, Task>>> CompileAccreditationCertificatesHistorySets<CONTEXT>(Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> create, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> update, Func<CONTEXT, int, LockContext, Task<ACCREDITATION_CERTIFICATE_SNAPSHOT>> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.accreditationCertificates, create, update, delete).ToDictionary(e => (long) e.Key, e => e.Value);
		}

		public Dictionary<long, List<Func<CONTEXT, LockContext, Task>>> CompileChainOptionsHistorySets<CONTEXT>(Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> create, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> update, Func<CONTEXT, int, LockContext, Task<CHAIN_OPTIONS_SNAPSHOT>> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.chainOptions, create, update, delete).ToDictionary(e => (long) e.Key, e => e.Value);
		}

		public bool Any() {
			return this.standardAccounts.Any() || this.jointAccounts.Any() || this.standardAccountKeys.Any() || this.accreditationCertificates.Any() || this.chainOptions.Any();
		}

		private Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>> CompileHistorySets<CONTEXT, KEY, ENTRY>(Dictionary<KEY, List<(ENTRY entry, SnapshotCache.EntryStatus status)>> source, Func<CONTEXT, KEY, ENTRY, LockContext, Task<ENTRY>> create, Func<CONTEXT, KEY, ENTRY, LockContext, Task<ENTRY>> update, Func<CONTEXT, KEY, LockContext, Task<ENTRY>> delete)
			where CONTEXT : DbContext {

			Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>> results = new Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>>();

			foreach((KEY key, List<(ENTRY entry, SnapshotCache.EntryStatus status)> value) in source) {

				if(!results.ContainsKey(key)) {
					results.Add(key, new List<Func<CONTEXT, LockContext, Task>>());
				}

				List<Func<CONTEXT, LockContext, Task>> list = results[key];

				foreach((ENTRY entry, SnapshotCache.EntryStatus status) timeEntry in value) {

					if(timeEntry.status == SnapshotCache.EntryStatus.Existing) {
					} else if(timeEntry.status == SnapshotCache.EntryStatus.New) {
						list.Add((db, lc) => create(db, key, timeEntry.entry, lc));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Modified) {
						list.Add((db, lc) => update(db, key, timeEntry.entry, lc));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Deleted) {
						list.Add((db, lc) => delete(db, key, lc));
					}
				}
			}

			return results;
		}

		private Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>> CompileSubkeyHistorySets<CONTEXT, KEY, ENTRY>(Dictionary<KEY, List<(ENTRY entry, KEY subKey, SnapshotCache.EntryStatus status)>> source, Func<CONTEXT, KEY, KEY, ENTRY, LockContext, Task<ENTRY>> create, Func<CONTEXT, KEY, ENTRY, LockContext, Task<ENTRY>> update, Func<CONTEXT, KEY, LockContext, Task<ENTRY>> delete)
			where CONTEXT : DbContext {

			Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>> results = new Dictionary<KEY, List<Func<CONTEXT, LockContext, Task>>>();

			foreach(KeyValuePair<KEY, List<(ENTRY entry, KEY subKey, SnapshotCache.EntryStatus status)>> entry in source) {

				if(!results.ContainsKey(entry.Key)) {
					results.Add(entry.Key, new List<Func<CONTEXT, LockContext, Task>>());
				}

				List<Func<CONTEXT, LockContext, Task>> list = results[entry.Key];

				foreach((ENTRY entry, KEY subKey, SnapshotCache.EntryStatus status) timeEntry in entry.Value) {

					if(timeEntry.status == SnapshotCache.EntryStatus.Existing) {
					} else if(timeEntry.status == SnapshotCache.EntryStatus.New) {

						list.Add((db, lc) => create(db, entry.Key, timeEntry.subKey, timeEntry.entry, lc));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Modified) {
						list.Add((db, lc) => update(db, entry.Key, timeEntry.entry, lc));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Deleted) {
						list.Add((db, lc) => delete(db, entry.Key, lc));
					}
				}
			}

			return results;
		}
	}
}