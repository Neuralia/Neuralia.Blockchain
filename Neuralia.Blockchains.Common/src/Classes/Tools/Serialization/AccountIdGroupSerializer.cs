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

		private static SequentialLongOffsetCalculator GetOffsetCalculator(bool onlyPublicAccounts) {
			return onlyPublicAccounts ? new AccountsOffsetsCalculator() : new SequentialLongOffsetCalculator();
		}

		public static void Dehydrate(List<AccountId> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId> parameters = null) {

			Dehydrate(accountIds.ToDictionary(e => e, e => e), dehydrator, onlyPublicAccounts, parameters);
		}

		public static void Dehydrate<T>(Dictionary<AccountId, T> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<T, AccountId> parameters = null) {

			SequentialLongOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			List<IGrouping<byte, KeyValuePair<AccountId, T>>> groups = accountIds.GroupBy(a => a.Key.AccountTypeRaw).OrderBy(g => g.Key).ToList();

			int groupsCount = groups.Count;

			if(parameters?.Initialize != null) {
				parameters?.Initialize(groupsCount);
			}

			adaptiveLong.Value = groupsCount;
			adaptiveLong.Dehydrate(dehydrator);

			int totalIndex = 0;
			
			if(groups.Any()) {
				int groupIndex = 0;

				// perform a pre run if required
				if(parameters?.DataPrerun != null) {
					foreach(IGrouping<byte, KeyValuePair<AccountId, T>> group in groups) {

						if(group.Any()) {

							List<KeyValuePair<AccountId, T>> orderedAccounts = group.OrderBy(a => a.Key.SequenceId).ToList();

							int index = 0;

							foreach(KeyValuePair<AccountId, T> entry in orderedAccounts) {
								
								parameters.DataPrerun(entry.Value, entry.Key, adaptiveLong.Value, index, totalIndex, dehydrator);
								
								index++;
								totalIndex++;
							}
						}

						groupIndex++;
					}
					
					if(parameters?.DataPrerunExecuted != null) {
						parameters?.DataPrerunExecuted();
					}
				}

				//reset 
				groupIndex = 0;
				totalIndex = 0;
				
				foreach(IGrouping<byte, KeyValuePair<AccountId, T>> group in groups) {

					int groupCount = group.Count();
					accountCalculator.Reset();

					if(parameters?.InitializeGroup != null) {
						parameters?.InitializeGroup(groupIndex, groupCount);
					}

					adaptiveLong.Value = groupCount;
					adaptiveLong.Dehydrate(dehydrator);

					dehydrator.Write(group.Key);

					if(group.Any()) {

						List<KeyValuePair<AccountId, T>> orderedAccounts = group.OrderBy(a => a.Key.SequenceId).ToList();

						accountCalculator.Reset();

						int index = 0;

						foreach(KeyValuePair<AccountId, T> entry in orderedAccounts) {
							adaptiveLong.Value = accountCalculator.CalculateOffset(entry.Key.SequenceId);
							adaptiveLong.Dehydrate(dehydrator);

							if(parameters?.DehydrateExtraData != null) {
								parameters?.DehydrateExtraData(entry.Value, entry.Key, adaptiveLong.Value, index, totalIndex, dehydrator);
							}

							accountCalculator.AddLastOffset();
							index++;
							totalIndex++;
						}
					}

					if(parameters?.FinalizeGroup != null) {
						parameters?.FinalizeGroup(groupIndex);
					}

					groupIndex++;
				}
			}
		}

		public static void Dehydrate<T>(Dictionary<long, T> accountIds, IDataDehydrator dehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerDehydrateParameters<T, long> parameters = null) {

			SequentialLongOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Value = accountIds.Count;
			adaptiveLong.Dehydrate(dehydrator);
			dehydrator.Write((ushort) accountIds.Count);

			int totalIndex = 0;
			if(accountIds.Any()) {

				if(parameters?.DataPrerunExecuted != null) {
					parameters?.DataPrerunExecuted();
				}
				
				List<KeyValuePair<long, T>> orderedAccounts = accountIds.OrderBy(a => a).ToList();

				accountCalculator.Reset();

				int index = 0;

				foreach(KeyValuePair<long, T> entry in orderedAccounts) {
					if(parameters?.DataPrerun != null) {
						parameters?.DataPrerun(entry.Value, entry.Key, adaptiveLong.Value, index, totalIndex, dehydrator);
					}
					
					index++;
					totalIndex++;
				}

				index = 0;
				totalIndex = 0;
				
				foreach(KeyValuePair<long, T> entry in orderedAccounts) {
					adaptiveLong.Value = accountCalculator.CalculateOffset(entry.Key);
					adaptiveLong.Dehydrate(dehydrator);

					if(parameters?.DehydrateExtraData != null) {
						parameters?.DehydrateExtraData(entry.Value, entry.Key, adaptiveLong.Value, index, totalIndex, dehydrator);
					}

					accountCalculator.AddLastOffset();
					index++;
					totalIndex++;
				}
			}
		}

		public static List<AccountId> Rehydrate(IDataRehydrator rehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = null) {

			SequentialLongOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			List<AccountId> accountIds = new List<AccountId>();

			adaptiveLong.Rehydrate(rehydrator);
			int groupsCount = (int) adaptiveLong.Value;

			if(parameters?.Initialize != null) {
				parameters?.Initialize(groupsCount);
			}

			int totalIndex = 0;
			if(groupsCount != 0) {

				if(parameters?.DataPrerun != null) {
					parameters?.DataPrerun();
				}
				
				for(int groupIndex = 0; groupIndex < groupsCount; groupIndex++) {

					adaptiveLong.Rehydrate(rehydrator);
					int groupCount = (int) adaptiveLong.Value;
					Enums.AccountTypes accountType = (Enums.AccountTypes) rehydrator.ReadByte();

					if(parameters?.InitializeGroup != null) {
						parameters?.InitializeGroup(groupIndex, groupCount, accountType);
					}

					accountCalculator.Reset();

					if(groupCount != 0) {

						accountCalculator.Reset();

						for(int i = 0; i < groupCount; i++) {

							adaptiveLong.Rehydrate(rehydrator);
							AccountId accountId = new AccountId(accountCalculator.RebuildValue(adaptiveLong.Value), accountType);

							accountIds.Add(accountId);

							if(parameters?.RehydrateExtraData != null) {
								parameters?.RehydrateExtraData(accountId, adaptiveLong.Value, i, totalIndex, rehydrator);
							}

							accountCalculator.AddLastOffset();
							totalIndex += 1;
						}
					}

					if(parameters?.FinalizeGroup != null) {
						parameters?.FinalizeGroup(groupIndex);
					}
				}
			}

			return accountIds;
		}

		public static List<long> RehydrateLong(IDataRehydrator rehydrator, bool onlyPublicAccounts, AccountIdGroupSerializerRehydrateParameters<long> parameters = null) {

			SequentialLongOffsetCalculator accountCalculator = GetOffsetCalculator(onlyPublicAccounts);
			List<long> accountIds = new List<long>();

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Rehydrate(rehydrator);
			int count = (int) adaptiveLong.Value;

			int totalIndex = 0;
			if(parameters?.Initialize != null) {
				parameters?.Initialize(count);
			}

			if(count != 0) {
				if(parameters?.DataPrerun != null) {
					parameters?.DataPrerun();
				}

				for(int i = 0; i < count; i++) {

					adaptiveLong.Rehydrate(rehydrator);
					long accountId = accountCalculator.CalculateOffset(adaptiveLong.Value);

					accountIds.Add(accountId);

					if(parameters?.RehydrateExtraData != null) {
						parameters?.RehydrateExtraData(accountId, adaptiveLong.Value, i, totalIndex, rehydrator);
					}

					accountCalculator.AddLastOffset();
					totalIndex += 1;
				}
			}

			return accountIds;
		}

		public class AccountIdGroupSerializerDehydrateParameters<T, K> {
			/// <summary>
			///     content entry, account Id, offset of accountID, entry index
			/// </summary>
			public Action<T, K, long, int, int, IDataDehydrator> DehydrateExtraData;
			
			/// <summary>
			/// in case we need to run something in the same order before we actually serialize the data
			/// </summary>
			public Action<T, K, long, int, int, IDataDehydrator> DataPrerun;

			/// <summary>
			/// called when the prerun has been executed
			/// </summary>
			public Action DataPrerunExecuted;
			
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
			public Action<K, long, int, int, IDataRehydrator> RehydrateExtraData;
			
			/// <summary>
			/// called when the prerun has been executed
			/// </summary>
			public Action DataPrerun;
		}
	}
}