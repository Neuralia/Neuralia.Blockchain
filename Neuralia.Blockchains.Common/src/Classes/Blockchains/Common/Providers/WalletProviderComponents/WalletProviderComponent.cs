namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers.WalletProviderComponents {

	public interface IWalletProviderComponent {
	
	}
	
	public interface IWalletProviderComponentInternal<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER> 
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where WALLET_PROVIDER : IWalletProvider{
		
		void Initialize(WALLET_PROVIDER walletProvider, CENTRAL_COORDINATOR centralCoordinator);
	}
	
	public abstract class WalletProviderComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER> : IWalletProviderComponent, IWalletProviderComponentInternal<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where WALLET_PROVIDER : IWalletProviderInternal{

		protected WALLET_PROVIDER MainWalletProvider { get; private set; }
		protected CENTRAL_COORDINATOR CentralCoordinator { get; private set; }
		public void Initialize(WALLET_PROVIDER walletProvider, CENTRAL_COORDINATOR centralCoordinator) {

			this.MainWalletProvider = walletProvider;
			this.CentralCoordinator = centralCoordinator;
		}
	}
}