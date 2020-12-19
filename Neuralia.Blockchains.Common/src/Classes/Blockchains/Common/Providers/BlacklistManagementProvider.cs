//using System;
//using System.IO;
//using Neuralia.Blockchains.Common.Classes.BlockChains.Common.Providers;
//using Neuralia.Blockchains.Common.Classes.BlockChains.Common.Wallet;
//
//namespace Neuralia.Blockchains.Common.Classes.BlockChains.Common.Managers {
//	public abstract class BlacklistManagementProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
//		where WALLET_IDENTITY : IWalletIdentity<WALLET_KEY>
//		where WALLET_KEY_HISTORY : IWalletKeyHistory
//		where WALLET_KEY : IWalletKey
//		where USERWALLET : IUserWallet<WALLET_IDENTITY, WALLET_KEY, WALLET_KEY_HISTORY>
//		where WALLET_MANAGER : IWalletManager<WALLET_PROVIDER,USERWALLET, WALLET_IDENTITY, WALLET_KEY, WALLET_KEY_HISTORY>
//		<USERWALLET, WALLET_IDENTITY, WALLET_KEY, WALLET_KEY_HISTORY> {
//
//		readonly private bool            loaded = false;
//		readonly private WALLET_PROVIDER walletProvider;
//		private          string          userDbPath = "";
//
//		public BlacklistManagementProvider(WALLET_PROVIDER walletProvider) {
//			this.walletProvider = walletProvider;
//		}
//
//		private void EnsureCreated() {
//			if(!this.loaded && (this.walletProvider != null) && this.walletProvider.IsWalletLoaded) {
//				this.userDbPath = Path.Combine(this.walletProvider.GetChainWalletDirectoryPath(), path2: "blacklist.neuralia");
//
//				// ensure created
//				if(!File.Exists(this.userDbPath)) {
//					//this.GetDBDAL().CreateDbFile<UserInfo, Guid>(u => u.uuid);
//				}
//			}
//		}
//
////		public bool UserDefined(Guid UUDI) {
////			this.EnsureCreated();
////
////			return this.GetDBDAL().Exists<UserInfo, bool>(u => u.uuid == UUDI);
////		}
//
//		public class UserInfo {
//			public Guid uuid { get; set; }
//		}
//	}
//}

