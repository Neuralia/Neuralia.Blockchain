using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public class KeySet : ITreeHashable, IBinarySerializable, IJsonSerializable {
		public Dictionary<byte, ICryptographicKey> Keys { get; } = new Dictionary<byte, ICryptographicKey>();

		public void Dehydrate(IDataDehydrator dehydrator) {
			var tool = new AdaptiveShort1_2((ushort)this.Keys.Count);
			tool.Dehydrate(dehydrator);

			foreach(KeyValuePair<byte, ICryptographicKey> key in this.Keys.OrderBy(k => k.Key)) {
				key.Value.Dehydrate(dehydrator);
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			var tool = new AdaptiveShort1_2();
			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			if(count < this.Keys.Count) {
				throw new ApplicationException("Invalid key count");
			}

			for(int i = 0; i < count; i++) {

				//rehydrate the key
				ICryptographicKey cryptographicKey = KeyFactory.RehydrateKey(rehydrator);

				if(this.Keys.ContainsKey(cryptographicKey.Ordinal)) {
					// compare the types, make sure they are the same
					if(this.Keys[cryptographicKey.Ordinal].Version != cryptographicKey.Version) {
						throw new ApplicationException("The loaded key is of a different type or version than is expected");
					}

					this.Keys[cryptographicKey.Ordinal] = cryptographicKey;
				} else {
					this.Keys.Add(cryptographicKey.Ordinal, cryptographicKey);
				}
			}
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetArray("Keys", this.Keys.OrderBy(k => k.Key), (deserializer, serializable) => {

				deserializer.WriteObject(s => {
					s.SetProperty("id", serializable.Key);
					s.SetProperty("key", serializable.Value);
				});
			});

		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			foreach(KeyValuePair<byte, ICryptographicKey> key in this.Keys.OrderBy(k => k.Key)) {
				nodeList.Add(key.Value.GetStructuresArray());
			}

			return nodeList;
		}

		public void Add(byte id, CryptographicKeyType keyType) {
			
			if(keyType == CryptographicKeyTypes.Instance.XMSS) {
				this.Add<XmssCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.XMSSMT) {
				this.Add<XmssmtCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.NTRUPrime) {
				this.Add<NTRUPrimeCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.NTRU) {
				this.Add<NTRUCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.MCELIECE) {
				this.Add<McElieceCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.ECDSA) {
				this.Add<TLSCertificate>(id);
			} 
			else if(keyType == CryptographicKeyTypes.Instance.Secret) {
				this.Add<SecretCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.SecretCombo) {
				this.Add<SecretComboCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.SecretDouble) {
				this.Add<SecretDoubleCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.SecretPenta) {
				this.Add<SecretPentaCryptographicKey>(id);
			}
			else if(keyType == CryptographicKeyTypes.Instance.TripleXMSS) {
				this.Add<TripleXmssCryptographicKey>(id);
			}
		}

		public void Add<KEY_TYPE>(byte id)
			where KEY_TYPE : ICryptographicKey, new() {

			this.Add(new KEY_TYPE(), id);
		}

		public void Add(ICryptographicKey cryptographicKey, byte id) {
			cryptographicKey.Ordinal = id;

			this.Add(cryptographicKey);
		}

		public void Add(ICryptographicKey cryptographicKey) {

			this.Keys.Add(cryptographicKey.Ordinal, cryptographicKey);
		}

		public KEY_TYPE Getkey<KEY_TYPE>(byte id)
			where KEY_TYPE : ICryptographicKey {

			return (KEY_TYPE) this.Keys[id];
		}

		public bool KeyLoaded(byte id) {
			if(!this.Keys.ContainsKey(id)) {
				return false;
			}

			return this.Keys[id].PublicKey != null;
		}
	}
}