using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface ISingleEntryWalletFileInfo : IWalletFileInfo {

	}

	public interface ISingleEntryWalletFileInfo<in ENTRY_TYPE> : ISingleEntryWalletFileInfo {

		Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext);
	}

	public abstract class SingleEntryWalletFileInfo<ENTRY_TYPE> : WalletFileInfo {

		protected SingleEntryWalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails, fileCacheTimeout) {
		}

		public virtual async Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext) {
			await CreateEmptyFile(lockContext).ConfigureAwait(false);

			// add an entry in the database
			await this.InsertNewDbData(entry, lockContext).ConfigureAwait(false);

			await SaveFile(lockContext).ConfigureAwait(false);
		}

		protected abstract Task InsertNewDbData(ENTRY_TYPE entry, LockContext lockContext);
	}
}