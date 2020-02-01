using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public static class BlockchainUtilities {

		public static bool UsesDigests(NodeShareType nodeShareType) {
			return nodeShareType.HasDigests;
		}

		public static bool UsesAllBlocks(NodeShareType nodeShareType) {
			return nodeShareType.AllBlocks;
		}

		public static bool UsesPartialBlocks(NodeShareType nodeShareType) {
			return nodeShareType.PartialBlocks;
		}
		
		public static bool UsesBlocks(NodeShareType nodeShareType) {
			return nodeShareType.HasBlocks;
		}
		
		public static bool DoesNotShare(NodeShareType nodeShareType) {
			return nodeShareType.DoesNotShare;
		}

		public static Enums.MiningTiers GetMiningTier(ChainConfigurations configuration, int? digestId) {
			return GetMiningTier(configuration.NodeShareType(), digestId);
		}

		public static Enums.MiningTiers GetMiningTier(NodeShareType nodeShareType, int? digestId) {

			// 1st tier is everything (digest included) or if only blocks, only while there is no digest.
			if(nodeShareType.HasDigestsAndBlocks || (nodeShareType.OnlyBlocks && digestId.HasValue && digestId.Value == 0)) {
				// first tier is for full sharers only
				return Enums.MiningTiers.FirstTier;
			}
			if(!nodeShareType.Shares) {
				// if they dont share anything, its third tier
				return Enums.MiningTiers.ThirdTier;
			}
			// anything in between is second tier
			return Enums.MiningTiers.SecondTier;
		}
	}
}