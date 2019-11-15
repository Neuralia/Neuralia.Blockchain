using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Passphrases;
using Neuralia.Blockchains.Core.DataAccess.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet {
	public class WalletChainStateFileInfo : SingleEntryWalletFileInfo<WalletAccountChainState> {

		private WalletAccountChainState chainState;

		public WalletChainStateFileInfo(IWalletAccount account, string filename, ChainConfigurations chainConfiguration, BlockchainServiceSet serviceSet, IWalletSerialisationFal serialisationFal, WalletPassphraseDetails walletSecurityDetails) : base(filename, chainConfiguration, serviceSet, serialisationFal, walletSecurityDetails) {
			this.Account = account;

		}

		public IWalletAccount Account { get; }

		public virtual WalletAccountChainState ChainState {
			get {
				if(this.chainState == null) {
					this.chainState = this.RunQueryDbOperation(litedbDal => litedbDal.GetSingle<WalletAccountChainState>());
				}

				return this.chainState;
			}
		}

		/// <summary>
		///     Insert the new empty wallet
		/// </summary>
		/// <param name="wallet"></param>
		protected override void InsertNewDbData(WalletAccountChainState chainState) {
			lock(this.locker) {
				this.chainState = chainState;

				this.RunDbOperation(litedbDal => {
					litedbDal.Insert(chainState, c => c.AccountUuid);
				});
			}

		}

		public override void Reset() {
			base.Reset();

			this.chainState = null;
		}

		protected override void CreateDbFile(LiteDBDAL litedbDal) {
			litedbDal.CreateDbFile<WalletAccountChainState, Guid>(i => i.AccountUuid);
		}

		protected override void PrepareEncryptionInfo() {
			this.CreateSecurityDetails();
		}

		protected override void CreateSecurityDetails() {
			lock(this.locker) {
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

		protected override void UpdateDbEntry() {
			this.RunDbOperation(litedbDal => {
				if(litedbDal.CollectionExists<WalletAccountChainState>()) {
					litedbDal.Update(this.ChainState);
				}
			});

		}
	}
}