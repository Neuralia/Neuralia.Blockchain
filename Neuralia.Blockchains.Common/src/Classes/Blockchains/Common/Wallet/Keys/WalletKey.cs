using System;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IWalletKey : IKey, IDisposableExtended, ITreeHashable, IBinarySerializable {
		[BsonId]
		Guid Id { get; set; }

		BlockId AnnouncementBlockId { get; set; }
		
		string AccountCode { get; set; }

		string Name { get; set; }

		long CreatedTime { get; set; }
		
		SafeArrayHandle PrivateKey { get; }

		long Hash { get; set; }
		
		DateTime? KeyChangeTimeout { get; set; }
		Enums.KeyStatus Status { get; set; }

		TransactionId ChangeTransactionId { get; set; }

		// the address of the key inside the confirmation block and indexedTransaction
		KeyAddress KeyAddress { get; set; }
	}

	public abstract class WalletKey : Versionable<CryptographicKeyType>, IWalletKey {

		static WalletKey() {
			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => new ComponentVersion<CryptographicKeyType>(bson.AsString));
		}
		// id of the transaction where the key was published and announced
		public BlockId AnnouncementBlockId { get; set; } = new BlockId();
		
		[BsonId]
		public Guid Id { get; set; }

		
		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, JsonIgnore]
		public KeyUseIndexSet Index {
			get => this.KeyAddress.KeyUseIndex.Clone();
			set => this.KeyAddress.KeyUseIndex = (IdKeyUseIndexSet)value;
		}

		public string AccountCode { get; set; }

		public long CreatedTime { get; set; }

		public string Name { get; set; }

		public SafeArrayHandle PublicKey { get; set; } = SafeArrayHandle.Create();
		public SafeArrayHandle PrivateKey { get; set; } = SafeArrayHandle.Create();

		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, JsonIgnore]
		public byte Ordinal {
			get => this.KeyAddress.OrdinalId;
			set => this.KeyAddress.OrdinalId = value;
		}

		/// <summary>
		///     This is the hash of the ID of this key. we can use it as a public unique without revealing too much about this key.
		///     used in chainstate
		/// </summary>
		public long Hash { get; set; }
		
		/// <summary>
		///     the address of the key inside the confirmation block
		/// </summary>
		public KeyAddress KeyAddress { get; set; } = new KeyAddress();

		public Enums.KeyStatus Status { get; set; } = Enums.KeyStatus.New;

		public DateTime? KeyChangeTimeout { get; set; }

		public TransactionId ChangeTransactionId { get; set; }

		[BsonIgnore, System.Text.Json.Serialization.JsonIgnore, JsonIgnore]
		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Id);
			nodeList.Add(this.AccountCode);
			nodeList.Add(this.CreatedTime);
			nodeList.Add(this.Name);
			nodeList.Add(this.PublicKey);
			nodeList.Add(this.PrivateKey);
			nodeList.Add(this.KeyAddress);
			nodeList.Add(this.KeyChangeTimeout);
			nodeList.Add(this.ChangeTransactionId);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			this.AnnouncementBlockId.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);
			dehydrator.Write(this.AccountCode);
			dehydrator.Write(this.CreatedTime);
			dehydrator.Write(this.Name);

			dehydrator.Write(this.PublicKey);
			dehydrator.Write(this.PrivateKey);

			dehydrator.Write(this.Hash);

			this.KeyAddress.Dehydrate(dehydrator);
			dehydrator.Write((byte) this.Status);

			dehydrator.Write(this.ChangeTransactionId == null);

			if(this.ChangeTransactionId != null) {
				this.ChangeTransactionId.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.KeyChangeTimeout);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);
			
			this.AnnouncementBlockId.Rehydrate(rehydrator);

			this.Id = rehydrator.ReadGuid();
			this.AccountCode = rehydrator.ReadString();
			this.CreatedTime = rehydrator.ReadLong();
			this.Name = rehydrator.ReadString();

			this.PublicKey.Entry = rehydrator.ReadArray();
			this.PrivateKey.Entry = rehydrator.ReadArray();

			this.Hash = rehydrator.ReadLong();

			this.KeyAddress.Rehydrate(rehydrator);
			this.Status = (Enums.KeyStatus) rehydrator.ReadByte();

			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.ChangeTransactionId = new TransactionId();
				this.ChangeTransactionId.Rehydrate(rehydrator);
			}

			this.KeyChangeTimeout = rehydrator.ReadNullableDateTime();
		}
		
		private void Dispose(bool disposing) {

			if(!this.IsDisposed && disposing) {

				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		protected virtual void DisposeAll() {
			// make sure we wipe the private key from memory
			if(this.PrivateKey != null) {
				this.PrivateKey.Clear();
			}
		}

		~WalletKey() {
			this.Dispose(false);
		}
	}
}