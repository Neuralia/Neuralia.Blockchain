using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public class KeySet : ITreeHashable, IBinarySerializable, IJsonSerializable {
		public Dictionary<byte, ICryptographicKey> Keys { get; } = new Dictionary<byte, ICryptographicKey>();

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write((byte) this.Keys.Count);

			foreach(var key in this.Keys.OrderBy(k => k.Key)) {
				key.Value.Dehydrate(dehydrator);
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			byte count = rehydrator.ReadByte();

			if(count < this.Keys.Count) {
				throw new ApplicationException("Invalid key count");
			}

			for(byte i = 0; i < count; i++) {

				//rehydrate the key
				ICryptographicKey cryptographicKey = KeyFactory.RehydrateKey(rehydrator);

				if(this.Keys.ContainsKey(cryptographicKey.Id)) {
					// compare the types, make sure they are the same
					if(this.Keys[cryptographicKey.Id].Type != cryptographicKey.Type) {
						throw new ApplicationException("The loaded key is of a different type than is expected");
					}

					this.Keys[cryptographicKey.Id] = cryptographicKey;
				} else {
					this.Keys.Add(cryptographicKey.Id, cryptographicKey);
				}
			}
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetArray("Keys", this.Keys.OrderBy(k => k.Key), (deserializer, serializable) => {

				deserializer.SetProperty("id", serializable.Key);
				deserializer.SetProperty("key", serializable.Value);
			});

		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			foreach(var key in this.Keys.OrderBy(k => k.Key)) {
				nodeList.Add(key.Value.GetStructuresArray());
			}

			return nodeList;
		}

		public void Add(byte id, Enums.KeyTypes keyType) {

			switch(keyType) {
				case Enums.KeyTypes.NTRU:
					this.Add<NtruCryptographicKey>(id);

					break;

				case Enums.KeyTypes.XMSS:
					this.Add<XmssCryptographicKey>(id);

					break;

				case Enums.KeyTypes.XMSSMT:
					this.Add<XmssmtCryptographicKey>(id);

					break;

				case Enums.KeyTypes.SecretDouble:
					this.Add<SecretDoubleCryptographicKey>(id);

					break;

				case Enums.KeyTypes.QTESLA:
					this.Add<QTeslaCryptographicKey>(id);

					break;

				case Enums.KeyTypes.SPHINCS:
					this.Add<SphincsCryptographicKey>(id);

					break;

				case Enums.KeyTypes.MCELIECE:
					this.Add<McElieceCryptographicKey>(id);

					break;

				case Enums.KeyTypes.ECDSA:
					this.Add<TLSCertificate>(id);

					break;

				case Enums.KeyTypes.RSA:
					this.Add<TLSCertificate>(id);

					break;
			}
		}

		public void Add<KEY_TYPE>(byte id)
			where KEY_TYPE : ICryptographicKey, new() {

			this.Add(new KEY_TYPE(), id);
		}

		public void Add(ICryptographicKey cryptographicKey, byte id) {
			cryptographicKey.Id = id;

			this.Add(cryptographicKey);
		}

		public void Add(ICryptographicKey cryptographicKey) {

			this.Keys.Add(cryptographicKey.Id, cryptographicKey);
		}

		public KEY_TYPE Getkey<KEY_TYPE>(byte id)
			where KEY_TYPE : ICryptographicKey {

			return (KEY_TYPE) this.Keys[id];
		}

		public bool KeyLoaded(byte id) {
			if(!this.Keys.ContainsKey(id)) {
				return false;
			}

			return this.Keys[id].Key != null;
		}
	}
}