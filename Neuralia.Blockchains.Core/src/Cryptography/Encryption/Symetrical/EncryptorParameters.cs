using System;
using LiteDB;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {

	public interface IEncryptorParameters : ITreeHashable, IDisposableExtended {
		int Iterations { get; set; }
		int KeyBitLength { get; set; }

		/// <summary>
		/// </summary>
		/// <remarks>is serialized by LiteDB, needs to be get;set;</remarks>
		SafeArrayHandle Salt { get; set; }

		EncryptorParameters.SymetricCiphers Cipher { get; set; }

		SafeArrayHandle Dehydrate();
		void Dehydrate(IDataDehydrator dehydrator);
		void Rehydrate(SafeArrayHandle data);
		void Rehydrate(IDataRehydrator rehydrator);
		IEncryptorParameters Clone();
	}

	/// <summary>
	/// </summary>
	/// <remarks>is serialized by LiteDB</remarks>
	public abstract class EncryptorParameters : IEncryptorParameters {

		public enum SymetricCiphers : byte {

			AES_256 = 1,
			AES_GCM_256 = 2,

			XCHACHA_20_POLY_1305 =11,
			XCHACHA_30_POLY_1305 = 12,
			XCHACHA_40_POLY_1305 = 13
		}

		public int Iterations { get; set; }
		public int KeyBitLength { get; set; }

		/// <summary>
		/// </summary>
		/// <remarks>is serialized by LiteDB, needs to be get;set;</remarks>
		public SafeArrayHandle Salt { get; set; } = new SafeArrayHandle();

		public SymetricCiphers Cipher { get; set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add((byte) this.Cipher);
			hashNodeList.Add(this.Iterations);
			hashNodeList.Add(this.KeyBitLength);
			hashNodeList.Add(this.Salt.Clone());

			return hashNodeList;
		}

		public SafeArrayHandle Dehydrate() {

			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {

				this.Dehydrate(dehydrator);

				return dehydrator.ToArray();
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write((byte) this.Cipher);
			dehydrator.Write(this.Iterations);
			dehydrator.Write(this.KeyBitLength);
			dehydrator.WriteNonNullable(this.Salt);
		}

		public virtual void Rehydrate(SafeArrayHandle data) {

			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				this.Rehydrate(rehydrator);
			}
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.Cipher = (SymetricCiphers) rehydrator.ReadByte();
			this.Iterations = rehydrator.ReadInt();
			this.KeyBitLength = rehydrator.ReadInt();
			this.Salt.Entry = rehydrator.ReadNonNullableArray();
		}

		public virtual IEncryptorParameters Clone() {
			IEncryptorParameters clone = this.CreateEncryptorParameter();

			clone.Iterations = this.Iterations;
			clone.Cipher = this.Cipher;
			clone.KeyBitLength = this.KeyBitLength;
			clone.Salt.Entry = this.Salt.Entry.Clone();

			return clone;
		}

		public static IEncryptorParameters RehydrateEncryptor(SafeArrayHandle data) {

			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				return RehydrateEncryptor(rehydrator);
			}
		}

		public static IEncryptorParameters RehydrateEncryptor(IDataRehydrator rehydrator) {

			SymetricCiphers cipher = SymetricCiphers.AES_256;

			rehydrator.RehydrateRewind(dr => {
				cipher = (SymetricCiphers) dr.ReadByte();
			});

			IEncryptorParameters parameters = null;

			if(cipher == SymetricCiphers.AES_256) {
				parameters = AESFileEncryptor.GenerateEncryptionParameters();
			} else if(cipher == SymetricCiphers.AES_GCM_256) {
				parameters = AESGCMFileEncryptor.GenerateEncryptionParameters();
			} else if(cipher == SymetricCiphers.XCHACHA_20_POLY_1305 || cipher == SymetricCiphers.XCHACHA_30_POLY_1305 || cipher == SymetricCiphers.XCHACHA_40_POLY_1305) {
				parameters = ChaCha20Poly1305FileEncryptor.GenerateEncryptionParameters(cipher);
			} else {
				throw new ArgumentException();
			}

			parameters.Rehydrate(rehydrator);

			return parameters;
		}
		
		protected abstract IEncryptorParameters CreateEncryptorParameter();
		
	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		~EncryptorParameters() {
			this.Dispose(false);
		}

		protected virtual void DisposeAll() {
			
			this.Salt?.SafeDispose();
			this.Iterations = 0;
		}

	#endregion
	}
	
	public class ChaCha20Poly1305EncryptorParameters : EncryptorParameters {
		
		public ChaCha20Poly1305EncryptorParameters() {
			this.Cipher = SymetricCiphers.XCHACHA_20_POLY_1305;
		}
		
		public ChaCha20Poly1305EncryptorParameters(SymetricCiphers cypher) {

			this.Cipher = cypher;
		}

		public void Init(ByteArray salt) {
			this.Nonce = SafeArrayHandle.Create(XChaCha.NONCE_SIZE_IN_BYTES);
			this.Nonce.FillSafeRandom();
			this.Salt.Entry = salt;
		}

		public SafeArrayHandle Nonce { get; set; }
		
		[BsonIgnore]
		public int Rounds {
			get {
				switch(this.Cipher) {
					case SymetricCiphers.XCHACHA_20_POLY_1305:
						return 20;
					case SymetricCiphers.XCHACHA_30_POLY_1305:
						return 30;
					case SymetricCiphers.XCHACHA_40_POLY_1305:
						return 40;
				}

				return 20;
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Nonce);
			
			return hashNodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.WriteRawArray(this.Nonce);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.Nonce = (SafeArrayHandle)rehydrator.ReadArray(XChaCha.NONCE_SIZE_IN_BYTES);
		}

		public override IEncryptorParameters Clone() {
			ChaCha20Poly1305EncryptorParameters clone = (ChaCha20Poly1305EncryptorParameters) base.Clone();

			clone.Nonce = this.Nonce.Clone();
			
			return clone;
		}
		
		protected override IEncryptorParameters CreateEncryptorParameter() {
			return ChaCha20Poly1305FileEncryptor.GenerateEncryptionParameters(this.Cipher);
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.Nonce?.SafeDispose();
		}
	}
	
	public class AesEncryptorParameters : EncryptorParameters {

		public AesEncryptorParameters() {
			this.Cipher = SymetricCiphers.AES_256;
		}

		protected override IEncryptorParameters CreateEncryptorParameter() {
			return AESFileEncryptor.GenerateEncryptionParameters();
		}
	}

	public class AesGcmEncryptorParameters : EncryptorParameters {

		public AesGcmEncryptorParameters() {
			this.Cipher = SymetricCiphers.AES_GCM_256;
		}

		public SafeArrayHandle Nonce { get; set; } = SafeArrayHandle.Create();
		public SafeArrayHandle Tag { get; set; } = SafeArrayHandle.Create();

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Nonce);
			hashNodeList.Add(this.Tag);
			
			return hashNodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Nonce);
			dehydrator.Write(this.Tag);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.Nonce = (SafeArrayHandle)rehydrator.ReadArray();
			this.Tag = (SafeArrayHandle)rehydrator.ReadArray();
		}

		public override IEncryptorParameters Clone() {
			AesGcmEncryptorParameters clone = (AesGcmEncryptorParameters) base.Clone();

			clone.Nonce = this.Nonce.Clone();
			clone.Tag = this.Tag.Clone();

			return clone;
		}

		protected override IEncryptorParameters CreateEncryptorParameter() {
			return AESGCMFileEncryptor.GenerateEncryptionParameters();

		}
		
		protected override void DisposeAll() {
			base.DisposeAll();
			
			this.Nonce?.SafeDispose();
			this.Tag?.SafeDispose();
		}
		
	}
}