using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public abstract class WalletKeyHelper {
		public T CreateKey<T>(IDataRehydrator rehydrator)
			where T : IWalletKey {

			Enums.KeyTypes keyType = Enums.KeyTypes.Unknown;

			rehydrator.RehydrateRewind(rh => {
				keyType = (Enums.KeyTypes) rh.ReadByte();
			});

			IChainTypeCreationFactory chainTypeCreationFactory = this.CreateChainTypeCreationFactory();

			return (T) chainTypeCreationFactory.CreateNewWalletKey(keyType);
		}

		protected abstract IChainTypeCreationFactory CreateChainTypeCreationFactory();
	}
}