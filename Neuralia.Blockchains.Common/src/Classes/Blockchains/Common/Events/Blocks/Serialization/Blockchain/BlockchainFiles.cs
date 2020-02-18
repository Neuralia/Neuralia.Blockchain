using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain {
	/// <summary>
	///     This is a 3 level index method that is meant to strike a reasonable balance between file size and access speed.
	///     The first level (L1) of the index maintains pointers into the block file. every entry in L2 and L3 after these L1
	///     entries will be relative to this file pointer. this allows to reduce data size.
	///     the second level (L2) holds offsets of both the Block file and L3 index at smaller intervals than L1.
	///     the third level (L3) holds contiguous sections of incrementing deltas from the L2 points. Each entry represents the
	///     size of a block.
	///     to rebuild a pointer, one must use L1 offset + L2 offset + the sum of L3 values in the L3 section.
	///     note: In order to save space, the first index in a new section pointing to a known offset is not saved for L1 & L2.
	///     For example, on a L1 entry which gives a pointer value,
	///     thats because they share the same pointer with 0 offset, and thus dont need to take space in the file.
	/// </summary>
	/// <remarks>
	///     Reading form the index is thread safe, as long as no thread is writing to it. But if any thread writes, then it is
	///     not thread safe during the write.
	///     Because of this, we use a queuing mechanism where no reds can happen while a write is happening. and no write can
	///     start while a read is happening. In order to do this, we have a schedule which ensures the proper ordering.
	///     In any case, a write has priority on any reads, but must wait for any already begun reads to finish before
	///     starting.
	/// </remarks>
	public class BlockchainFiles {

		protected readonly BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels;
		protected readonly List<BlockChannelUtils.BlockChannelTypes> blockchainEnabledMainIndexChannels = new List<BlockChannelUtils.BlockChannelTypes>();

		private readonly List<BlockChannelUtils.BlockChannelTypes> enabledChannels = new List<BlockChannelUtils.BlockChannelTypes>();

		protected readonly IFileSystem fileSystem;

		protected readonly string folderPath;
		protected readonly int mainBlockIndexL1Interval;
		protected readonly int mainBlockIndexL2Interval;

		public BlockchainFiles(string folderPath, int mainBlockIndexL1Interval, int mainBlockIndexL2Interval, BlockChannelUtils.BlockChannelTypes enabledChannels, IFileSystem fileSystem) {

			// we make sure the header is always set, this one is never optional

			this.blockchainEnabledChannels = enabledChannels;
			this.blockchainEnabledChannels |= BlockChannelUtils.BlockChannelTypes.Headers;

			// lets stopre the channels that are activated in this blockchain
			BlockChannelUtils.RunForFlags(this.blockchainEnabledChannels, type => this.enabledChannels.Add(type));

			// now figure out all the main index channels we will be using
			foreach(BlockChannelUtils.BlockChannelTypes indexFlag in BlockChannelUtils.MainIndexChannels) {
				if(this.enabledChannels.Contains(indexFlag)) {
					this.blockchainEnabledMainIndexChannels.Add(indexFlag);
				}
			}

			this.mainBlockIndexL1Interval = mainBlockIndexL1Interval;
			this.mainBlockIndexL2Interval = mainBlockIndexL2Interval;
			this.folderPath = folderPath;
			this.fileSystem = fileSystem ?? new FileSystem();
		}

		public ChannelIndexSet CreateChannelSet() {
			return this.CreateChannelSet(this.blockchainEnabledChannels);
		}

		/// <summary>
		///     Generate the index and provider structure for the requested channels
		/// </summary>
		/// <param name="channels"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public ChannelIndexSet CreateChannelSet(BlockChannelUtils.BlockChannelTypes channels) {

			// make sure the flags they ask for are enabled at the blockchain level
			BlockChannelUtils.RunForFlags(channels, type => {
				if(!this.enabledChannels.Contains(type) && (type != BlockChannelUtils.BlockChannelTypes.Keys)) {
					throw new InvalidOperationException("A requested channel type is not activated in this blockchain");
				}
			});

			ChannelIndexSet channelIndexSet = new ChannelIndexSet();

			// select the providers we will be using
			ChannelsProviderSet activeProviderSet = new ChannelsProviderSet(channels, type => BlockChannelUtils.GetProviderFactory(type, this.folderPath, this.fileSystem)());

			var mainIndexedConcatenatedChannelProviders = activeProviderSet.SelectAllMainIndexedConcatenatedChannelProviders.ToList();

			if(mainIndexedConcatenatedChannelProviders.Any()) {
				// lets prepare our channel indices collection
				SharedChannelIndex mainChannelIndex = new SharedChannelIndex(this.folderPath, this.blockchainEnabledChannels, this.blockchainEnabledMainIndexChannels, this.fileSystem, this.mainBlockIndexL1Interval, this.mainBlockIndexL2Interval);

				// now register the shared channels in the main index
				foreach((BlockChannelUtils.BlockChannelTypes channelType, MainIndexedConcatenatedChannelProvider provider) entry in mainIndexedConcatenatedChannelProviders) {
					mainChannelIndex.RegisterChannel(entry.channelType, entry.provider);
				}

				// we artificially add the keys channel
				if(!mainChannelIndex.ActiveChannelTypes.Contains(BlockChannelUtils.BlockChannelTypes.Keys)) {
					var keysFactory = BlockChannelUtils.GetProviderFactory(BlockChannelUtils.BlockChannelTypes.Keys, this.folderPath, this.fileSystem);
					mainChannelIndex.RegisterChannel(BlockChannelUtils.BlockChannelTypes.Keys, (MainIndexedConcatenatedChannelProvider) keysFactory());
				}

				channelIndexSet.SetMainChannelIndex(mainChannelIndex);
			}

			// and now the single file indices
			foreach((BlockChannelUtils.BlockChannelTypes channelType, IndependentFileChannelProvider provider) entry in activeProviderSet.SelectAllIndependentFileChannelProviders) {
				if(entry.channelType == BlockChannelUtils.BlockChannelTypes.Erasables) {
					channelIndexSet.AddIndex(new ErasableFileChannelIndex(this.folderPath, this.blockchainEnabledChannels, this.fileSystem, entry.channelType, entry.provider));
				} else {
					channelIndexSet.AddIndex(new IndividualFileChannelIndex(this.folderPath, this.blockchainEnabledChannels, this.fileSystem, entry.channelType, entry.provider));
				}

			}

			return channelIndexSet;
		}

		protected uint PrepareContext(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			// now we adjust the id, 0 based blockId
			var returnedId = this.AdjustBlockId(blockId, blockIndex, channelIndexSet);

			if(!returnedId.HasValue) {
				throw new InvalidOperationException("Block id must be set");
			}

			uint adjustedBlockId = returnedId.Value;

			// ensure all the basics are set
			channelIndexSet.Reset(adjustedBlockId, blockIndex);

			return adjustedBlockId;
		}

		public bool SaveBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<SafeArrayHandle> blockData, SafeArrayHandle keyedOffsets) {
			return this.SaveBlockBytes(blockId, blockIndex, this.CreateChannelSet(), blockData, keyedOffsets);
		}

		/// <summary>
		///     Save a block data entry in both the blocks file AND the 3 levels of indexes
		/// </summary>
		/// <exception cref="ApplicationException"></exception>
		/// <remarks>This method is absolutely NOT thread safe.</remarks>
		public bool SaveBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet, ChannelsEntries<SafeArrayHandle> blockData, SafeArrayHandle keyedOffsets) {

			uint adjustedBlockId = this.PrepareContext(blockId, blockIndex, channelIndexSet);

			// lets never save more than once
			if(this.BlockExists(blockId, blockIndex)) {
				return false;
			}

			blockData[BlockChannelUtils.BlockChannelTypes.Keys] = keyedOffsets;

			//thats it, lets write an entry
			channelIndexSet.WriteEntry(blockData);

			return true;
		}

		public SafeArrayHandle QueryBlockHighHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader)).HighHeaderData;
		}

		public SafeArrayHandle QueryBlockLowHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.LowHeader)).LowHeaderData;
		}

		public (SafeArrayHandle high, SafeArrayHandle low) QueryBlockWholeHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			var channels = this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader | BlockChannelUtils.BlockChannelTypes.LowHeader));

			return (channels.HighHeaderData, channels.LowHeaderData);
		}

		public SafeArrayHandle QueryBlockContentsBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Contents)).ContentsData;
		}

		public SafeArrayHandle QueryBlockErasablesBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Erasables)).ErasablesData;
		}

		public SafeArrayHandle QueryBlockSlotsBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Slots)).SlotsData;
		}

		public ChannelsEntries<SafeArrayHandle> QueryBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockBytes(blockId, blockIndex, this.CreateChannelSet());
		}

		/// <summary>
		///     Query the indexes to retreive the proper block bytes
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="l3BlockFileSize"></param>
		/// <returns></returns>
		public ChannelsEntries<SafeArrayHandle> QueryBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			uint adjustedBlockId = this.PrepareContext(blockId, blockIndex, channelIndexSet);

			return channelIndexSet.QueryBytes(adjustedBlockId);
		}

		/// <summary>
		///     this will tell us if a block is already saved in the file
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="l3BlockFileSize"></param>
		/// <param name="l3ContentsFileSize"></param>
		/// <returns></returns>
		public bool BlockExists(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			return this.BlockExists(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader));
		}

		public bool BlockExists(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			if((channelIndexSet.ChannelTypes.Count > 1) || (channelIndexSet.ChannelTypes.First() != BlockChannelUtils.BlockChannelTypes.HighHeader)) {
				channelIndexSet = this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader);
			}

			var result = this.QueryBlockIndex(blockId, blockIndex, channelIndexSet);

			return result?.Entries.Any()??false;
		}

		private uint? AdjustBlockId(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			// all block Ids are zero based here
			uint zeroBlockId = (uint) blockId - 1;

			// start by getting or setting the offset info
			channelIndexSet.MainChannelIndex.LoadStartingId(zeroBlockId, blockIndex);

			uint startingId = zeroBlockId;

			if(channelIndexSet.MainChannelIndex.StartingId.HasValue) {
				startingId = channelIndexSet.MainChannelIndex.StartingId.Value;
			}

			if(startingId != (blockIndex.startingBlockId - 1)) {
				//throw new ApplicationException($"block file starting Id '{startingId + 1}' does not match block index starting id '{blockIndex.startingBlockId}'");
				//TODO: does this even matter?
			}

			return zeroBlockId - startingId;
		}

		/// <summary>
		///     retreive a subset of a block data
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="l3BlockFileSize"></param>
		/// <param name="l3ContentsFileSize"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public SafeArrayHandle QueryPartialBlockHighHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length) {

			return this.QueryPartialBlockBytes(blockId, blockIndex, new ChannelsEntries<(int offset, int length)>(BlockChannelUtils.BlockChannelTypes.HighHeader, (offset, length)), this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader)).HighHeaderData;
		}

		/// <summary>
		///     a very special method that accesses the rare keys channel to extract the offsets of a keyed transaction in a block
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="masterTransactionIndex"></param>
		/// <returns></returns>
		public SafeArrayHandle QueryBlockMasterTransactionOffsets(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int masterTransactionIndex) {
			ChannelIndexSet channelIndexSet = this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Keys);

			uint adjustedBlockId = this.PrepareContext(blockId, blockIndex, channelIndexSet);

			return channelIndexSet.QueryMasterTransactionOffsets(adjustedBlockId, masterTransactionIndex);
		}

		public ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets) {
			return this.QueryPartialBlockBytes(blockId, blockIndex, offsets, this.CreateChannelSet());
		}

		public SafeArrayHandle QueryPartialBlockContentBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length) {

			return this.QueryPartialBlockBytes(blockId, blockIndex, new ChannelsEntries<(int offset, int length)>(BlockChannelUtils.BlockChannelTypes.Contents, (offset, length)), this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Contents)).ContentsData;
		}

		public ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets, ChannelIndexSet channelIndexSet) {

			uint adjustedBlockId = this.PrepareContext(blockId, blockIndex, channelIndexSet);

			return channelIndexSet.QueryPartialBlockBytes(adjustedBlockId, offsets);
		}

		public int? QueryBlockHighHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockSize(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.HighHeader)).HighHeaderData;
		}

		public int? QueryBlockLowHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockSize(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.LowHeader)).LowHeaderData;
		}

		public int? QueryBlockWholeHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			var results = this.QueryBlockSize(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Headers));

			return results.HighHeaderData + results.LowHeaderData;
		}

		public int? QueryBlockContentsSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockSize(blockId, blockIndex, this.CreateChannelSet(BlockChannelUtils.BlockChannelTypes.Contents)).ContentsData;
		}

		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? QueryFullBlockSizeAndHash(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int hashOffset, int hashLength) {
			return this.QueryFullBlockSizeAndHash(blockId, blockIndex, hashOffset, hashLength, this.CreateChannelSet());
		}

		/// <summary>
		///     A special optimization method that combines a block size query with a hash load.
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="hashOffset"></param>
		/// <param name="hashLength"></param>
		/// <param name="indexFileInfos"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? QueryFullBlockSizeAndHash(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int hashOffset, int hashLength, ChannelIndexSet channelIndexSet) {

			var sizes = this.QueryBlockSize(blockId, blockIndex, channelIndexSet);

			SafeArrayHandle hashBytes = this.QueryPartialBlockHighHeaderBytes(blockId, blockIndex, hashOffset, hashLength);

			return (sizes, hashBytes);
		}

		public ChannelsEntries<int> QueryBlockSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			return this.QueryBlockSize(blockId, blockIndex, this.CreateChannelSet());
		}

		public ChannelsEntries<int> QueryBlockSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			var entries = new ChannelsEntries<int>(channelIndexSet.ChannelTypes);

			var indices = this.QueryBlockIndex(blockId, blockIndex, channelIndexSet);

			if(indices != null) {
				entries.RunForAll((key, index) => {

					entries[key] = indices[key].blockEnd;
				});
			}

			return entries;
		}

		public ChannelsEntries<(long blockStart, int blockEnd)> QueryBlockIndex(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			return this.QueryBlockIndex(blockId, blockIndex, this.CreateChannelSet());
		}

		/// <summary>
		///     Query the indexes to retreive the proper block bytes
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="l3BlockFileSize"></param>
		/// <returns></returns>
		public ChannelsEntries<(long blockStart, int blockEnd)> QueryBlockIndex(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelIndexSet channelIndexSet) {

			uint adjustedBlockId = this.PrepareContext(blockId, blockIndex, channelIndexSet);

			return channelIndexSet.QueryIndex(adjustedBlockId);
		}

		public ChannelsEntries<long> GetBlockChannelFileSize((long index, long startingBlockId, long endingBlockId) blockIndex, BlockChannelUtils.BlockChannelTypes channelTypes) {

			ChannelIndexSet channelIndexSet = this.CreateChannelSet(channelTypes);

			channelIndexSet.Reset(0, blockIndex);

			return channelIndexSet.QueryProviderFileSizes();
		}
	}
}