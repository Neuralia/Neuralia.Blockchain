using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {

	public interface ISingleEntryWalletFileInfo : ITypedEntryWalletFileInfo {
	}

	public interface ISingleEntryWalletFileInfo<in ENTRY_TYPE> :  ITypedEntryWalletFileInfo<ENTRY_TYPE>, ISingleEntryWalletFileInfo {

		Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext);
	}

	public abstract class SingleEntryWalletFileInfo<ENTRY_TYPE> : TypedEntryWalletFileInfo<ENTRY_TYPE> {

		protected SingleEntryWalletFileInfo(string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails, int? fileCacheTimeout = null) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails, fileCacheTimeout) {
		}

		protected abstract Task UpdateDbEntry(LockContext lockContext);

		protected abstract ENTRY_TYPE CreateEntryType();

		public override Task CreateEmptyFile(LockContext lockContext, object data = null) {
			
			return this.CreateEmptyFile(this.CreateEntryType(), lockContext);
		}

		public virtual async Task CreateEmptyFile(ENTRY_TYPE entry, LockContext lockContext) {
			await base.CreateEmptyFile(lockContext).ConfigureAwait(false);

			// add an entry in the database
			if(entry != null && !entry.Equals(default)) {
				await this.InsertNewDbData(entry, lockContext).ConfigureAwait(false);
			}

			await this.SaveFile(lockContext).ConfigureAwait(false);
		}

		protected abstract Task InsertNewDbData(ENTRY_TYPE entry, LockContext lockContext);
		
		public override async Task Save(LockContext lockContext, object data = null) {

			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {

				await this.LazyLoad(handle, data).ConfigureAwait(false);

				await this.UpdateDbEntry(handle).ConfigureAwait(false);

				await this.SaveFile(handle, false, data).ConfigureAwait(false);
			}
		}
	}
}