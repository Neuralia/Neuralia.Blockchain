using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface ITypedEntryWalletFileInfo : IWalletFileInfo {
		
	}
	
	public interface ITypedEntryWalletFileInfo<in ENTRY_TYPE> :ITypedEntryWalletFileInfo {
		
	}
	
	public abstract class TypedEntryWalletFileInfo<ENTRY_TYPE> : WalletFileInfo, ITypedEntryWalletFileInfo<ENTRY_TYPE>{
		protected TypedEntryWalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails, fileCacheTimeout) {
		}
	}
}