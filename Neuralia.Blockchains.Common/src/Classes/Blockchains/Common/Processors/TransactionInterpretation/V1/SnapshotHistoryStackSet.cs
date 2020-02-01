using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Core.General.Types;

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

		Dictionary<long, List<Action<CONTEXT>>> CompileStandardAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT> create, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT> update, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Action<CONTEXT>>> CompileJointAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT> create, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT> update, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Action<CONTEXT>>> CompileStandardAccountKeysHistorySets<CONTEXT>(Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT> create, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT> update, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Action<CONTEXT>>> CompileAccreditationCertificatesHistorySets<CONTEXT>(Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT> create, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT> update, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT> delete)
			where CONTEXT : DbContext;

		Dictionary<long, List<Action<CONTEXT>>> CompileChainOptionsHistorySets<CONTEXT>(Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> create, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> update, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT> delete)
			where CONTEXT : DbContext;

		List<long> CompileStandardAccountHistoryImpactedIds();
		List<long> CompileJointAccountHistoryImpactedIds();
		List<long> CompileStandardAccountKeysHistoryImpactedIds();
		List<long> CompileAccreditationCertificatesHistoryImpactedIds();
		List<long> CompileChainOptionsHistoryImpactedIds();
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

		public Dictionary<AccountId, List<(STANDARD_ACCOUNT_SNAPSHOT entry, AccountId subKey, SnapshotCache.EntryStatus status)>> simpleAccounts;
		public Dictionary<(long AccountId, byte OrdinalId), List<(STANDARD_ACCOUNT_KEY_SNAPSHOT entry, SnapshotCache.EntryStatus status)>> standardAccountKeys;

		public List<long> CompileStandardAccountHistoryImpactedIds() {
			return this.simpleAccounts.Keys.Select(k => k.ToLongRepresentation()).Distinct().ToList();
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

		public Dictionary<long, List<Action<CONTEXT>>> CompileStandardAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT> create, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT> update, Func<CONTEXT, AccountId, STANDARD_ACCOUNT_SNAPSHOT> delete)
			where CONTEXT : DbContext {
			return this.CompileSubkeyHistorySets(this.simpleAccounts, create, update, delete).ToDictionary(e => e.Key.SequenceId, e => e.Value);
		}

		public Dictionary<long, List<Action<CONTEXT>>> CompileJointAccountHistorySets<CONTEXT>(Func<CONTEXT, AccountId, AccountId, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT> create, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT> update, Func<CONTEXT, AccountId, JOINT_ACCOUNT_SNAPSHOT> delete)
			where CONTEXT : DbContext {
			return this.CompileSubkeyHistorySets(this.jointAccounts, create, update, delete).ToDictionary(e => e.Key.SequenceId, e => e.Value);
		}

		public Dictionary<long, List<Action<CONTEXT>>> CompileStandardAccountKeysHistorySets<CONTEXT>(Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT> create, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT> update, Func<CONTEXT, (long AccountId, byte OrdinalId), STANDARD_ACCOUNT_KEY_SNAPSHOT> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.standardAccountKeys, create, update, delete).GroupBy(e => e.Key.AccountId.ToAccountId().SequenceId).ToDictionary(e => e.Key, e => e.SelectMany(e2 => e2.Value).ToList());
		}

		public Dictionary<long, List<Action<CONTEXT>>> CompileAccreditationCertificatesHistorySets<CONTEXT>(Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT> create, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT> update, Func<CONTEXT, int, ACCREDITATION_CERTIFICATE_SNAPSHOT> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.accreditationCertificates, create, update, delete).ToDictionary(e => (long) e.Key, e => e.Value);
		}

		public Dictionary<long, List<Action<CONTEXT>>> CompileChainOptionsHistorySets<CONTEXT>(Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> create, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> update, Func<CONTEXT, int, CHAIN_OPTIONS_SNAPSHOT> delete)
			where CONTEXT : DbContext {
			return this.CompileHistorySets(this.chainOptions, create, update, delete).ToDictionary(e => (long) e.Key, e => e.Value);
		}

		public bool Any() {
			return this.simpleAccounts.Any() || this.jointAccounts.Any() || this.standardAccountKeys.Any() || this.accreditationCertificates.Any() || this.chainOptions.Any();
		}

		private Dictionary<KEY, List<Action<CONTEXT>>> CompileHistorySets<CONTEXT, KEY, ENTRY>(Dictionary<KEY, List<(ENTRY entry, SnapshotCache.EntryStatus status)>> source, Func<CONTEXT, KEY, ENTRY, ENTRY> create, Func<CONTEXT, KEY, ENTRY, ENTRY> update, Func<CONTEXT, KEY, ENTRY> delete)
			where CONTEXT : DbContext {

			var results = new Dictionary<KEY, List<Action<CONTEXT>>>();

			foreach(var entry in source) {

				if(!results.ContainsKey(entry.Key)) {
					results.Add(entry.Key, new List<Action<CONTEXT>>());
				}

				var list = results[entry.Key];

				foreach((ENTRY entry, SnapshotCache.EntryStatus status) timeEntry in entry.Value) {

					if(timeEntry.status == SnapshotCache.EntryStatus.Existing) {
					} else if(timeEntry.status == SnapshotCache.EntryStatus.New) {
						list.Add(db => create(db, entry.Key, timeEntry.entry));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Modified) {
						list.Add(db => update(db, entry.Key, timeEntry.entry));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Deleted) {
						list.Add(db => delete(db, entry.Key));
					}
				}
			}

			return results;
		}

		private Dictionary<KEY, List<Action<CONTEXT>>> CompileSubkeyHistorySets<CONTEXT, KEY, ENTRY>(Dictionary<KEY, List<(ENTRY entry, KEY subKey, SnapshotCache.EntryStatus status)>> source, Func<CONTEXT, KEY, KEY, ENTRY, ENTRY> create, Func<CONTEXT, KEY, ENTRY, ENTRY> update, Func<CONTEXT, KEY, ENTRY> delete)
			where CONTEXT : DbContext {

			var results = new Dictionary<KEY, List<Action<CONTEXT>>>();

			foreach(var entry in source) {

				if(!results.ContainsKey(entry.Key)) {
					results.Add(entry.Key, new List<Action<CONTEXT>>());
				}

				var list = results[entry.Key];

				foreach((ENTRY entry, KEY subKey, SnapshotCache.EntryStatus status) timeEntry in entry.Value) {

					if(timeEntry.status == SnapshotCache.EntryStatus.Existing) {
					} else if(timeEntry.status == SnapshotCache.EntryStatus.New) {

						list.Add(db => create(db, entry.Key, timeEntry.subKey, timeEntry.entry));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Modified) {
						list.Add(db => update(db, entry.Key, timeEntry.entry));
					} else if(timeEntry.status == SnapshotCache.EntryStatus.Deleted) {
						list.Add(db => delete(db, entry.Key));
					}
				}
			}

			return results;
		}
	}
}