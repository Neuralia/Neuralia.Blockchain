using System;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Cryptography.Passphrases {

	/// <summary>
	///     A utility class to hold memory passphrases and other parameters about them if applicable
	/// </summary>
	public abstract class PassphraseDetails : IDisposable2 {

		protected readonly int? keyPassphraseTimeout;

		public PassphraseDetails(int? keyPassphraseTimeout) {
			// store the explicit intent, no matter what keys we have
			this.keyPassphraseTimeout = keyPassphraseTimeout;
		}

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private string GenerateKeyScopedName(Guid identityUuid, string keyname) {
			return $"{identityUuid.ToString()}-{keyname}";
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {
					this.DisposeAll();
				} finally {
					this.IsDisposed = true;
				}
			}
		}

		protected virtual void DisposeAll() {

		}

		~PassphraseDetails() {
			this.Dispose(false);
		}
	}
}