using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public static class BlockchainUtilities {

		public static bool UsesDigests(AppSettingsBase.BlockSavingModes blockSavingModes) {
			return (blockSavingModes == AppSettingsBase.BlockSavingModes.DigestAndBlocks) || (blockSavingModes == AppSettingsBase.BlockSavingModes.DigestsThenBlocks);
		}

		public static bool UsesAllBlocks(AppSettingsBase.BlockSavingModes blockSavingModes) {
			return (blockSavingModes == AppSettingsBase.BlockSavingModes.BlockOnly) || (blockSavingModes == AppSettingsBase.BlockSavingModes.DigestAndBlocks);
		}

		public static bool UsesPartialBlocks(AppSettingsBase.BlockSavingModes blockSavingModes) {
			return blockSavingModes == AppSettingsBase.BlockSavingModes.DigestsThenBlocks;
		}

		/// <summary>
		///     Do we even use blocks at all?
		/// </summary>
		/// <param name="blockSavingModes"></param>
		/// <returns></returns>
		public static bool UsesBlocks(AppSettingsBase.BlockSavingModes blockSavingModes) {
			return UsesAllBlocks(blockSavingModes) || UsesPartialBlocks(blockSavingModes);
		}
	}
}