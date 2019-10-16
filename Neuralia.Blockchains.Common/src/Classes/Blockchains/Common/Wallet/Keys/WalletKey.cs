using System;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IWalletKey : IDisposable2, ITreeHashable {
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

		Enums.KeyStatus Status { get; set; }
		
		TransactionIdExtended ChangeTransactionId { get; set; }

		// the address of the key inside the confirmation block and keyedTransaction
		KeyAddress KeyAddress { get; set; }

		IWalletKey NextKey { get; set; }
	}

	public abstract class WalletKey : IWalletKey {

		// id of the transaction where the key was published and announced
		public BlockId AnnouncementBlockId { get; set; } = BlockId.NullBlockId;

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

		public Enums.KeyStatus Status { get; set; } = Enums.KeyStatus.Ok;

		public TransactionIdExtended ChangeTransactionId { get; set; }

		public IWalletKey NextKey { get; set; }

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

			// clear the next key too, if applicable
			if(this.NextKey?.PrivateKey != null) {
				Array.Clear(this.NextKey.PrivateKey, 0, this.NextKey.PrivateKey.Length);
				this.NextKey.PrivateKey = null;
			}
		}

		~WalletKey() {
			this.Dispose(false);
		}
	}
}