using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.Passphrases {
	public class AccountPassphraseDetails : PassphraseDetails {

		private const string DEFAULT_NAME = "$%$DEFAULT%$%";

		private readonly Dictionary<string, (SecureString keysPassphrase, Timer keysPassphraseTimer)> keys = new Dictionary<string, (SecureString keysPassphrase, Timer keysPassphraseTimer)>();

		public AccountPassphraseDetails(bool encryptWalletKeys, bool encryptWalletKeysIndividually, int? keyPassphraseTimeout) : base(keyPassphraseTimeout) {
			// store the explicit intent, no matter what keys we have
			this.EncryptWalletKeys = encryptWalletKeys;
			this.EncryptWalletKeysIndividually = encryptWalletKeysIndividually;
		}

		public SecureString WalletPassphrase { get; private set; }

		public SafeArrayHandle WalletPassphraseBytes => ByteArray.WrapAndOwn(Encoding.UTF8.GetBytes(this.WalletPassphrase.ConvertToUnsecureString()));

		public bool HasKeysPassphrases => this.keys.Count != 0;

		/// <summary>
		///     An explicit flag to determine if we should encrypt the wallet keys
		/// </summary>
		public bool EncryptWalletKeys { get; set; }

		/// <summary>
		///     An explicit flag to determine if we should encrypt the wallet keys with a separate passphrase
		/// </summary>
		public bool EncryptWalletKeysIndividually { get; set; }

		public void ClearWalletPassphrase() {
			if(this.WalletPassphrase != null) {
				this.WalletPassphrase.Dispose();
				this.WalletPassphrase = null;
			}
		}

		private string GenerateKeyScoppedName(Guid identityUuid, string keyname) {
			return $"{identityUuid.ToString()}-{keyname}";
		}

		public void SetKeysPassphrase(Guid identityUuid, string keyname, byte[] passphrase, int? timeout = null) {

			this.SetKeysPassphrase(identityUuid, keyname, Encoding.UTF8.GetString(passphrase), timeout);
		}

		public void SetKeysPassphrase(Guid identityUuid, string keyname, string passphrase, int? timeout = null) {

			this.SetKeysPassphrase(identityUuid, keyname, passphrase.ConvertToSecureString(), timeout);
		}

		public void SetKeysPassphrase(Guid identityUuid, byte[] passphrase, int? timeout = null) {

			this.SetKeysPassphrase(identityUuid, null, Encoding.UTF8.GetString(passphrase), timeout);
		}

		public void SetKeysPassphrase(Guid identityUuid, string passphrase, int? timeout = null) {

			this.SetKeysPassphrase(identityUuid, passphrase.ConvertToSecureString(), timeout);
		}
		
		public void SetKeysPassphrase(Guid identityUuid, SecureString passphrase, int? timeout = null) {

			this.SetKeysPassphrase(identityUuid, null, passphrase, timeout);
		}

		public void SetKeysPassphrase(Guid identityUuid, string keyname, SecureString passphrase, int? timeout = null) {

			if(passphrase == null) {
				throw new ApplicationException("null passphrase provided");
			}

			passphrase.MakeReadOnly();

			string scoppedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				if(string.IsNullOrWhiteSpace(keyname)) {
					throw new ApplicationException("Key name is required when encrypting keys individually");
				}

				scoppedName = this.GenerateKeyScoppedName(identityUuid, keyname);
			}

			this.SetKeyEntry(scoppedName, passphrase, timeout);

			if((scoppedName == DEFAULT_NAME) && !string.IsNullOrWhiteSpace(keyname)) {
				// let's set that one too
				scoppedName = this.GenerateKeyScoppedName(identityUuid, keyname);

				SecureString clone = passphrase.ConvertToUnsecureString().ConvertToSecureString();

				clone.MakeReadOnly();
				this.SetKeyEntry(scoppedName, clone, timeout);
			}
		}

		private void SetKeyEntry(string scoppedName, SecureString passphrase, int? timeout = null) {
			var passphraseTimeout = this.keyPassphraseTimeout;

			if(timeout.HasValue) {
				passphraseTimeout = timeout.Value;
			}

			// clear previous entry
			if(this.keys.ContainsKey(scoppedName)) {
				(SecureString keysPassphrase, Timer timer) = this.keys[scoppedName];
				this.keys.Remove(scoppedName);

				timer?.Dispose();
				keysPassphrase?.Dispose();
			}

			Timer keysPassphraseTimer = null;

			// set a timeout, if applicable
			if(passphraseTimeout.HasValue) {
				keysPassphraseTimer = new Timer(state => {

					try{
						PassphraseDetails details = (PassphraseDetails) state;

						if (this.keys.ContainsKey(scoppedName)){
							(SecureString keysPassphrase, Timer timer) = this.keys[scoppedName];

							// lets clear everything
							keysPassphrase.Clear();
							keysPassphrase.Dispose();
							keysPassphrase = null;

							timer.Dispose();
							timer = null;

							this.keys.Remove(scoppedName);
						}
					}
					catch(Exception ex){
						//TODO: do something?
						Log.Error(ex, "Timer exception");
					}

				}, this, TimeSpan.FromMinutes(passphraseTimeout.Value), new TimeSpan(-1));
			}

			this.keys.Add(scoppedName, (passphrase, keysPassphraseTimer));
		}

		public bool KeyPassphraseValid(Guid identityUuid, string keyname) {
			if(!this.EncryptWalletKeys) {
				return true; // no encryption, so we dont care, always valid
			}

			string scoppedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				scoppedName = this.GenerateKeyScoppedName(identityUuid, keyname);
			}

			if(this.keys.ContainsKey(scoppedName)) {
				(SecureString keysPassphrase, _) = this.keys[scoppedName];

				return !((keysPassphrase == null) || (keysPassphrase.Length == 0));
			}

			return false;
		}

		public bool KeyPassphraseValid(Guid identityUuid) {
			if(!this.EncryptWalletKeys) {
				return true; // no encryption, so we dont care, always valid
			}

			if(this.EncryptWalletKeysIndividually) {
				return false;
			}

			if(this.keys.ContainsKey(DEFAULT_NAME)) {
				(SecureString keysPassphrase, _) = this.keys[DEFAULT_NAME];

				return !((keysPassphrase == null) || (keysPassphrase.Length == 0));
			}

			return false;
		}

		public SecureString KeyPassphrase(Guid identityUuid, string keyname) {
			string scoppedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				if(string.IsNullOrWhiteSpace(keyname)) {
					throw new ApplicationException("Key name is required when encrypting keys individually");
				}

				scoppedName = this.GenerateKeyScoppedName(identityUuid, keyname);
			}

			if(this.keys.ContainsKey(scoppedName)) {
				(SecureString keysPassphrase, _) = this.keys[scoppedName];

				return keysPassphrase;
			}

			return null;
		}

		public SecureString KeyPassphrase(Guid identityUuid) {

			if(this.EncryptWalletKeysIndividually) {
				return null;
			}

			if(this.keys.ContainsKey(DEFAULT_NAME)) {
				(SecureString keysPassphrase, _) = this.keys[DEFAULT_NAME];

				return keysPassphrase;
			}

			return null;
		}

		public void ClearKeysPassphrase() {
			foreach(var key in this.keys) {

				try {
					key.Value.keysPassphrase?.Dispose();
				} catch {

				}

				try {
					key.Value.keysPassphraseTimer?.Dispose();
				} catch {

				}
			}

			this.keys.Clear();
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.ClearKeysPassphrase();
		}
	}
}