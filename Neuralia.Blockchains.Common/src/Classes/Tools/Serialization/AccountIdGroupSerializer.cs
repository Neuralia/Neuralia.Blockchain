using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Tools.Serialization {

	/// <summary>
	///     a utility class to efficiently serialize a series of account ids
	/// </summary>
	public static class AccountIdGroupSerializer {

		private static SequantialOffsetCalculator GetOffsetCalculator(bool onlyPublicAccounts) {
			return onlyPublicAccounts ? new AccountsOffsetsCalculator() : new SequantialOffsetCalculator();
		}

		public static void Dehydrate(List<AccountId> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId> parameters = null) {

			Dehydrate(accountIds.ToDictionary(e => e, e => e), dehydrator, onlyPublicAccounts, parameters);
		}

		public static void Dehydrate<T>(Dictionary<AccountId, T> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<T, AccountId> parameters = null) {

			SequantialOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			var groups = accountIds.GroupBy(a => a.Key.AccountTypeRaw).OrderBy(g => g.Key).ToList();

			int groupsCount = groups.Count();

			parameters?.Initialize?.Invoke(groupsCount);

			adaptiveLong.Value = groupsCount;
			adaptiveLong.Dehydrate(dehydrator);

			if(groups.Any()) {
				int groupIndex = 0;

				foreach(var group in groups) {

					int groupCount = group.Count();
					accountCalculator.Reset();
					parameters?.InitializeGroup?.Invoke(groupIndex, groupCount);

					adaptiveLong.Value = groupCount;
					adaptiveLong.Dehydrate(dehydrator);

					dehydrator.Write(group.Key);

					if(group.Any()) {

						var orderedAccounts = group.OrderBy(a => a.Key.SequenceId).ToList();

						accountCalculator.Reset();

						int index = 0;

						foreach(var entry in orderedAccounts) {
							adaptiveLong.Value = accountCalculator.CalculateOffset(entry.Key.SequenceId);
							adaptiveLong.Dehydrate(dehydrator);

							parameters?.DehydrateExtraData?.Invoke(entry.Value, entry.Key, adaptiveLong.Value, index, dehydrator);

							accountCalculator.AddLastOffset();
							index++;
						}
					}

					parameters?.FinalizeGroup?.Invoke(groupIndex);
					groupIndex++;
				}
			}
		}

		public static void Dehydrate<T>(Dictionary<long, T> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<T, long> parameters = null) {

			SequantialOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Value = accountIds.Count();
			adaptiveLong.Dehydrate(dehydrator);
			dehydrator.Write((ushort) accountIds.Count());

			if(accountIds.Any()) {

				var orderedAccounts = accountIds.OrderBy(a => a).ToList();

				accountCalculator.Reset();

				int index = 0;

				foreach(var entry in orderedAccounts) {
					adaptiveLong.Value = accountCalculator.CalculateOffset(entry.Key);
					adaptiveLong.Dehydrate(dehydrator);

					parameters?.DehydrateExtraData?.Invoke(entry.Value, entry.Key, adaptiveLong.Value, index, dehydrator);

					accountCalculator.AddLastOffset();
					index++;
				}
			}
		}

		public static List<AccountId> Rehydrate(IDataRehydrator rehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = null) {

			SequantialOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			var accountIds = new List<AccountId>();

			adaptiveLong.Rehydrate(rehydrator);
			int groupsCount = (int) adaptiveLong.Value;

			parameters?.Initialize?.Invoke(groupsCount);

			if(groupsCount != 0) {

				for(int groupIndex = 0; groupIndex < groupsCount; groupIndex++) {

					adaptiveLong.Rehydrate(rehydrator);
					int groupCount = (int) adaptiveLong.Value;
					Enums.AccountTypes accountType = (Enums.AccountTypes) rehydrator.ReadByte();

					parameters?.InitializeGroup?.Invoke(groupIndex, groupCount, accountType);
					accountCalculator.Reset();

					if(groupCount != 0) {

						accountCalculator.Reset();

						for(int i = 0; i < groupCount; i++) {

							adaptiveLong.Rehydrate(rehydrator);
							AccountId accountId = new AccountId(accountCalculator.RebuildValue(adaptiveLong.Value), accountType);

							accountIds.Add(accountId);

							parameters?.RehydrateExtraData?.Invoke(accountId, adaptiveLong.Value, i, rehydrator);

							accountCalculator.AddLastOffset();
						}
					}

					parameters?.FinalizeGroup?.Invoke(groupIndex);
				}
			}

			return accountIds;
		}

		public static List<long> RehydrateLong(IDataRehydrator rehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerRehydrateParameters<long> parameters = null) {

			SequantialOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			var accountIds = new List<long>();

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Rehydrate(rehydrator);
			int count = (int) adaptiveLong.Value;

			parameters?.Initialize?.Invoke(count);

			for(int i = 0; i < count; i++) {

				adaptiveLong.Rehydrate(rehydrator);
				long accountId = accountCalculator.CalculateOffset(adaptiveLong.Value);

				accountIds.Add(accountId);

				parameters?.RehydrateExtraData?.Invoke(accountId, adaptiveLong.Value, i, rehydrator);

				accountCalculator.AddLastOffset();
			}

			return accountIds;
		}

		public class AccountIdGroupSerializerDehydrateParameters<T, K> {
			/// <summary>
			///     content entry, account Id, offset of accountID, entry index
			/// </summary>
			public Action<T, K, long, int, IDataDehydrator> DehydrateExtraData;

			/// <summary>
			///     group index
			/// </summary>
			public Action<int> FinalizeGroup;

			/// <summary>
			///     total group count
			/// </summary>
			public Action<int> Initialize;

			/// <summary>
			///     group index, group entry count
			/// </summary>
			public Action<int, int> InitializeGroup;
		}

		public class AccountIdGroupSerializerRehydrateParameters<K> {
			/// <summary>
			///     group index
			/// </summary>
			public Action<int> FinalizeGroup;

			/// <summary>
			///     total group count
			/// </summary>
			public Action<int> Initialize;

			/// <summary>
			///     group index, group entry count
			/// </summary>
			public Action<int, int, Enums.AccountTypes> InitializeGroup;

			/// <summary>
			///     content entry, account Id, offset of accountID, entry index
			/// </summary>
			public Action<K, long, int, IDataRehydrator> RehydrateExtraData;
		}
	}
}