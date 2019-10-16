using LiteDB;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet {
	public class KeyInfo {

		public byte Ordinal { get; set; }

		public string Name { get; set; }

		/// <summary>
		///     are the key files encrypted?
		/// </summary>
		/// <returns></returns>
		public IEncryptorParameters EncryptionParameters { get; set; }

		[BsonIgnore]
		public bool KeyEncrypted => this.EncryptionParameters != null;
	}
}