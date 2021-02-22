using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public static class MiningTierUtils {

		public const Enums.MiningTiers DefaultTier = Enums.MiningTiers.ThirdTier;
		
		public const byte MininingTierCount = 4;

		public static Enums.MiningTiers Convert(int value) {
			if(value > MininingTierCount) {
				throw new ArgumentException("Invalid mining tier", nameof(value));
			}

			return (Enums.MiningTiers) value;
		}

		public static bool HasTier(IList<Enums.MiningTiers> set, Enums.MiningTiers miningTier) {
			return set.Contains(miningTier);
		}

		public static bool HasTier<T>(Dictionary<Enums.MiningTiers, T> set, Enums.MiningTiers miningTier) {
			return set.ContainsKey(miningTier);
		}

		public static bool IsBaseTier(Enums.MiningTiers miningTier) {
			return IsFirstOrSecondTier(miningTier) || IsThirdTier(miningTier);
		}

		public static bool IsSpecialTier(Enums.MiningTiers miningTier) {
			return !IsBaseTier(miningTier);
		}

		public static bool IsFirstOrSecondTier(Enums.MiningTiers miningTier) {
			return IsFirstTier(miningTier) || IsSecondTier(miningTier);
		}

		public static bool IsServerTier(Enums.MiningTiers miningTier) {
			return IsFirstOrSecondTier(miningTier);
		}
		
		public static bool IsUserTier(Enums.MiningTiers miningTier) {
			return !IsServerTier(miningTier);
		}
		
		public static bool IsFourthTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.FourthTier;
		}

		public static bool IsThirdTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.ThirdTier;
		}

		public static bool IsSecondTier(Enums.MiningTiers miningTier) {
			return IsFirstTier(miningTier) || (miningTier == Enums.MiningTiers.SecondTier);
		}

		public static bool IsFirstTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.FirstTier;
		}
		
		/// <summary>
		/// ensure that the basic conditions are right for mining
		/// </summary>
		/// <param name="miningTier"></param>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public static bool IsMiningTierValid(Enums.MiningTiers miningTier, AccountId accountId) {

			if(accountId == null) {
				return false;
			}
			
			// only these 2 types of accounts can mine
			if(!(accountId.IsUser || accountId.IsServer)) {
				return false;
			}
			
			// if in 1st or 2nd tier, the account must be of type server or we ignore it
			if(IsFirstOrSecondTier(miningTier) && !accountId.IsServer) {
				return false;
			}

			return true;
		}


		public static List<Enums.MiningTiers> GetAllMiningTiers(int total = MininingTierCount) {
			return Enumerable.Range(1, total).Select(e => (Enums.MiningTiers) e).ToList();
		}

		public static Dictionary<Enums.MiningTiers, T> FillMiningTierSet<T>(T defaultValue) {

			return FillMiningTierSet(MininingTierCount, defaultValue);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, T defaultValue) {

			FillMiningTierSet(set, MininingTierCount, defaultValue);
		}

		public static Dictionary<Enums.MiningTiers, T> FillMiningTierSet<T>(int miningTierTotal, T defaultValue) {
			Dictionary<Enums.MiningTiers, T> set = new Dictionary<Enums.MiningTiers, T>();

			FillMiningTierSet(set, miningTierTotal, defaultValue);

			return set;
		}

		public static Dictionary<Enums.MiningTiers, T> FillMiningTierSet<T>(IList<Enums.MiningTiers> list, T defaultValue) {
			Dictionary<Enums.MiningTiers, T> set = new Dictionary<Enums.MiningTiers, T>();

			FillMiningTierSet(set, list, defaultValue);

			return set;
		}

		public static void SetTierValue<T>(this Dictionary<Enums.MiningTiers, T> set,Enums.MiningTiers tier, T value) {
			if(!set.ContainsKey(tier)) {
				set.Add(tier, value);
			} else {
				set[tier] = value;
			}
		}
		
		public static T GetTierValue<T>(this Dictionary<Enums.MiningTiers, T> set,Enums.MiningTiers tier) {
			if(set.ContainsKey(tier)) {
				return set[tier];
			}

			return default;
		}
		

		/// <summary>
		///     Ensure a full set
		/// </summary>
		/// <param name="set"></param>
		/// <param name="miningTierTotal"></param>
		/// <param name="defaultValue"></param>
		/// <typeparam name="T"></typeparam>
		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, int miningTierTotal, T defaultValue, bool reset = true) {

			FillMiningTierSet(set, GetAllMiningTiers(miningTierTotal), defaultValue, reset);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, IList<Enums.MiningTiers> list, T defaultValue, bool reset = true) {

			FillMiningTierSet(set, list, t => defaultValue, reset);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, int miningTierTotal, Func<Enums.MiningTiers, T> defaultValueCreator, bool reset = true) {
			FillMiningTierSet(set, GetAllMiningTiers(miningTierTotal), defaultValueCreator, reset);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, IList<Enums.MiningTiers> list, Func<Enums.MiningTiers, T> defaultValueCreator, bool reset = true) {

			if(reset) {
				set.Clear();
			}

			foreach(Enums.MiningTiers tier in list) {

				if(!set.ContainsKey(tier)) {
					set.Add(tier, defaultValueCreator(tier));
				}
			}
		}

		public static void RehydrateUShortMiningSet(Dictionary<Enums.MiningTiers, ushort> set, IDataRehydrator rehydrator, ushort defaultValue = 0) {
			RehydrateMiningSet<ushort, AdaptiveShort1_2>(set, rehydrator, (a,b) => (ushort)(a+b), (a,b) => (ushort)(a-b), defaultValue);
		}
		
		public static void RehydrateLongMiningSet(Dictionary<Enums.MiningTiers, long> set, IDataRehydrator rehydrator, long defaultValue = 0) {
			RehydrateMiningSet<long, AdaptiveLong1_9>(set, rehydrator, (a,b) => a+b, (a,b) => a-b, defaultValue);
		}
		
		public static void RehydrateDecimalMiningSet(Dictionary<Enums.MiningTiers, decimal> set, IDataRehydrator rehydrator, decimal defaultValue = 0) {
			RehydrateMiningSet<decimal, Amount>(set, rehydrator, (a,b) => a+b, (a,b) => a-b, defaultValue);
		}

		public static void RehydrateMiningSet<T, R>(Dictionary<Enums.MiningTiers, T> set, IDataRehydrator rehydrator, Func<T, T, T> add, Func<T, T, T> subtract, T defaultValue = default)
			where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
			where R : IBinarySerializable, IValue<T>, new() {

			RehydrateTierList(set, defaultValue, rehydrator);

			DualByte dualByte = new DualByte();

			dualByte.Rehydrate(rehydrator);

			int activeTierCount = dualByte.High;
			int amountCounts = dualByte.Low + 1;

			if(activeTierCount != 0) {

				RepeatableOffsetCalculator<T> accountIdsCalculator = new RepeatableOffsetCalculator<T>(add, subtract);

				R serializationTool = new R();
				List<T> amounts = new List<T>();

				for(int i = 0; i < amountCounts; i++) {

					serializationTool.Rehydrate(rehydrator);
					T offset = accountIdsCalculator.RebuildValue(serializationTool.Value);

					amounts.Add(offset);

					accountIdsCalculator.AddLastOffset();
				}

				SafeArrayHandle data = (SafeArrayHandle)rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, activeTierCount));
				using SpecialIntegerSizeArray tiersetTierType = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, data, activeTierCount);

				List<int> indices = new List<int>();

				if(amountCounts > 1) {
					SafeArrayHandle data2 = (SafeArrayHandle)rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, activeTierCount));
					using SpecialIntegerSizeArray tiersetOffsets = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, data2, activeTierCount);

					for(int i = 0; i < activeTierCount; i++) {
						indices.Add((int) tiersetOffsets[i]);
					}
				} else {
					// just use the default index
					indices.AddRange(Enumerable.Repeat(0, activeTierCount));
				}

				for(int i = 0; i < activeTierCount; i++) {
					set[(Enums.MiningTiers) tiersetTierType[i]] = amounts[indices[i]];
				}
			}
		}

		public static ImmutableList<Enums.MiningTiers> FlagsToTiers(ushort flags) {

			List<Enums.MiningTiers> miningTiers = new List<Enums.MiningTiers>();

			for(int i = 0; i <= 0xF; i++) {
				if((flags & (ushort) (1 << i)) != 0) {
					if(Enum.IsDefined(typeof(Enums.MiningTiers), (byte) (i + 1))) {
						miningTiers.Add((Enums.MiningTiers) (i + 1));
					}
				}
			}

			return miningTiers.ToImmutableList();
		}

		public static ushort TiersToFlags(ImmutableList<Enums.MiningTiers> miningTiers) {
			ushort flags = 0;

			foreach(Enums.MiningTiers tier in miningTiers) {
				flags |= (ushort) (1 << ((int) tier - 1));
			}

			return flags;
		}

		public static void DehydrateTierList<T>(Dictionary<Enums.MiningTiers, T> miningTiers, IDataDehydrator dehydrator) {

			DehydrateTierList(miningTiers.Keys.ToImmutableList(), dehydrator);
		}

		public static void DehydrateTierList(ImmutableList<Enums.MiningTiers> miningTiers, IDataDehydrator dehydrator) {

			AdaptiveLong1_9 serializationTool = new AdaptiveLong1_9(TiersToFlags(miningTiers));
			serializationTool.Dehydrate(dehydrator);
		}

		public static Dictionary<Enums.MiningTiers, T> RehydrateTierList<T>(T defaultValue, IDataRehydrator rehydrator) {

			return RehydrateTierList(rehydrator).ToDictionary(t => t, t => defaultValue);
		}

		public static void RehydrateTierList<T>(Dictionary<Enums.MiningTiers, T> set, T defaultValue, IDataRehydrator rehydrator) {

			Dictionary<Enums.MiningTiers, T> result = RehydrateTierList(defaultValue, rehydrator);

			set.Clear();

			foreach(KeyValuePair<Enums.MiningTiers, T> entry in result) {
				set.Add(entry.Key, entry.Value);
			}
		}

		public static ImmutableList<Enums.MiningTiers> RehydrateTierList(IDataRehydrator rehydrator) {

			AdaptiveLong1_9 serializationTool = new AdaptiveLong1_9();
			serializationTool.Rehydrate(rehydrator);

			return FlagsToTiers((ushort) serializationTool.Value);
		}

		public static IEnumerable<Enums.MiningTiers> Order(this IEnumerable<Enums.MiningTiers> miningTiers) {

			return miningTiers.OrderBy(t => (int) t);
		}

		public static IEnumerable<KeyValuePair<Enums.MiningTiers, T>> Order<T>(this Dictionary<Enums.MiningTiers, T> miningTiers) {

			return miningTiers.OrderBy(t => (int) t.Key);
		}

		public static void AddStructuresArray<T>(HashNodeList hashNodeList, Dictionary<Enums.MiningTiers, T> miningTiers) {
			hashNodeList.Add(miningTiers.Count);

			foreach(KeyValuePair<Enums.MiningTiers, T> entry in miningTiers.Order()) {
				hashNodeList.Add((byte) entry.Key);
				hashNodeList.Add(entry.Value);
			}
		}

		public static string GetOrdinalName(Enums.MiningTiers miningTier) {

			if(miningTier == Enums.MiningTiers.FirstTier) {
				return "1st";
			}

			if(miningTier == Enums.MiningTiers.SecondTier) {
				return "2nd";
			}

			if(miningTier == Enums.MiningTiers.ThirdTier) {
				return "3rd";
			}

			return $"{(int) miningTier}th";
		}
	}
}