using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletChainStateFileInfo : SingleEntryWalletFileInfo<WalletAccountChainState> {

		private WalletAccountChainState chainState;

		public WalletChainStateFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.Account = account;

		}

		public IWalletAccount Account { get; }

		public virtual async Task<WalletAccountChainState> ChainState(LockContext lockContext) {

			if(this.chainState == null) {
				this.chainState = await this.RunQueryDbOperation((litedbDal, lc) => {

					return Task.FromResult(litedbDal.GetSingle<WalletAccountChainState>());
				}, lockContext).ConfigureAwait(false);
			}

			return this.chainState;

		}

		protected override WalletAccountChainState CreateEntryType() {
			return null;

			
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override async Task InsertNewDbData(WalletAccountChainState chainState, LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				this.chainState = chainState;

				await this.RunDbOperation((litedbDal, lc) => {
					litedbDal.Insert(chainState, c => c.AccountCode);

					return Task.CompletedTask;
				}, handle).ConfigureAwait(false);

			}
		}

		public override void ClearCached(LockContext lockContext) {
			base.ClearCached(lockContext);
			
			this.chainState = null;
		}

		public override async Task Reset(LockContext lockContext) {
			await base.Reset(lockContext).ConfigureAwait(false);

			this.ClearCached(lockContext);
		}

		protected override Task CreateDbFile(LiteDBDAL litedbDal, LockContext lockContext) {
			litedbDal.CreateDbFile<WalletAccountChainState, string>(i => i.AccountCode);

			return Task.CompletedTask;
		}

		protected override Task PrepareEncryptionInfo(LockContext lockContext) {
			return this.CreateSecurityDetails(lockContext);
		}

		protected override async Task CreateSecurityDetails(LockContext lockContext) {
			using(LockHandle handle = await this.locker.LockAsync(lockContext).ConfigureAwait(false)) {
				if(this.EncryptionInfo == null) {
					this.EncryptionInfo = new EncryptionInfo();

					this.EncryptionInfo.Encrypt = this.WalletSecurityDetails.EncryptWallet;

					if(this.EncryptionInfo.Encrypt) {

						this.EncryptionInfo.EncryptionParameters = this.Account.KeyLogFileEncryptionParameters;
						this.EncryptionInfo.Secret = () => this.Account.KeyLogFileSecret;
					}
				}
			}
		}

		protected override Task UpdateDbEntry(LockContext lockContext) {
			return this.RunDbOperation((litedbDal, lc) => {
				if(litedbDal.CollectionExists<WalletAccountChainState>()) {
					litedbDal.Update(this.ChainState(lc).WaitAndUnwrapException());
				}

				return Task.CompletedTask;
			}, lockContext);

		}
	}
}