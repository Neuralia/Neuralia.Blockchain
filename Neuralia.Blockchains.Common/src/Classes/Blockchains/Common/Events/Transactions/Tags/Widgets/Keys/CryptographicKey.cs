using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ICryptographicKey : ISerializableCombo, IDisposable2 {
		byte Id { get; set; }
		byte Version { get; }
		IByteArray Key { get; set; }

		Enums.KeyTypes Type { get; }
		void Rehydrate(byte id, IDataRehydrator rehydrator);
	}

	public abstract class CryptographicKey : ICryptographicKey {
		private IByteArray key;

		public CryptographicKey() {
			this.SetType();

			if(this.Type == Enums.KeyTypes.Unknown) {
				throw new ApplicationException("Key type is not set");
			}
		}

		public byte Version { get; } = 1;
		public byte Id { get; set; }

		public IByteArray Key {
			get => this.key;
			set => this.key = value?.Clone();
		}

		public Enums.KeyTypes Type { get; protected set; } = Enums.KeyTypes.Unknown;

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write((byte) this.Type);
			dehydrator.Write(this.Version);

			dehydrator.Write(this.Id);
			dehydrator.WriteNonNullable(this.Key);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			Enums.KeyTypes type = (Enums.KeyTypes) rehydrator.ReadByte();

			if(type != this.Type) {
				throw new ApplicationException("Invalid key type");
			}

			byte version = rehydrator.ReadByte();

			if(version != this.Version) {
				throw new ApplicationException("Invalid key version");
			}

			byte id = rehydrator.ReadByte();

			this.Rehydrate(id, rehydrator);
		}

		public virtual void Rehydrate(byte id, IDataRehydrator rehydrator) {
			this.Id = id;

			this.Key = rehydrator.ReadNonNullableArray();

		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Id);
			nodeList.Add(this.Version);
			nodeList.Add(this.Key);

			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("id", this.Id);
			jsonDeserializer.SetProperty("Version", this.Version);

			//
			jsonDeserializer.SetProperty("key", this.Key);
			jsonDeserializer.SetProperty("type", this.Type);
		}

		protected abstract void SetType();
		
	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(!this.IsDisposed) {
				try {
					this.DisposeAll(disposing);
				} finally {
						
				}
			}
		}

		protected virtual void DisposeAll(bool disposing) {
			if(disposing) {
				this.Key?.Dispose();
			}
		}

		~CryptographicKey() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion
	}

}