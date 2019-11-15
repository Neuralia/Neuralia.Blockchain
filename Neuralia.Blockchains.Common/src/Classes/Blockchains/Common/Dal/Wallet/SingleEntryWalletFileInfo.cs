using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface ISingleEntryWalletFileInfo : IWalletFileInfo {
	}

	public interface ISingleEntryWalletFileInfo<in ENTRY_TYPE> : ISingleEntryWalletFileInfo {

		void CreateEmptyFile(ENTRY_TYPE entry);
	}

	public abstract class SingleEntryWalletFileInfo<ENTRY_TYPE> : WalletFileInfo {

		protected SingleEntryWalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails, fileCacheTimeout) {
		}

		public virtual void CreateEmptyFile(ENTRY_TYPE entry) {
			this.CreateEmptyFile();

			// add an entry in the database
			this.InsertNewDbData(entry);

			this.SaveFile();
		}

		protected abstract void InsertNewDbData(ENTRY_TYPE entry);
	}
}