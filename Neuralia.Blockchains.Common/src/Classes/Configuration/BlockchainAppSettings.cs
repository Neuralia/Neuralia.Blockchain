using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Common.Classes.Configuration {

	public interface IBlockchainAppSettings : IAppSettingsBase {
	}

	public abstract class BlockchainAppSettings : AppSettingsBase, IBlockchainAppSettings {
	}
}