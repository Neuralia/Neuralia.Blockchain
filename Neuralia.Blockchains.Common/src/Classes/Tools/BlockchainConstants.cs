using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralium.Blockchains.Neuralium.Classes.NeuraliumChain {
	public static class BlockchainConstants {
		
		/// <summary>
		/// the latest release, change every build
		/// </summary>
		public static SoftwareVersion ReleaseVersion { get; } = new SoftwareVersion(1,0,1,12, "MAINNET RELEASE");
		
		/// <summary>
		/// the blockchain compatibility version for backwards compatibility. change rarely!
		/// </summary>
		public static SoftwareVersion BlockchainCompatibilityVersion { get; } = new SoftwareVersion(1,0,1,0, "MAINNET RELEASE");

		
		/// <summary>
		///     this is where we decide which versions are acceptable to us
		/// </summary>
		/// <param name="localVersion"></param>
		/// <param name="other"></param>
		/// <returns></returns>
		public static bool VersionValidationCallback(SoftwareVersion localVersion, SoftwareVersion other) {
			SoftwareVersion minimumAcceptable = new SoftwareVersion(BlockchainConstants.BlockchainCompatibilityVersion);

			return (other <= localVersion) && (other >= minimumAcceptable);
		}
	}
}