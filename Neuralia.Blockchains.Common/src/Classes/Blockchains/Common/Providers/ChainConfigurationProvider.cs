using Neuralia.Blockchains.Common.Classes.Configuration;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainConfigurationProvider : IChainProvider{
		BlockChainConfigurations ChainConfiguration { get; }

		BlockChainConfigurations GetChainConfiguration();
	}

	/// <summary>
	///     A provider that offers the chain state parameters from the DB
	/// </summary>
	/// <typeparam name="CHAIN_STATE_DAL"></typeparam>
	/// <typeparam name="CHAIN_STATE_CONTEXT"></typeparam>
	/// <typeparam name="CHAIN_STATE_ENTRY"></typeparam>
	public abstract class ChainConfigurationProvider : ChainProvider, IChainConfigurationProvider {

		public BlockChainConfigurations ChainConfiguration => this.GetChainConfiguration();
		public abstract BlockChainConfigurations GetChainConfiguration();
	}
}