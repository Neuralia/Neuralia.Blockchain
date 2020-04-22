using System;
using System.Security;
using System.Text;
using System.Threading;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.Passphrases {
	public class WalletPassphraseDetails : PassphraseDetails {

		private Timer walletPassphraseTimer;

		public WalletPassphraseDetails(bool encryptWallet, int? keyPassphraseTimeout) : base(keyPassphraseTimeout) {
			// store the explicit intent, no matter what keys we have
			this.EncryptWallet = encryptWallet;
		}

		public bool WalletPassphraseValid {
			get {
				if(!this.EncryptWallet) {
					return true; // no encryption, so we dont care, always valid
				}

				return !((this.WalletPassphrase == null) || (this.WalletPassphrase.Length == 0));
			}
		}

		public SecureString WalletPassphrase { get; private set; }

		public SafeArrayHandle WalletPassphraseBytes => this.WalletPassphrase == null ? null : ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(this.WalletPassphrase.ConvertToUnsecureString()));

		/// <summary>
		///     An explicit flag to determine if we should encrypt the wallet
		/// </summary>
		public bool EncryptWallet { get; set; }

		public void ClearWalletPassphrase() {
			if(this.WalletPassphrase != null) {
				this.WalletPassphrase.Dispose();
				this.WalletPassphrase = null;
			}
		}

		public void SetWalletPassphrase(byte[] passphrase, int? timeout = null) {

			this.SetWalletPassphrase(Encoding.UTF8.GetString(passphrase), timeout);
		}

		public void SetWalletPassphrase(string passphrase, int? timeout = null) {

			//TODO: this is not secure, debug only
			SecureString securePassphrase = new SecureString();

			foreach(char c in passphrase) {
				securePassphrase.AppendChar(c);
			}

			this.SetWalletPassphrase(securePassphrase, timeout);
		}

		/// <summary>
		///     Set the wallet passphrase. This passphrase never expires, because the wallet unlocked is mandatory for the chain to
		///     operate under an account.
		///     its basically all or nothing. we use an account or we dont. anyways, security requirements is less for this file
		///     than the keys one
		/// </summary>
		/// <param name="passphrase"></param>
		public void SetWalletPassphrase(SecureString passphrase, int? timeout = null) {

			if(passphrase == null) {
				throw new ApplicationException("null passphrase provided");
			}

			passphrase.MakeReadOnly();
			this.WalletPassphrase = passphrase;

			var passphraseTimeout = this.keyPassphraseTimeout;

			if(timeout.HasValue) {
				passphraseTimeout = timeout.Value;
			}

			// set a timeout, if applicable
			if(passphraseTimeout.HasValue) {
				this.walletPassphraseTimer = new Timer(state => {

					try{
						// lets clear everything
						this.ClearWalletPassphrase();

						this.walletPassphraseTimer.Dispose();
						this.walletPassphraseTimer = null;
					}
					catch(Exception ex){
						//TODO: do something?
						Log.Error(ex, "Timer exception");
					}

				}, this, TimeSpan.FromMinutes(passphraseTimeout.Value), new TimeSpan(-1));
			}
		}

		private string GenerateKeyScoppedName(Guid identityUuid, string keyname) {
			return $"{identityUuid.ToString()}-{keyname}";
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.ClearWalletPassphrase();
		}
	}
}