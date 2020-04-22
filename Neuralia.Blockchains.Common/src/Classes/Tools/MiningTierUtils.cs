using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto.Prng;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public static class MiningTierUtils {

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

		public static bool IsFourthTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.FourthTier;
		}

		public static bool IsThirdTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.ThirdTier;
		}

		public static bool IsSecondTier(Enums.MiningTiers miningTier) {
			return IsFirstTier(miningTier) || miningTier == Enums.MiningTiers.SecondTier;
		}

		public static bool IsFirstTier(Enums.MiningTiers miningTier) {
			return miningTier == Enums.MiningTiers.FirstTier;
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
			var set = new Dictionary<Enums.MiningTiers, T>();

			FillMiningTierSet(set, miningTierTotal, defaultValue);

			return set;
		}

		public static Dictionary<Enums.MiningTiers, T> FillMiningTierSet<T>(IList<Enums.MiningTiers> list, T defaultValue) {
			var set = new Dictionary<Enums.MiningTiers, T>();

			FillMiningTierSet(set, list, defaultValue);

			return set;
		}
		
		/// <summary>
		/// Ensure a full set
		/// </summary>
		/// <param name="set"></param>
		/// <param name="miningTierTotal"></param>
		/// <param name="defaultValue"></param>
		/// <typeparam name="T"></typeparam>
		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, int miningTierTotal, T defaultValue, bool reset = true) {

			FillMiningTierSet(set, GetAllMiningTiers(miningTierTotal), defaultValue, reset);
		}
		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, IList<Enums.MiningTiers> list, T defaultValue, bool reset = true) {

			FillMiningTierSet<T>(set, list, (t) => defaultValue, reset);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, int miningTierTotal, Func<Enums.MiningTiers, T> defaultValueCreator, bool reset = true) {
			FillMiningTierSet(set, GetAllMiningTiers(miningTierTotal), defaultValueCreator, reset);
		}

		public static void FillMiningTierSet<T>(Dictionary<Enums.MiningTiers, T> set, IList<Enums.MiningTiers> list, Func<Enums.MiningTiers, T> defaultValueCreator, bool reset = true) {

			if(reset) {
				set.Clear();
			}

			foreach(var tier in list) {

				if(!set.ContainsKey(tier)) {
					set.Add(tier, defaultValueCreator(tier));
				}
			}
		}

		public static void RehydrateMiningSet<T, R>(Dictionary<Enums.MiningTiers, T> set, T defaultValue, IDataRehydrator rehydrator, Func<long, T> convertValue)
			where R : IBinarySerializable, new() {

			RehydrateTierList(set, defaultValue, rehydrator);
			
			DualByte dualByte = new DualByte();

			dualByte.Rehydrate(rehydrator);
			
			int activeTierCount = dualByte.High;
			int amountCounts = dualByte.Low+1;

			if(activeTierCount != 0) {

				SequantialOffsetCalculator accountIdsCalculator = new SequantialOffsetCalculator();

				AdaptiveLong1_9 serializationTool = new AdaptiveLong1_9();
				List<long> amounts = new List<long>();

				for(int i = 0; i < amountCounts; i++) {

					serializationTool.Rehydrate(rehydrator);
					long offset = accountIdsCalculator.RebuildValue(serializationTool.Value);

					amounts.Add(offset);

					accountIdsCalculator.AddLastOffset();
				}

				using ByteArray data = rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, activeTierCount));
				using SpecialIntegerSizeArray tiersetTierType = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, data, activeTierCount);

				List<int> indices = new List<int>();

				if(amountCounts > 1) {
					using ByteArray data2 = rehydrator.ReadArray(SpecialIntegerSizeArray.GetbyteSize(SpecialIntegerSizeArray.BitSizes.B0d5, activeTierCount));
					using SpecialIntegerSizeArray tiersetOffsets = new SpecialIntegerSizeArray(SpecialIntegerSizeArray.BitSizes.B0d5, data2, activeTierCount);

					for(int i = 0; i < activeTierCount; i++) {
						indices.Add((int) tiersetOffsets[i]);
					}
				} else {
					// just use the default index
					indices.AddRange(Enumerable.Repeat(0, activeTierCount));
				}

				for(int i = 0; i < activeTierCount; i++) {
					set[(Enums.MiningTiers) tiersetTierType[i]] = convertValue(amounts[indices[i]]);
				}
			}
		}

		public static ImmutableList<Enums.MiningTiers> FlagsToTiers(ushort flags) {

			var miningTiers = new List<Enums.MiningTiers>();
			
			for(int i = 0; i <= 0xF; i++) {
				if((flags & (ushort) (1 << i)) != 0) {
					if(Enum.IsDefined(typeof(Enums.MiningTiers), (byte)(i + 1))) {
						miningTiers.Add((Enums.MiningTiers) (i + 1));
					}
				}
			}

			return miningTiers.ToImmutableList();
		}

		public static ushort TiersToFlags(ImmutableList<Enums.MiningTiers> miningTiers) {
			ushort flags = 0;

			foreach(var tier in miningTiers) {
				flags |= (ushort)(1 << (((int)tier)-1));
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

			var result = RehydrateTierList(defaultValue, rehydrator);
			
			set.Clear();

			foreach(var entry in result) {
				set.Add(entry.Key, entry.Value);
			}
		}

		public static ImmutableList<Enums.MiningTiers> RehydrateTierList(IDataRehydrator rehydrator) {
			
			AdaptiveLong1_9 serializationTool = new AdaptiveLong1_9();
			serializationTool.Rehydrate(rehydrator);
			return FlagsToTiers((ushort)serializationTool.Value);
		}

		public static IEnumerable<Enums.MiningTiers> Order(this IEnumerable<Enums.MiningTiers> miningTiers) {

			return miningTiers.OrderBy(t => (int) t);
		}
		
		public static IEnumerable<KeyValuePair<Enums.MiningTiers, T>> Order<T>(this Dictionary<Enums.MiningTiers, T> miningTiers) {

			return miningTiers.OrderBy(t => (int) t.Key);
		}

		public static void AddStructuresArray<T>(HashNodeList hashNodeList, Dictionary<Enums.MiningTiers, T> miningTiers) {
			hashNodeList.Add(miningTiers.Count);

			foreach(var entry in miningTiers.Order()) {
				hashNodeList.Add((byte)entry.Key);
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

			return $"{(int)miningTier}th";
		}
	}
}