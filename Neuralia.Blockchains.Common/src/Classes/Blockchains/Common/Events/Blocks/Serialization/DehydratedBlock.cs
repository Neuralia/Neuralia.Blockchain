using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization {

	public interface IDehydratedBlock : IDehydrateBlockchainEvent {
		BlockId BlockId { get; set; }

		IByteArray Hash { get; set; }
		IByteArray HighHeader { get; set; }
		IByteArray LowHeader { get; set; }
		long HeaderSize { get; }

		IBlock RehydratedBlock { get; }

		ChannelsEntries<IByteArray> GetEssentialDataChannels();
		ChannelsEntries<IByteArray> GetRawDataChannels();
		IBlock RehydrateBlock(IBlockchainEventsRehydrationFactory rehydrationFactory, bool buildOffsets);

		void Rehydrate(ChannelsEntries<IByteArray> dataChannels);

		void Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels);
	}

	public class DehydratedBlock : IDehydratedBlock {

		private readonly ChannelsEntries<IByteArray> dataChannels = new ChannelsEntries<IByteArray>();

		public DehydratedBlock() {

		}

		public DehydratedBlock(IBlock rehydratedBlock) {
			this.RehydratedBlock = rehydratedBlock;
		}

		public BlockId BlockId { get; set; } = BlockId.NullBlockId;

		public IByteArray Hash { get; set; }

		public IByteArray HighHeader {
			get => this.dataChannels.HighHeaderData;
			set => this.dataChannels.HighHeaderData = value;
		}

		public IByteArray LowHeader {
			get => this.dataChannels.LowHeaderData;
			set => this.dataChannels.LowHeaderData = value;
		}

		public long HeaderSize => this.HighHeader.Length + this.LowHeader.Length;

		/// <summary>
		///     Provider the data channels without the keys
		/// </summary>
		/// <returns></returns>
		public ChannelsEntries<IByteArray> GetEssentialDataChannels() {
			return new ChannelsEntries<IByteArray>(this.dataChannels, BlockChannelUtils.BlockChannelTypes.Keys);
		}

		/// <summary>
		///     providate all data channels as the original source
		/// </summary>
		/// <returns></returns>
		public ChannelsEntries<IByteArray> GetRawDataChannels() {
			return this.dataChannels;
		}

		public IBlock RehydratedBlock { get; private set; }

		public IByteArray Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			var otherEntries = this.dataChannels.Entries.Where(e => !e.Key.HasFlag(BlockChannelUtils.BlockChannelTypes.Keys)).OrderBy(v => (int) v.Key);

			dehydrator.Write(otherEntries.Count());

			foreach(var entry in otherEntries) {
				dehydrator.Write((ushort) entry.Key);

				dehydrator.WriteNonNullable(entry.Value);
			}

		}

		public void Rehydrate(IByteArray data) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			var dataChannels = new ChannelsEntries<IByteArray>();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				BlockChannelUtils.BlockChannelTypes channelId = (BlockChannelUtils.BlockChannelTypes) rehydrator.ReadUShort();
				IByteArray channelData = rehydrator.ReadNonNullableArray();

				dataChannels[channelId] = channelData;
			}

			this.Rehydrate(dataChannels);
		}

		public void Rehydrate(ChannelsEntries<IByteArray> dataChannels) {

			var results = BlockHeader.RehydrateHeaderEssentials(DataSerializationFactory.CreateRehydrator(dataChannels.HighHeaderData));

			this.BlockId = results.blockId;
			this.Hash = results.hash;

			this.dataChannels.Entries.Clear();

			foreach(var entry in dataChannels.Entries) {
				this.dataChannels.Entries.Add(entry.Key, entry.Value);
			}
		}

		public void Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels) {

			this.Rehydrate(dataChannels.ConvertAll(rehydrator => rehydrator.ReadArray()));
		}

		public IBlock RehydrateBlock(IBlockchainEventsRehydrationFactory rehydrationFactory, bool buildOffsets) {
			if(this.RehydratedBlock == null) {

				this.RehydratedBlock = rehydrationFactory.CreateBlock(this);

				if(buildOffsets) {
					this.RehydratedBlock.BuildKeyedOffsets();
				}

				this.RehydratedBlock.Rehydrate(this, rehydrationFactory);
			}

			return this.RehydratedBlock;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			//TODO: what should this be?
			nodeList.Add(this.Hash);

			return nodeList;
		}

		/// <summary>
		///     a special method to rehydrate only the header portion of the block
		/// </summary>
		/// <param name="data"></param>
		/// <param name="rehydrationFactory"></param>
		/// <returns></returns>
		public static IBlockHeader RehydrateBlockHeader(IByteArray data, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			IBlockHeader blockHeader = rehydrationFactory.CreateBlock(rehydrator);

			blockHeader.Rehydrate(rehydrator, rehydrationFactory);

			return blockHeader;
		}
	}
}