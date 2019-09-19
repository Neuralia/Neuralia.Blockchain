using System;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization {

	public interface IDehydratedTransaction : IDehydrateBlockchainEvent {
		TransactionIdExtended Uuid { get; set; }
		SafeArrayHandle Header { get; }
		ChannelsEntries<SafeArrayHandle> DataChannels { get; }

		ITransaction RehydratedTransaction { get; set; }
		ChannelsEntries<SafeArrayHandle> DehydrateSplit();
		void Dehydrate(ChannelsEntries<IDataDehydrator> channelDehydrators);

		void RehydrateHeader(SafeArrayHandle data, AccountId accountId, TransactionTimestamp timestamp);
		void RehydrateHeader(IDataRehydrator rehydrator, AccountId accountId, TransactionTimestamp timestamp);
		ITransaction RehydrateTransaction(IBlockchainEventsRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp);
		ITransaction RehydrateTransaction(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}

	/// <summary>
	/// </summary>
	/// <remarks>we ALWAYS write to the low header. the high header is not used for transactions.</remarks>
	public class DehydratedTransaction : IDehydratedTransaction {

		public BlockChannelUtils.BlockChannelTypes HeaderChannel => BlockChannelUtils.BlockChannelTypes.LowHeader;

		public TransactionIdExtended Uuid { get; set; }
		public SafeArrayHandle Header => this.DataChannels[this.HeaderChannel];
		public ChannelsEntries<SafeArrayHandle> DataChannels { get; } = new ChannelsEntries<SafeArrayHandle>();

		public ITransaction RehydratedTransaction { get; set; }

		public SafeArrayHandle Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			// we dont use the high header with transactions. we can remove it
			var essentialEntries = this.DataChannels.Entries.Where(e => e.Key != BlockChannelUtils.BlockChannelTypes.HighHeader).ToList();

			dehydrator.Write(essentialEntries.Count());

			foreach(var entry in essentialEntries) {
				dehydrator.Write((ushort) entry.Key);

				dehydrator.WriteNonNullable(entry.Value);
			}
		}

		public void Dehydrate(ChannelsEntries<IDataDehydrator> channelDehydrators) {

			foreach(var entry in this.DataChannels.Entries) {
				channelDehydrators[entry.Key].WriteNonNullable(entry.Value);
			}
		}

		public ChannelsEntries<SafeArrayHandle> DehydrateSplit() {
			return new ChannelsEntries<SafeArrayHandle>(this.DataChannels);
		}

		public void Rehydrate(SafeArrayHandle data) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				this.Rehydrate(rehydrator);
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			var dataChannels = new ChannelsEntries<SafeArrayHandle>();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				BlockChannelUtils.BlockChannelTypes channelId = (BlockChannelUtils.BlockChannelTypes) rehydrator.ReadUShort();
				SafeArrayHandle channelData = rehydrator.ReadNonNullableArray();

				dataChannels[channelId] = channelData;
			}

			this.Rehydrate(dataChannels);
		}

		public void RehydrateHeader(SafeArrayHandle headerData, AccountId accountId, TransactionTimestamp timestamp) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(headerData)) {

				this.RehydrateHeader(rehydrator, accountId, timestamp);
			}
		}

		public void RehydrateHeader(IDataRehydrator rehydrator, AccountId accountId, TransactionTimestamp timestamp) {

			// peek in the header, extract the transaction id
			this.Uuid = new TransactionIdExtended();
			var rehydratedVersion = Transaction.RehydrateTopHeader(rehydrator, this.Uuid, accountId, timestamp);
		}

		public ITransaction RehydrateTransaction(IBlockchainEventsRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp) {
			if(this.RehydratedTransaction == null) {

				this.RehydratedTransaction = rehydrationFactory.CreateTransaction(this);

				if((accountId == null) && (timestamp == null)) {
					this.RehydratedTransaction.Rehydrate(this, rehydrationFactory);
				} else {
					this.RehydratedTransaction.RehydrateForBlock(this, rehydrationFactory, accountId, timestamp);
				}

			}

			return this.RehydratedTransaction;
		}

		public ITransaction RehydrateTransaction(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			return this.RehydrateTransaction(rehydrationFactory, null, null);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Uuid);

			//TODO: add this

			return nodeList;
		}

		public void Rehydrate(ChannelsEntries<SafeArrayHandle> dataChannels, AccountId accountId, TransactionTimestamp timestamp) {

			this.DataChannels.Entries.Clear();

			foreach(var entry in dataChannels.Entries) {
				this.DataChannels.Entries.Add(entry.Key, entry.Value.Branch());
			}

			// since we dont use the high header, we add it back artificially
			this.DataChannels.Entries.Add(BlockChannelUtils.BlockChannelTypes.HighHeader, ByteArray.Create());

			this.RehydrateHeader(this.Header, accountId, timestamp);
		}

		public void Rehydrate(ChannelsEntries<SafeArrayHandle> dataChannels) {

			this.Rehydrate(dataChannels, null, null);
		}

		public void Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels, AccountId accountId, TransactionTimestamp timestamp) {

			this.Rehydrate(dataChannels.ConvertAll(rehydrator => {

				if(rehydrator.RemainingLength == 0) {
					return ByteArray.Create();
				}

				return (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			}, BlockChannelUtils.BlockChannelTypes.HighHeader | BlockChannelUtils.BlockChannelTypes.Keys), accountId, timestamp);
		}
		
	}
}