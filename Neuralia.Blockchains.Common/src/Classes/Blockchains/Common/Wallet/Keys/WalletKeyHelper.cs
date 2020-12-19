using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public abstract class WalletKeyHelper {
		public T CreateKey<T>(IDataRehydrator rehydrator)
			where T : IWalletKey {

			ComponentVersion<CryptographicKeyType> version = rehydrator.RehydrateRewind<ComponentVersion<CryptographicKeyType>>();
			
			IChainTypeCreationFactory chainTypeCreationFactory = this.CreateChainTypeCreationFactory();

			return (T) chainTypeCreationFactory.CreateNewWalletKey(version.Type.Value);
		}

		protected abstract IChainTypeCreationFactory CreateChainTypeCreationFactory();
	}
}