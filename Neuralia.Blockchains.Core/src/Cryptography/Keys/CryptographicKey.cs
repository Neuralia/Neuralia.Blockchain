using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public interface IKey : IVersionable<CryptographicKeyType>{
		SafeArrayHandle PublicKey { get; }
		byte Ordinal { get; set; }
		KeyUseIndexSet Index { get; set;}
	}

	public interface ICryptographicKey : IKey {
		
		bool IsEmpty { get; }

		void SetFromKey(IKey walletKey);

		SafeArrayHandle Dehydrate();
		void Rehydrate(SafeArrayHandle bytes);
		IdKeyUseIndexSet KeyIndex { get; }
	}

	public abstract class CryptographicKey : Versionable<CryptographicKeyType>, ICryptographicKey {

		public CryptographicKey() {

		}
		
		public CryptographicKey(IKey other) : this() {
			this.SetFromKey(other);
		}

		public byte Ordinal { get; set; }
		public KeyUseIndexSet Index { get; set; } = new KeyUseIndexSet();
		
		public virtual bool IsEmpty => (this.PublicKey == null) || this.PublicKey.IsEmpty || this.PublicKey.IsZero;

		public SafeArrayHandle PublicKey { get; } = SafeArrayHandle.Create();
		
		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);
			
			dehydrator.Write(this.Ordinal);

			this.Index.Dehydrate(dehydrator);
			
			dehydrator.WriteNonNullable(this.PublicKey);
		}

		public SafeArrayHandle Dehydrate() {
			using(IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator()) {
				this.Dehydrate(dehydrator);

				return dehydrator.ToArray();
			}
		}

		public void Rehydrate(SafeArrayHandle bytes) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes)) {
				this.Rehydrate(rehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Ordinal = rehydrator.ReadByte();

			this.Index.Rehydrate(rehydrator);

			this.PublicKey.Entry = rehydrator.ReadNonNullableArray();
		}
		

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Ordinal);
			nodeList.Add(this.Index);
			nodeList.Add(this.PublicKey);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("Id", this.Ordinal);
			jsonDeserializer.SetProperty("Index", this.Index);
			jsonDeserializer.SetProperty("Version", this.Version);
			jsonDeserializer.SetProperty("key", this.PublicKey);
		}
		
		public void SetFromKey(IKey other) {

			KeyUtils.SetFromKey(other, this);
		}

		public IdKeyUseIndexSet KeyIndex => new IdKeyUseIndexSet(this.Index, this.Ordinal);
	}
}