using System;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Cryptography.Passphrases {

	/// <summary>
	///     A utility class to hold memory passphrases and other parameters about them if applicable
	/// </summary>
	public abstract class PassphraseDetails : IDisposableExtended {

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

		private string GenerateKeyScoppedName(Guid identityUuid, string keyname) {
			return $"{identityUuid.ToString()}-{keyname}";
		}

		protected void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}
			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {

		}

		~PassphraseDetails() {
			this.Dispose(false);
		}
	}
}