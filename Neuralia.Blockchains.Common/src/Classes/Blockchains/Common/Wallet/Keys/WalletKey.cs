using System;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IWalletKey : IDisposableExtended, ITreeHashable, IBinarySerializable {
		[BsonId]
		Guid Id { get; set; }

		BlockId AnnouncementBlockId { get; set; }

		long KeySequenceId { get; set; }

		Guid AccountUuid { get; set; }

		string Name { get; set; }

		long CreatedTime { get; set; }

		byte[] PublicKey { get; set; }

		byte[] PrivateKey { get; set; }

		long Hash { get; set; }


		Enums.KeyTypes KeyType { get; set; }

		DateTime? KeyChangeTimeout { get; set; }
		Enums.KeyStatus Status { get; set; }
		
		TransactionId ChangeTransactionId { get; set; }

		// the address of the key inside the confirmation block and keyedTransaction
		KeyAddress KeyAddress { get; set; }
	}

	public abstract class WalletKey : IWalletKey {

		// id of the transaction where the key was published and announced
		public BlockId AnnouncementBlockId { get; set; } = new BlockId();

		public long KeySequenceId { get; set; }

		[BsonIgnore]
		public bool IsDisposed { get; private set; }

		[BsonId]
		public Guid Id { get; set; }

		public Guid AccountUuid { get; set; }

		public long CreatedTime { get; set; }

		public string Name { get; set; }

		public byte[] PublicKey { get; set; }

		public byte[] PrivateKey { get; set; }

		/// <summary>
		///     This is the hash of the ID of this key. we can use it as a public unique without revealing too much about this key.
		///     used in chainstate
		/// </summary>
		public long Hash { get; set; }


		/// <summary>
		///     are we using XMSS or XMSSMT
		/// </summary>
		public Enums.KeyTypes KeyType { get; set; }

		/// <summary>
		///     the address of the key inside the confirmation block
		/// </summary>
		public KeyAddress KeyAddress { get; set; } = new KeyAddress();

		public Enums.KeyStatus Status { get; set; } = Enums.KeyStatus.New;

		public DateTime? KeyChangeTimeout { get; set; }
		
		public TransactionId ChangeTransactionId { get; set; }
		
		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Id);
			nodeList.Add(this.AccountUuid);
			nodeList.Add(this.CreatedTime);
			nodeList.Add(this.Name);
			nodeList.Add(this.PublicKey);
			nodeList.Add((byte) this.KeyType);
			nodeList.Add(this.KeyAddress);
			nodeList.Add(this.KeyChangeTimeout);
			nodeList.Add(this.ChangeTransactionId);
			
			return nodeList;
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
				Array.Clear(this.PrivateKey, 0, this.PrivateKey.Length);
				this.PrivateKey = null;
			}
		}

		~WalletKey() {
			this.Dispose(false);
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write((byte)this.KeyType);
			
			this.AnnouncementBlockId.Dehydrate(dehydrator);
			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Value = this.KeySequenceId;
			entry.Dehydrate(dehydrator);

			dehydrator.Write(this.Id);
			dehydrator.Write(this.AccountUuid);
			dehydrator.Write(this.CreatedTime);
			dehydrator.Write(this.Name);
			
			dehydrator.Write(this.PublicKey);
			dehydrator.Write(this.PrivateKey);
			
			dehydrator.Write(this.Hash);

			this.KeyAddress.Dehydrate(dehydrator);
			dehydrator.Write((byte)this.Status);

			dehydrator.Write(this.ChangeTransactionId == (TransactionId)null);

			if(this.ChangeTransactionId != (TransactionId) null) {
				this.ChangeTransactionId.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.KeyChangeTimeout);
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator) {
			
			this.KeyType = (Enums.KeyTypes)rehydrator.ReadByte();
			this.AnnouncementBlockId.Rehydrate(rehydrator);
			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.KeySequenceId = entry.Value;

			this.Id = rehydrator.ReadGuid();
			this.AccountUuid = rehydrator.ReadGuid();
			this.CreatedTime = rehydrator.ReadLong();
			this.Name = rehydrator.ReadString();

			this.PublicKey = rehydrator.ReadArray().ToExactByteArrayCopy();
			this.PrivateKey = rehydrator.ReadArray().ToExactByteArrayCopy();
		
			this.Hash = rehydrator.ReadLong();

			this.KeyAddress.Rehydrate(rehydrator);
			this.Status = (Enums.KeyStatus)rehydrator.ReadByte();

			bool isNull = rehydrator.ReadBool();

			if(isNull == false) {
				this.ChangeTransactionId = new TransactionId();
				this.ChangeTransactionId.Rehydrate(rehydrator);
			}

			this.KeyChangeTimeout = rehydrator.ReadNullableDateTime();
		}
	}
}