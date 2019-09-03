using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;

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

		public IByteArray WalletPassphraseBytes => (ByteArray) Encoding.UTF8.GetBytes(this.WalletPassphrase.ConvertToUnsecureString());

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

		private string GenerateKeyScopedName(Guid identityUuid, string keyname) {
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

			this.SetKeysPassphrase(identityUuid, null, passphrase.ConvertToSecureString(), timeout);
		}

		public void SetKeysPassphrase(Guid identityUuid, string keyname, SecureString passphrase, int? timeout = null) {

			if(passphrase == null) {
				throw new ApplicationException("null passphrase provided");
			}

			passphrase.MakeReadOnly();

			string scopedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				if(string.IsNullOrWhiteSpace(keyname)) {
					throw new ApplicationException("Key name is required when encrypting keys individually");
				}

				scopedName = this.GenerateKeyScopedName(identityUuid, keyname);
			}

			this.SetKeyEntry(scopedName, passphrase, timeout);

			if((scopedName == DEFAULT_NAME) && !string.IsNullOrWhiteSpace(keyname)) {
				// let's set that one too
				scopedName = this.GenerateKeyScopedName(identityUuid, keyname);

				SecureString clone = passphrase.ConvertToUnsecureString().ConvertToSecureString();

				clone.MakeReadOnly();
				this.SetKeyEntry(scopedName, clone, timeout);
			}
		}

		private void SetKeyEntry(string scopedName, SecureString passphrase, int? timeout = null) {
			var passphraseTimeout = this.keyPassphraseTimeout;

			if(timeout.HasValue) {
				passphraseTimeout = timeout.Value;
			}

			// clear previous entry
			if(this.keys.ContainsKey(scopedName)) {
				(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[scopedName];
				this.keys.Remove(scopedName);

				entry.keysPassphraseTimer?.Dispose();
				entry.keysPassphrase?.Dispose();
			}

			Timer keysPassphraseTimer = null;

			// set a timeout, if applicable
			if(passphraseTimeout.HasValue) {
				keysPassphraseTimer = new Timer(state => {

					PassphraseDetails details = (PassphraseDetails) state;

					if(this.keys.ContainsKey(scopedName)) {
						(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[scopedName];

						// lets clear everything
						entry.keysPassphrase.Clear();
						entry.keysPassphrase.Dispose();
						entry.keysPassphrase = null;

						entry.keysPassphraseTimer.Dispose();
						entry.keysPassphraseTimer = null;

						this.keys.Remove(scopedName);
					}

				}, this, TimeSpan.FromMinutes(passphraseTimeout.Value), new TimeSpan(-1));
			}

			this.keys.Add(scopedName, (passphrase, keysPassphraseTimer));
		}

		public bool KeyPassphraseValid(Guid identityUuid, string keyname) {
			if(!this.EncryptWalletKeys) {
				return true; // no encryption, so we dont care, always valid
			}

			string scopedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				scopedName = this.GenerateKeyScopedName(identityUuid, keyname);
			}

			if(this.keys.ContainsKey(scopedName)) {
				(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[scopedName];

				return !((entry.keysPassphrase == null) || (entry.keysPassphrase.Length == 0));
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
				(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[DEFAULT_NAME];

				return !((entry.keysPassphrase == null) || (entry.keysPassphrase.Length == 0));
			}

			return false;
		}

		public SecureString KeyPassphrase(Guid identityUuid, string keyname) {
			string scopedName = DEFAULT_NAME;

			if(this.EncryptWalletKeysIndividually) {
				if(string.IsNullOrWhiteSpace(keyname)) {
					throw new ApplicationException("Key name is required when encrypting keys individually");
				}

				scopedName = this.GenerateKeyScopedName(identityUuid, keyname);
			}

			if(this.keys.ContainsKey(scopedName)) {
				(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[scopedName];

				return entry.keysPassphrase;
			}

			return null;
		}

		public SecureString KeyPassphrase(Guid identityUuid) {

			if(this.EncryptWalletKeysIndividually) {
				return null;
			}

			if(this.keys.ContainsKey(DEFAULT_NAME)) {
				(SecureString keysPassphrase, Timer keysPassphraseTimer) entry = this.keys[DEFAULT_NAME];

				return entry.keysPassphrase;
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