using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	public class EncryptorParameters : ITreeHashable {

		public enum SymetricCiphers : byte {
			AES_256 = 1,
			XCHACHA_20 = 2,
			XCHACHA_40 = 3
		}

		public byte[] salt { get; set; }
		public int iterations { get; set; }
		public int keyBitLength { get; set; }
		public SymetricCiphers cipher { get; set; } = SymetricCiphers.XCHACHA_40;

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.iterations);
			hashNodeList.Add((byte) this.cipher);
			hashNodeList.Add(this.keyBitLength);
			hashNodeList.Add(this.salt);

			return hashNodeList;
		}

		public IByteArray Dehydrate() {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			dehydrator.Write(this.iterations);
			dehydrator.Write((byte) this.cipher);
			dehydrator.Write(this.keyBitLength);
			dehydrator.WriteNonNullable(this.salt);

			return dehydrator.ToArray();
		}

		public void Rehydrate(IByteArray data) {

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.iterations = rehydrator.ReadInt();
			this.cipher = (SymetricCiphers) rehydrator.ReadByte();
			this.keyBitLength = rehydrator.ReadInt();
			IByteArray saltArray = rehydrator.ReadNonNullableArray();

			this.salt = saltArray.ToExactByteArrayCopy();
			saltArray.Return();
		}

		public static EncryptorParameters RehydrateEncryptor(IByteArray data) {

			EncryptorParameters parameters = new EncryptorParameters();

			parameters.Rehydrate(data);

			return parameters;
		}

		public static EncryptorParameters RehydrateEncryptor(IDataRehydrator rehydrator) {

			EncryptorParameters parameters = new EncryptorParameters();

			parameters.Rehydrate(rehydrator);

			return parameters;
		}

		public EncryptorParameters Clone() {
			EncryptorParameters clone = new EncryptorParameters();

			clone.iterations = this.iterations;
			clone.cipher = this.cipher;
			clone.keyBitLength = this.keyBitLength;
			clone.salt = this.salt;

			return clone;
		}
	}
}