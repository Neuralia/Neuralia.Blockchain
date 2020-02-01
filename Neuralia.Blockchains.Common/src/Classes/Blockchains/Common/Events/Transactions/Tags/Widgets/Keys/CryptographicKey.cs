﻿using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {
	public interface ICryptographicKey : ISerializableCombo {
		byte Id { get; set; }
		byte Version { get; }
		SafeArrayHandle Key { get; }
		bool IsEmpty { get; }

		Enums.KeyTypes Type { get; }
		void Rehydrate(byte id, IDataRehydrator rehydrator);
		void SetFromWalletKey(IWalletKey walletKey);
	}

	public abstract class CryptographicKey : ICryptographicKey {

		public CryptographicKey() {
			this.SetType();

			if(this.Type == Enums.KeyTypes.Unknown) {
				throw new ApplicationException("Key type is not set");
			}
		}

		public byte Version { get; } = 1;
		public byte Id { get; set; }

		public virtual bool IsEmpty => this.Key == null || this.Key.IsEmpty || this.Key.IsZero;

		public  SafeArrayHandle Key { get; } = SafeArrayHandle.Create();

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

			this.Key.Entry = rehydrator.ReadNonNullableArray();

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
		
		
		public virtual void SetFromWalletKey(IWalletKey walletKey) {

			this.Id = walletKey.KeyAddress.OrdinalId;
			this.Key.Entry = ByteArray.CreateClone(walletKey.PublicKey);

		}
	}

}