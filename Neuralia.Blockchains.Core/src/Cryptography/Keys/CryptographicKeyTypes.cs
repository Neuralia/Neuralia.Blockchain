using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public class CryptographicKeyType : SimpleUShort<CryptographicKeyType>, IBinarySerializable {

		public CryptographicKeyType() {
		}

		public CryptographicKeyType(byte value) : base(value) {
		}

		public static implicit operator CryptographicKeyType(byte d) {
			return new CryptographicKeyType(d);
		}

		public static bool operator ==(CryptographicKeyType a, CryptographicKeyType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(CryptographicKeyType a, CryptographicKeyType b) {
			return !(a == b);
		}

        public static bool Equals(CryptographicKeyType left, CryptographicKeyType right) {
	        return left == right;
        }

        public void Rehydrate(IDataRehydrator rehydrator) {
	        AdaptiveShort1_2 tool = new AdaptiveShort1_2();
	        tool.Rehydrate(rehydrator);
	        this.Value = tool.Value;
        }

        public void Dehydrate(IDataDehydrator dehydrator) {
	        AdaptiveShort1_2 tool = new AdaptiveShort1_2(this.Value);
	        tool.Dehydrate(dehydrator);
        }
	}

	public sealed class CryptographicKeyTypes : UShortConstantSet<CryptographicKeyType> {

		public readonly CryptographicKeyType Unknown;
		public readonly CryptographicKeyType XMSS;
		public readonly CryptographicKeyType XMSSMT;
		public readonly CryptographicKeyType NTRUPrime;
		public readonly CryptographicKeyType NTRU;
		public readonly CryptographicKeyType MCELIECE;
		public readonly CryptographicKeyType SPHINCS;
		public readonly CryptographicKeyType QTESLA;
		public readonly CryptographicKeyType ECDSA;
		public readonly CryptographicKeyType RSA;
		
		
		public readonly CryptographicKeyType Secret;
		public readonly CryptographicKeyType SecretCombo;
		public readonly CryptographicKeyType SecretDouble;
		public readonly CryptographicKeyType SecretPenta;
		public readonly CryptographicKeyType TripleXMSS;

		static CryptographicKeyTypes() {
		}

		private CryptographicKeyTypes() : base((ushort) 100) {
			this.Unknown = this.CreateBaseConstant();
			this.XMSS = this.CreateBaseConstant();
			this.XMSSMT = this.CreateBaseConstant();
			this.NTRUPrime = this.CreateBaseConstant();
			this.NTRU = this.CreateBaseConstant();
			this.MCELIECE = this.CreateBaseConstant();
			this.SPHINCS = this.CreateBaseConstant();
			this.QTESLA = this.CreateBaseConstant();
			this.ECDSA = this.CreateBaseConstant();
			this.RSA = this.CreateBaseConstant();
			
			this.Secret = this.CreateBaseConstant();
			this.SecretCombo = this.CreateBaseConstant();
			this.SecretDouble = this.CreateBaseConstant();
			this.SecretPenta = this.CreateBaseConstant();
			this.TripleXMSS = this.CreateBaseConstant();
		}

		public static CryptographicKeyTypes Instance { get; } = new CryptographicKeyTypes();
	}

}