namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers.WalletProviderComponents {

	public interface IWalletProviderAPIComponentUtility {

	}

	public interface IWalletProviderAPIComponentReadonly {

	}

	public interface IWalletProviderAPIComponentWrite {
		
	}
	
	public interface IWalletProviderAPIComponent :  IWalletProviderAPIComponentReadonly, IWalletProviderAPIComponentWrite, IWalletProviderAPIComponentUtility {

	}
	
	public abstract class WalletProviderAPIComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER> : WalletProviderComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER>,  	IWalletProviderComponent, IWalletProviderAPIComponent
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where WALLET_PROVIDER : IWalletProviderInternal{

		
	}
}