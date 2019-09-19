using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Cryptography.SHA3 {
	public abstract class SHA3 : HashAlgorithm, IDisposable2 {

		public bool IsDisposed { get; protected set; }

		protected override void HashCore(byte[] array, int ibStart, int cbSize) {
			if(array == null) {
				throw new ArgumentNullException("array");
			}

			if(ibStart < 0) {
				throw new ArgumentOutOfRangeException("ibStart");
			}

			if(cbSize > array.Length) {
				throw new ArgumentOutOfRangeException("cbSize");
			}

			if((ibStart + cbSize) > array.Length) {
				throw new ArgumentOutOfRangeException("ibStart or cbSize");
			}
		}

	#region Statics

		public static new SHA3 Create() {
			return Create("SHA3-256");
		}

		public bool UseKeccakPadding { get; set; }

		public static new SHA3 Create(string hashName) {
			switch(hashName.ToLower()) {

				case "sha3-256":
				case "sha3256":

					return new SHA3256Managed();

				case "sha3-512":
				case "sha3512":

					return new SHA3512Managed();

				default:

					return null;
			}
		}

	#endregion

	}
}