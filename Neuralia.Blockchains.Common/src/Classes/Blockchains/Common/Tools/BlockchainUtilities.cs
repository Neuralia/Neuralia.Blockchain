using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
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

		public static Enums.MiningTiers GetMiningTier(BlockChainConfigurations configuration, int digestId) {

			// if they force a tier, then we attempt to use it (first and second tier are special, so we ignore the explicit set)
			if(configuration.MiningTier.HasValue && (configuration.MiningTier.Value != Enums.MiningTiers.FirstTier) && (configuration.MiningTier.Value != Enums.MiningTiers.SecondTier)) {
				return configuration.MiningTier.Value;
			}

			Enums.MiningTiers determinedMiningTier = Enums.MiningTiers.ThirdTier;

			NodeShareType nodeShareType = configuration.NodeShareType();

			// 1st tier is everything (digest included) or if only blocks, only while there is no digest.
			if(nodeShareType.HasDigestsAndBlocks || (nodeShareType.OnlyBlocks && (digestId == 0))) {
				// first tier is for full sharers only
				determinedMiningTier = Enums.MiningTiers.FirstTier;
			} else if(!nodeShareType.Shares) {
				// if they dont share anything, its third tier
				determinedMiningTier = Enums.MiningTiers.ThirdTier;
			} else {
				// anything in between is second tier
				determinedMiningTier = Enums.MiningTiers.SecondTier;
			}

			if(configuration.MiningTier.HasValue) {

				if((configuration.MiningTier.Value == Enums.MiningTiers.FirstTier) && (determinedMiningTier != Enums.MiningTiers.FirstTier)) {
					// leave whatever we already have, we can not fullfill the request
				} else {
					// ok, lets override it.
					determinedMiningTier = configuration.MiningTier.Value;
				}
			}

			if((determinedMiningTier == Enums.MiningTiers.SecondTier) && (digestId == 0)) {
				determinedMiningTier = Enums.MiningTiers.FirstTier;
			}

			return determinedMiningTier;
		}
	}
}