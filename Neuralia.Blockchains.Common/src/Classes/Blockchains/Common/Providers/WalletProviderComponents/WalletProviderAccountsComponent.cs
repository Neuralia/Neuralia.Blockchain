namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers.WalletProviderComponents {


	public interface IWalletProviderAccountsComponentUtility {

	}

	public interface IWalletProviderAccountsComponentReadonly {

	}

	public interface IWalletProviderAccountsComponentWrite {
		
	}
	
	public interface IWalletProviderAccountsComponent :  IWalletProviderAccountsComponentReadonly, IWalletProviderAccountsComponentWrite, IWalletProviderAccountsComponentUtility {

	}

	public abstract class WalletProviderAccountsComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER> : WalletProviderComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER>, IWalletProviderComponent,	IWalletProviderAccountsComponent
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where WALLET_PROVIDER : IWalletProviderInternal{

		
	}
}