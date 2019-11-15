using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical{

	public interface IEncryptorParameters : ITreeHashable{
		int Iterations { get; set; }
		int KeyBitLength { get; set; }
		ByteArray Salt { get; set; }
		EncryptorParameters.SymetricCiphers cipher { get; set; }
		SafeArrayHandle Dehydrate();
		void Dehydrate(IDataDehydrator dehydrator);
		void Rehydrate(SafeArrayHandle data);
		void Rehydrate(IDataRehydrator rehydrator);
		IEncryptorParameters Clone();
	}

	public abstract class EncryptorParameters : IEncryptorParameters {

		public enum SymetricCiphers : byte {
			
			XCHACHA_20 = 1,
			XCHACHA_40 = 2,
			AES_256 = 3,
			AES_GCM_256 = 4
		}

		public int Iterations { get; set; }
		public int KeyBitLength { get; set; }
		public ByteArray Salt { get; set; }
		
		public SymetricCiphers cipher { get; set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add((byte) this.cipher);
			hashNodeList.Add(this.Iterations);
			hashNodeList.Add(this.KeyBitLength);
			hashNodeList.Add(this.Salt);

			return hashNodeList;
		}

		public SafeArrayHandle Dehydrate() {

			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {

				this.Dehydrate(dehydrator);
				
				return dehydrator.ToArray();
			}
		}
		
		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			
			dehydrator.Write((byte) this.cipher);
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

			this.cipher = (SymetricCiphers) rehydrator.ReadByte();
			this.Iterations = rehydrator.ReadInt();
			this.KeyBitLength = rehydrator.ReadInt();
			this.Salt = rehydrator.ReadNonNullableArray();
		}

		public static IEncryptorParameters RehydrateEncryptor(SafeArrayHandle data) {

			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				return RehydrateEncryptor(rehydrator);
			}
		}

		public static IEncryptorParameters RehydrateEncryptor(IDataRehydrator rehydrator) {

			SymetricCiphers cipher = SymetricCiphers.XCHACHA_40;
			
			rehydrator.RehydrateRewind((dr) => {
				cipher = (SymetricCiphers) dr.ReadByte();
			});

			IEncryptorParameters parameters = null;
			
			if(cipher == SymetricCiphers.AES_256) {
				parameters = new AesEncryptorParameters();
			}
			else if(cipher == SymetricCiphers.AES_GCM_256) {
				parameters = new AesGcmEncryptorParameters();
			}
			else if(cipher == SymetricCiphers.XCHACHA_20) {
				parameters = new XChachaEncryptorParameters(SymetricCiphers.XCHACHA_20);
			}
			else if(cipher == SymetricCiphers.XCHACHA_40) {
				parameters = new XChachaEncryptorParameters(SymetricCiphers.XCHACHA_40);
			} else {
				throw new ArgumentException();
			}

			parameters.Rehydrate(rehydrator);

			return parameters;
		}

		public virtual IEncryptorParameters Clone() {
			IEncryptorParameters clone = this.CreateEncryptorParameter();

			clone.Iterations = this.Iterations;
			clone.cipher = this.cipher;
			clone.KeyBitLength = this.KeyBitLength;
			clone.Salt = this.Salt.Clone();

			return clone;
		}

		protected abstract IEncryptorParameters CreateEncryptorParameter();
	}
	
	public class XChachaEncryptorParameters : EncryptorParameters {

		public XChachaEncryptorParameters(SymetricCiphers symetricCiphers = SymetricCiphers.XCHACHA_40) {
			if(!(symetricCiphers == SymetricCiphers.XCHACHA_20 || symetricCiphers == SymetricCiphers.XCHACHA_40)) {
				throw new ArgumentException("Invalid cypher type");
			}
			this.cipher = symetricCiphers;
		}

		protected override IEncryptorParameters CreateEncryptorParameter() {
			return new XChachaEncryptorParameters(SymetricCiphers.XCHACHA_40);
		}
	}
	
	public class AesEncryptorParameters : EncryptorParameters {
		
		public AesEncryptorParameters() {
			this.cipher = SymetricCiphers.AES_256;
		}

		protected override IEncryptorParameters CreateEncryptorParameter() {
			return new AesEncryptorParameters();
		}
	}
	
	public class AesGcmEncryptorParameters : EncryptorParameters {
		
		public ByteArray Nonce { get; set; }
		public ByteArray Tag { get; set; }
		
		public AesGcmEncryptorParameters() {
			this.cipher = SymetricCiphers.AES_GCM_256;
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();
			
			hashNodeList.Add(this.Nonce);

			return hashNodeList;
		}
		
		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.Nonce);
			dehydrator.WriteNonNullable(this.Tag);
		}
		
		public override void Rehydrate(IDataRehydrator rehydrator) {
			
			base.Rehydrate(rehydrator);
			
			this.Nonce = rehydrator.ReadNonNullableArray();
			this.Tag = rehydrator.ReadNonNullableArray();
		}

		public override IEncryptorParameters Clone() {
			AesGcmEncryptorParameters clone = (AesGcmEncryptorParameters)base.Clone();
			
			clone.Nonce = this.Nonce.Clone();
			clone.Tag = this.Tag.Clone();
			
			return clone;
		}

		protected override IEncryptorParameters CreateEncryptorParameter() {
			return new AesGcmEncryptorParameters();
		}
	}
}