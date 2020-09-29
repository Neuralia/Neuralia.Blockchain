using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization {

	public interface IDehydratedBlock : IDehydrateBlockchainEvent {
		BlockId BlockId { get; set; }

		SafeArrayHandle Hash { get; }
		SafeArrayHandle HighHeader { get; set; }
		SafeArrayHandle LowHeader { get; set; }
		long HeaderSize { get; }

		IBlock RehydratedBlock { get; }

		ChannelsEntries<SafeArrayHandle> GetEssentialDataChannels();
		ChannelsEntries<SafeArrayHandle> GetRawDataChannels();
		IBlock RehydrateBlock(IBlockchainEventsRehydrationFactory rehydrationFactory, bool buildOffsets);

		void Rehydrate(ChannelsEntries<SafeArrayHandle> dataChannels);

		void Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels);
	}

	public class DehydratedBlock : IDehydratedBlock {

		private readonly ChannelsEntries<SafeArrayHandle> dataChannels = new ChannelsEntries<SafeArrayHandle>();

		public DehydratedBlock() {

		}

		public DehydratedBlock(IBlock rehydratedBlock) {
			this.RehydratedBlock = rehydratedBlock;
		}

		public BlockId BlockId { get; set; } = BlockId.NullBlockId;

		public SafeArrayHandle Hash { get; } = SafeArrayHandle.Create();

		public SafeArrayHandle HighHeader {
			get => this.dataChannels.HighHeaderData.Branch();
			set => this.dataChannels.HighHeaderData.Entry = value.Entry;
		}

		public SafeArrayHandle LowHeader {
			get => this.dataChannels.LowHeaderData.Branch();
			set => this.dataChannels.LowHeaderData.Entry = value.Entry;
		}

		public long HeaderSize => this.HighHeader.Length + this.LowHeader.Length;

		/// <summary>
		///     Provider the data channels without the keys
		/// </summary>
		/// <returns></returns>
		public ChannelsEntries<SafeArrayHandle> GetEssentialDataChannels() {
			return new ChannelsEntries<SafeArrayHandle>(this.dataChannels, BlockChannelUtils.BlockChannelTypes.Keys);
		}

		/// <summary>
		///     providate all data channels as the original source
		/// </summary>
		/// <returns></returns>
		public ChannelsEntries<SafeArrayHandle> GetRawDataChannels() {
			return this.dataChannels;
		}

		public IBlock RehydratedBlock { get; private set; }

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			List<KeyValuePair<BlockChannelUtils.BlockChannelTypes, SafeArrayHandle>> otherEntries = this.dataChannels.Entries.Where(e => !e.Key.HasFlag(BlockChannelUtils.BlockChannelTypes.Keys)).OrderBy(v => (int) v.Key).ToList();

			dehydrator.Write(otherEntries.Count);

			foreach(KeyValuePair<BlockChannelUtils.BlockChannelTypes, SafeArrayHandle> entry in otherEntries) {
				dehydrator.Write((ushort) entry.Key);

				dehydrator.WriteNonNullable(entry.Value);
			}

		}

		public void Rehydrate(SafeArrayHandle data) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);

		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			ChannelsEntries<SafeArrayHandle> dataChannels = new ChannelsEntries<SafeArrayHandle>();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				BlockChannelUtils.BlockChannelTypes channelId = (BlockChannelUtils.BlockChannelTypes) rehydrator.ReadUShort();
				SafeArrayHandle channelData = (SafeArrayHandle)rehydrator.ReadNonNullableArray();

				dataChannels[channelId] = channelData;
			}

			this.Rehydrate(dataChannels);
		}

		public void Rehydrate(ChannelsEntries<SafeArrayHandle> dataChannels) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(dataChannels.HighHeaderData);

			(ComponentVersion<BlockType> version, SafeArrayHandle hash, BlockId blockId) results = BlockHeader.RehydrateHeaderEssentials(rehydrator);

			this.BlockId = results.blockId;
			this.Hash.Entry = results.hash.Entry;

			this.dataChannels.Entries.Clear();

			foreach(KeyValuePair<BlockChannelUtils.BlockChannelTypes, SafeArrayHandle> entry in dataChannels.Entries) {
				this.dataChannels.Entries.Add(entry.Key, entry.Value.Branch());
			}

		}

		public void Clear() {
			this.dataChannels.Entries.Clear();
		}
		
		public void Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels) {

			this.Rehydrate(dataChannels.ConvertAll(rehydrator => (SafeArrayHandle) rehydrator.ReadArray()));
		}

		public IBlock RehydrateBlock(IBlockchainEventsRehydrationFactory rehydrationFactory, bool buildOffsets) {
			if(this.RehydratedBlock == null) {

				this.RehydratedBlock = rehydrationFactory.CreateBlock(this);

				if(buildOffsets) {
					this.RehydratedBlock.BuildIndexedTransactionOffsets();
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
		public static IBlockHeader RehydrateBlockHeader(SafeArrayHandle data, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			IBlockHeader blockHeader = rehydrationFactory.CreateBlock(rehydrator);

			blockHeader.Rehydrate(rehydrator, rehydrationFactory);

			return blockHeader;

		}
	}
}