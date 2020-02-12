using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Serialization.V1;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelIndex {
	public class SharedChannelIndex : ChannelIndex<MainIndexedConcatenatedChannelProvider> {

		//TODO: minimize the reading of file sizes. cache them as much as possible
		//TODO: here we use an exception for out of index. perhaps this can be changed to another faster method

		public const ushort BLOCK_INDEX_TYPE = 1;

		public const int SIZE_L1_STRUCTURE_TYPE_ENTRY = sizeof(ushort);
		public const int SIZE_L1_BLOCKID_ENTRY = sizeof(uint);

		public const int BLOCK_INDEX_INTRO = SIZE_L1_STRUCTURE_TYPE_ENTRY + SIZE_L1_BLOCKID_ENTRY;

		private const int SIZE_L1_L3FILE_POINTER_ENTRY = sizeof(ushort);
		private const int SIZE_L1_CHANNEL_POINTER_ENTRY = sizeof(uint);

		private const int SIZE_L2_L3_INCREMENT_POINTER_ENTRY = sizeof(ushort);
		private const int SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY = sizeof(uint);

		private const string L1_INDEX_BASE_NAME = "L1";
		private const string L2_INDEX_BASE_NAME = "L2";
		private const string L3_INDEX_BASE_NAME = "L3";

		private const string INDEX_FILE_NAME_TEMPLATE = "{0}.index";

		private const byte DEFAULT_INDEX_TYPE = 1;

		protected readonly List<BlockChannelUtils.BlockChannelTypes> blockchainEnabledMainIndexChannels;

		// pointer in blocks file, external contents, and then in l3
		private readonly int L1_ENTRY_SIZE;

		private readonly int L1Interval;

		//  l1 increment block file, and l1 increment external contents and then  pointer into l3 (relative to l1)
		private readonly int L2_ENTRY_SIZE;
		private readonly int L2Interval;

		public SharedChannelIndex(string folderPath, BlockChannelUtils.BlockChannelTypes blockchainEnabledChannels, List<BlockChannelUtils.BlockChannelTypes> blockchainEnabledMainIndexChannels, IFileSystem fileSystem, int L1Interval, int L2Interval) : base(folderPath, blockchainEnabledChannels, fileSystem) {
			this.L1Interval = L1Interval;
			this.L2Interval = L2Interval;

			// out of all the main index channels, store which ones are activate on the entire blockchain
			this.blockchainEnabledMainIndexChannels = blockchainEnabledMainIndexChannels;

			// add the keys indices artificially
			if(!this.blockchainEnabledMainIndexChannels.Contains(BlockChannelUtils.BlockChannelTypes.Keys)) {
				this.blockchainEnabledMainIndexChannels.Add(BlockChannelUtils.BlockChannelTypes.Keys);
			}

			this.blockchainEnabledMainIndexChannels = this.blockchainEnabledMainIndexChannels.Distinct().ToList();

			// determine the size of an entry
			this.L1_ENTRY_SIZE = SIZE_L1_L3FILE_POINTER_ENTRY + (SIZE_L1_CHANNEL_POINTER_ENTRY * this.blockchainEnabledMainIndexChannels.Count);
			this.L2_ENTRY_SIZE = SIZE_L2_L3_INCREMENT_POINTER_ENTRY + (SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY * this.blockchainEnabledMainIndexChannels.Count);
		}

		public List<BlockChannelUtils.BlockChannelTypes> EssentialChannelTypes => this.ChannelTypes.Where(p => !p.HasFlag(BlockChannelUtils.BlockChannelTypes.Keys)).ToList();
		public Dictionary<BlockChannelUtils.BlockChannelTypes, MainIndexedConcatenatedChannelProvider> EssentialProviders => this.Providers.Where(p => !p.Key.HasFlag(BlockChannelUtils.BlockChannelTypes.Keys)).ToDictionary();

		public MainIndexedConcatenatedChannelProvider KeyedIndexProvider => this.Providers[BlockChannelUtils.BlockChannelTypes.Keys];

		/// <summary>
		///     The block id offset of this current set of index entries
		/// </summary>
		public uint? StartingId { get; private set; }

		public ushort? IndexType { get; private set; }

		public List<BlockChannelUtils.BlockChannelTypes> ActiveChannelTypes { get; } = new List<BlockChannelUtils.BlockChannelTypes>();

		public FileSpecs L1_FileSpec => this.fileSpecs[L1_INDEX_BASE_NAME];
		public FileSpecs L2_FileSpec => this.fileSpecs[L2_INDEX_BASE_NAME];
		public FileSpecs L3_FileSpec => this.fileSpecs[L3_INDEX_BASE_NAME];

		protected void RunForEachChannelType(Action<BlockChannelUtils.BlockChannelTypes> action, BlockChannelUtils.BlockChannelTypes ignores = BlockChannelUtils.BlockChannelTypes.None) {

			foreach(BlockChannelUtils.BlockChannelTypes entry in this.ActiveChannelTypes.ToArray()) {
				if(!ignores.HasFlag(entry)) {
					action(entry);
				}
			}
		}

		/// <summary>
		///     a special method that will load the current starting index from the idnex file, if it is there
		/// </summary>
		/// <param name="blockIndex"></param>
		public void LoadStartingId(uint blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			if(this.StartingId == null) {

				FileSpecs L1FileSpecs = this.CreateL1FileSpec(blockIndex);

				if(L1FileSpecs.FileSize != 0) {
					SafeArrayHandle blockIdData = FileExtensions.ReadBytes(L1FileSpecs.FilePath, 0, BLOCK_INDEX_INTRO, this.fileSystem);

					if((blockIdData != null) && blockIdData.HasData) {
						TypeSerializer.Deserialize(blockIdData.Span, out ushort indexType);
						this.IndexType = indexType;

						TypeSerializer.Deserialize(blockIdData.Span.Slice(SIZE_L1_STRUCTURE_TYPE_ENTRY, SIZE_L1_BLOCKID_ENTRY), out blockId);
						this.StartingId = blockId;
					}
				} else {
					this.StartingId = blockId;
				}
			}
		}

		protected override void ResetAllFileSpecs(uint adjustedBlockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			base.ResetAllFileSpecs(adjustedBlockId, blockIndex);

			this.fileSpecs.Add(L1_INDEX_BASE_NAME, this.CreateL1FileSpec(blockIndex));
			this.fileSpecs.Add(L2_INDEX_BASE_NAME, new FileSpecs(this.GetBlocksL2IndexFile(blockIndex), this.fileSystem));
			this.fileSpecs.Add(L3_INDEX_BASE_NAME, new FileSpecs(this.GetBlocksL3IndexFile(blockIndex), this.fileSystem));
		}

		private FileSpecs CreateL1FileSpec((long index, long startingBlockId, long endingBlockId) blockIndex) {
			return new FileSpecs(this.GetBlocksL1IndexFile(blockIndex), this.fileSystem);
		}

		public void RegisterChannel(BlockChannelUtils.BlockChannelTypes channelType, MainIndexedConcatenatedChannelProvider channelProvider) {

			this.ActiveChannelTypes.Add(channelType);
			this.Providers.Add(channelType, channelProvider);
		}

		public override void WriteEntry(ChannelsEntries<SafeArrayHandle> blockData) {
			base.WriteEntry(blockData);

			// maintain our undo histories
			WriteHistory L1WriteHistory = new WriteHistory();
			WriteHistory L2WriteHistory = new WriteHistory();
			WriteHistory L3WriteHistory = new WriteHistory();

			var blockChannelHistory = new Dictionary<BlockChannelUtils.BlockChannelTypes, WriteHistory>();

			try {
				uint adjustedBlockId = this.adjustedBlockId.Value;

				bool isFirst = false;

				if(this.L1_FileSpec.FileEmpty) {
					if(adjustedBlockId != 0) {
						throw new ApplicationException("When starting a new index, the adjusted block Value should always be 0");
					}

					if(!this.StartingId.HasValue) {
						throw new ApplicationException("The starting Value should always be set");
					}

					// first thing first, this is the first block, we will add the starting block offset in the L1 index.

					Span<byte> dat = stackalloc byte[BLOCK_INDEX_INTRO];
					TypeSerializer.Serialize(BLOCK_INDEX_TYPE, dat.Slice(0, SIZE_L1_STRUCTURE_TYPE_ENTRY));

					TypeSerializer.Serialize(this.StartingId.Value, dat.Slice(SIZE_L1_STRUCTURE_TYPE_ENTRY, SIZE_L1_BLOCKID_ENTRY));

					// the first entry is the starting block Value, to use as a reference
					this.L1_FileSpec.Write(dat);

					isFirst = true;
				}

				// now get the L1 sizes

				// the L1 pointer into the l3 file
				ushort l3RelativeSize = 0;

				// which L1 entry index do we fall on
				(int l1Index, long _, int l2Index) = this.GetBlockIdSpecs(adjustedBlockId);

				// are we on a write entry for L1
				bool writeL1 = !isFirst && ((adjustedBlockId % this.L1Interval) == 0);

				Dictionary<BlockChannelUtils.BlockChannelTypes, uint> l1RelativeDataSizes = null;

				//L1 level
				// w2 dont write the 0 entry since we already know the offset at the beginning ;)
				if(writeL1) {

					l1RelativeDataSizes = new Dictionary<BlockChannelUtils.BlockChannelTypes, uint>();
					Memory<byte> dat = new byte[this.L1_ENTRY_SIZE];

					//in L3
					// size relative to l1
					l3RelativeSize = (ushort) this.L3_FileSpec.FileSize;
					int offset = 0;
					int length = SIZE_L1_L3FILE_POINTER_ENTRY;
					TypeSerializer.Serialize(l3RelativeSize, dat.Span.Slice(offset, length));
					offset += length;

					// get the distance in blocks file

					this.RunForEachChannelType(channel => {

						uint l1RelativeDataSize = this.Providers[channel].DataFile.FileSize;
						l1RelativeDataSizes.Add(channel, l1RelativeDataSize);

						length = SIZE_L1_CHANNEL_POINTER_ENTRY;
						TypeSerializer.Serialize(l1RelativeDataSize, dat.Span.Slice(offset, length));
						offset += length;

					});

					L1WriteHistory.initialLength = this.L1_FileSpec.FileSize;
					L1WriteHistory.file = this.L1_FileSpec;
					this.L1_FileSpec.Append(dat.Span);
					L1WriteHistory.written = true;

				} else if(l1Index != 0) {
					var entry = this.ReadL1Entry(l1Index);

					if(!entry.HasValue) {
						// we reached the end of the filep
						throw new ApplicationException("Failed to read index file");
					}

					l1RelativeDataSizes = entry.Value.l1relativeSizes.Entries;
					l3RelativeSize += entry.Value.l3relativeSize;
				}

				//L2 level

				//are we on a write entry for l2
				bool writeL2 = (l2Index != 0) && ((adjustedBlockId % this.L2Interval) == 0);

				// now get the L2 pointer into the block file
				Dictionary<BlockChannelUtils.BlockChannelTypes, uint> l2RelativeDataSizes = null;

				// we dont write the 0 entry both at L1 and L2 since we already know the offset at the beginning ;)
				if(writeL2) {
					// get the distance in L3
					l2RelativeDataSizes = new Dictionary<BlockChannelUtils.BlockChannelTypes, uint>();

					// the pointer into the l3 file relative to l1
					ushort l3RelativePointer = (ushort) (this.L3_FileSpec.FileSize - l3RelativeSize);

					Memory<byte> dat = new byte[this.L2_ENTRY_SIZE];

					int offset = 0;
					int length = SIZE_L2_L3_INCREMENT_POINTER_ENTRY;
					TypeSerializer.Serialize(l3RelativePointer, dat.Span.Slice(offset, length));
					offset += length;

					// size block file size relative to l1
					this.RunForEachChannelType(channel => {

						uint l1DeltaSize = this.Providers[channel].DataFile.FileSize - (l1RelativeDataSizes?[channel] ?? 0);
						l2RelativeDataSizes.Add(channel, l1DeltaSize);

						length = SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY;
						TypeSerializer.Serialize(l1DeltaSize, dat.Span.Slice(offset, length));
						offset += length;

					});

					L2WriteHistory.initialLength = this.L2_FileSpec.FileSize;
					L2WriteHistory.file = this.L2_FileSpec;
					this.L2_FileSpec.Append(dat.Span);
					L2WriteHistory.written = true;

				} else if(l2Index != 0) {

					var entry = this.ReadL2Entry(l1Index, l2Index);

					if(!entry.HasValue) {
						// we reached the end of the file and this should never happen
						throw new ApplicationException("Failed to read index file");
					}

				}

				//L3 level

				// ok, since the l3 section, we simply store the size of the block

				using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

				AdaptiveInteger2_5 value = new AdaptiveInteger2_5();

				this.RunForEachChannelType(channel => {

					value.Value = (uint) (blockData[channel]?.Length ?? 0);
					value.Dehydrate(dehydrator);
				});

				// insert the entry
				L3WriteHistory.initialLength = this.L3_FileSpec.FileSize;
				L3WriteHistory.file = this.L3_FileSpec;
				SafeArrayHandle datax = dehydrator.ToRawArray();
				this.L3_FileSpec.Append(datax);
				L3WriteHistory.written = true;
				datax.Return();

				// thats it, insert the block data

				this.RunForEachChannelType(channel => {

					SafeArrayHandle data = blockData[channel];

					// write only if we have data
					if((data != null) && data.HasData) {
						WriteHistory channelWriteHistory = new WriteHistory {initialLength = this.Providers[channel].DataFile.FileSize, file = this.Providers[channel].DataFile};
						blockChannelHistory.Add(channel, channelWriteHistory);
						this.Providers[channel].DataFile.Append(data);
						channelWriteHistory.written = true;
					}
				});
			} catch(Exception ex) {

				Log.Fatal(ex, "Failed to write to the blockchain files. changes will be undone...");

				// ok, something went wrong. we need to restore all our files like they were.

				var histories = blockChannelHistory.Values.ToList();
				histories.Add(L1WriteHistory);
				histories.Add(L2WriteHistory);
				histories.Add(L3WriteHistory);

				var failedFiles = this.UndoFileModifications(histories);

				if(failedFiles.Any()) {
					// ok, some files could not be restored, this si very serious, our blockchain is potentially in an invalid state.
					Log.Fatal($"Failed to undo blockchain file modifications in files: [{string.Join(",", failedFiles)}]. Blockchain could remain in an invalid state.");

					throw new ApplicationException($"Failed to undo blockchain file modifications in files: [{string.Join(",", failedFiles)}]. Blockchain could remain in an invalid state.", ex);
				}

				Log.Warning(ex, "Blockchain write failed but file restore was successful");

				// at least the undo was successful
				throw new ApplicationException("Blockchain write failed but file restore was successful.");
			}
		}

		/// <summary>
		///     Undo changes made to the blockchain in case of errors
		/// </summary>
		private List<string> UndoFileModifications(List<WriteHistory> writeHistories) {

			var failedFiles = new List<string>();

			foreach(WriteHistory entry in writeHistories.Where(e => e.written)) {
				try {
					// reset the file to its original size
					entry.file.Truncate(entry.initialLength);

				} catch(Exception ex) {
					failedFiles.Add(entry.file.FilePath);
				}
			}

			return failedFiles;
		}

		/// <summary>
		///     Read an entry in the L1 index
		/// </summary>
		/// <param name="l1Index"></param>
		/// <returns></returns>
		protected (ushort l3relativeSize, ChannelsEntries<uint> l1relativeSizes)? ReadL1Entry(int l1Index) {

			var l1relativeSizes = new ChannelsEntries<uint>(this.ActiveChannelTypes);

			SafeArrayHandle bytes = null;

			int offset = BLOCK_INDEX_INTRO + ((l1Index - 1) * this.L1_ENTRY_SIZE);

			//TODO: here we load all values even if we only need a few. This could be optimized to load only what we need.

			int dataLength = this.L1_ENTRY_SIZE;

			// check that we are within bounds
			if((offset > this.L1_FileSpec.FileSize) || ((offset + dataLength) > this.L1_FileSpec.FileSize)) {
				return null;
			}

			try {
				bytes = this.L1_FileSpec.ReadBytes(offset, dataLength);

			} catch(ArgumentOutOfRangeException ex) {
				// this will happen if we attempt to read more than we should, thats mean we reach the end of the file. we will handle the null from the callers
				return null;
			}

			if((bytes == null) || !bytes.HasData) {
				return null;
			}

			offset = 0;
			dataLength = SIZE_L1_L3FILE_POINTER_ENTRY;
			TypeSerializer.Deserialize(bytes.Span.Slice(offset, dataLength), out ushort l3RelativeSize);
			offset += dataLength;

			this.ForEachBlockchainEnabledMainIndexChannels(offset, SIZE_L1_CHANNEL_POINTER_ENTRY, (dataOffset, length, type) => {
				TypeSerializer.Deserialize(bytes.Span.Slice(dataOffset, length), out uint l1RelativeSizeResult);
				l1relativeSizes[type] = l1RelativeSizeResult;
			});

			return (l3RelativeSize, l1relativeSizes);
		}

		/// <summary>
		///     Read an entry in the L2 index
		/// </summary>
		/// <param name="l1Index"></param>
		/// <param name="l2Index"></param>
		/// <param name="BlockCacheL1Interval"></param>
		/// <param name="BlockCacheL2Interval"></param>
		/// <returns></returns>
		protected (ushort l3relativeSize2, ChannelsEntries<uint> l2relativeSizes)? ReadL2Entry(int l1Index, int l2Index) {

			var l2relativeSizes = new ChannelsEntries<uint>(this.ActiveChannelTypes);
			SafeArrayHandle bytes = null;

			int offset = (l1Index * ((this.L1Interval / this.L2Interval) - 1) * this.L2_ENTRY_SIZE) + ((l2Index - 1) * this.L2_ENTRY_SIZE);

			//TODO: here we load all values even if we only need a few. This could be optimized to load only what we need.

			int dataLength = this.L2_ENTRY_SIZE;

			// check that we are within bounds
			if((offset > this.L2_FileSpec.FileSize) || ((offset + dataLength) > this.L2_FileSpec.FileSize)) {
				return null;
			}

			try {
				bytes = this.L2_FileSpec.ReadBytes(offset, dataLength);
			} catch(ArgumentOutOfRangeException ex) {
				return null;
			}

			if((bytes == null) || !bytes.HasData) {
				return null;
			}

			offset = 0;
			dataLength = SIZE_L2_L3_INCREMENT_POINTER_ENTRY;
			TypeSerializer.Deserialize(bytes.Span.Slice(offset, dataLength), out ushort l3RelativeSize);
			offset += dataLength;

			this.ForEachBlockchainEnabledMainIndexChannels(offset, SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY, (dataOffset, length, type) => {
				TypeSerializer.Deserialize(bytes.Span.Slice(dataOffset, length), out uint l2RelativeSizeResult);
				l2relativeSizes[type] = l2RelativeSizeResult;
			});

			return (l3RelativeSize, l2relativeSizes);
		}

		/// <summary>
		///     This method will loop for every blockchain channel that is used in this main index, and operate a method only for
		///     the ones that are selected among them, still advancing the offset index
		/// </summary>
		/// <param name="startOffset"></param>
		/// <param name="channelLength"></param>
		/// <param name="action"></param>
		protected int ForEachBlockchainEnabledMainIndexChannels(int startOffset, int channelLength, Action<int, int, BlockChannelUtils.BlockChannelTypes> activeAction, Action<int, int, BlockChannelUtils.BlockChannelTypes> inactiveAction = null) {

			foreach(BlockChannelUtils.BlockChannelTypes type in this.blockchainEnabledMainIndexChannels) {
				if(this.ActiveChannelTypes.Contains(type)) {
					activeAction(startOffset, channelLength, type);
				} else {
					inactiveAction?.Invoke(startOffset, channelLength, type);
				}

				startOffset += channelLength;
			}

			return startOffset;
		}

		/// <summary>
		///     Get the string of bytes for an entire l3 section
		/// </summary>
		/// <param name="id"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		protected (ChannelsEntries<uint> l1RelativeSizes, ushort l3relativeSize, long startingId)? GetL3SectionOffsets(long id) {
			// the L1 pointer into the block file

			var l1RelativeSizes = new ChannelsEntries<uint>(this.ActiveChannelTypes);

			this.RunForEachChannelType(channel => {
				l1RelativeSizes[channel] = 0;
			});

			// the L1 pointer into the l3 file
			ushort l3RelativeSize = 0;

			// which L1 entry index do we fall on
			(int l1Index, long adjustedL2Id, int l2Index) specs = this.GetBlockIdSpecs(id);

			//L1 level
			// w2 dont write the 0 entry since we already know the offset at the beginning ;)
			if(specs.l1Index != 0) {
				var entry = this.ReadL1Entry(specs.l1Index);

				if(!entry.HasValue) {
					// we reached the end of the file, it does not exist
					return null;
				}

				l3RelativeSize = entry.Value.l3relativeSize;

				foreach(var relativeSizeEntry in entry.Value.l1relativeSizes.Entries) {
					l1RelativeSizes[relativeSizeEntry.Key] = relativeSizeEntry.Value;
				}
			}

			//L2 level
			if(specs.l2Index != 0) {

				var entry = this.ReadL2Entry(specs.l1Index, specs.l2Index);

				if(!entry.HasValue) {
					// we reached the end of the file, it does not exist
					return null;
				}

				l3RelativeSize += entry.Value.l3relativeSize2;

				foreach(var relativeSizeEntry in entry.Value.l2relativeSizes.Entries) {
					l1RelativeSizes[relativeSizeEntry.Key] += relativeSizeEntry.Value;
				}
			}

			return (l1RelativeSizes, l3RelativeSize, (specs.l1Index * this.L1Interval) + (specs.l2Index * this.L2Interval));
		}

		public override ChannelsEntries<(long start, int end)> QueryIndex(uint adjustedBlockId) {

			var results = new ChannelsEntries<(long start, int end)>(this.ChannelTypes);

			// now we get the info for our section.
			var sectionOffset = this.GetL3SectionOffsets(adjustedBlockId);

			if(sectionOffset == null) {
				return null; // it was not found
			}

			(int l1Index, long adjustedL2Id, int l2Index) specs = this.GetBlockIdSpecs(adjustedBlockId);

			// first, lets artificially generate an id that would fall in the next section
			// ok, here we must get the next l2 index, so we will artificially find an id that will fall into it
			uint nextSectionId = (uint) ((specs.l1Index * this.L1Interval) + ((specs.l2Index + 1) * this.L2Interval));

			// ok, we have the section start. but sadly, we dont have the length. we attempt to read the next one.  it's start is this one's end.
			var nextSectionOffset = this.GetL3SectionOffsets(nextSectionId);

			if(nextSectionOffset == null) {
				var newL3OffsetEntries = new ChannelsEntries<uint>(this.ChannelTypes);

				newL3OffsetEntries.Entries.ToArray().ForEach(entry => {
					newL3OffsetEntries[entry.Key] = this.Providers[entry.Key].DataFile.FileSize;
				});

				// ok, if its null, its that there is no next section, so we will take the end of the file
				nextSectionOffset = (newL3OffsetEntries, (ushort) this.L3_FileSpec.FileSize, 0);
			}

			// now we sum the sizes of the previous entries in the section. this will give us the offset relative to L2 of the block position in the file
			var sum = this.SumL3SectionEntries(sectionOffset.Value.l3relativeSize, (ushort) (nextSectionOffset.Value.l3relativeSize - sectionOffset.Value.l3relativeSize), (int) (adjustedBlockId - sectionOffset.Value.startingId));

			if(sum == null) {
				return null; // it was not found or something went wrong
			}

			this.RunForEachChannelType(channel => {

				results[channel] = ((int) sectionOffset.Value.l1RelativeSizes[channel] + sum[channel].blockOffset, sum[channel].blockLength);
			});

			return results;
		}

		/// <summary>
		///     Sequentialy sum all the offsets in the section to get the final offset value. Since and entry gives us the offset
		///     of the start in the block file, we sometimes
		///     need to read one more, to get the size of the block. This can be controlled by the attemptFetchNext parameter.
		/// </summary>
		/// <param name="l3RelativeSize"></param>
		/// <param name="l3Length"></param>
		/// <param name="count"></param>
		/// <param name="attemptFetchNext">attempt to fetch one more telling us the size of the data we are fetching</param>
		/// <returns></returns>
		protected ChannelsEntries<(long blockOffset, int blockLength)> SumL3SectionEntries(ushort l3RelativeSize, ushort l3Length, int count) {

			var entries = new ChannelsEntries<(long blockOffset, int blockLength)>(this.ChannelTypes);

			int index = 0;

			if(index <= count) {

				SafeArrayHandle bytes = null;

				if(l3Length == 0) {
					// we need to read, but the data size is 0
					return null;
				}

				// check that we are within bounds
				if((this.L3_FileSpec.FileSize == 0) || (l3RelativeSize > this.L3_FileSpec.FileSize)) {
					return null;
				}

				if((l3RelativeSize + l3Length) > this.L3_FileSpec.FileSize) {

					// ok, we asked for a full section but we have less in file. we will take it to the end of the file and give it a try
					l3Length = (ushort) (this.L3_FileSpec.FileSize - l3RelativeSize);
				}

				// now we check again
				if(l3Length == 0) {
					// we need to read, but the data size is 0
					return null;
				}

				bytes = this.L3_FileSpec.ReadBytes(l3RelativeSize, l3Length);

				if((bytes != null) && bytes.HasData) {
					IDataRehydrator rehydrator = new DataRehydratorV1(bytes, false);

					AdaptiveInteger2_5 value = new AdaptiveInteger2_5();

					while(!rehydrator.IsEnd && (index <= count)) {

						this.ForEachBlockchainEnabledMainIndexChannels(0, 0, (dataOffset, length, type) => {
							value.Rehydrate(rehydrator);

							(long blockOffset, int blockLength) entry = entries[type];

							if(index < count) {

								entries[type] = (entry.blockOffset + value.Value, entry.blockLength);
							} else {
								entries[type] = (entry.blockOffset, (int) value.Value);
							}

						}, (dataOffset, length, type) => {
							int byteSize = value.ReadByteSize(rehydrator.ReadByte());

							//TODO: ensure this still works after a refactor
							rehydrator.Forward((byte) byteSize - 1);
						});

						index++;
					}

					if(rehydrator.IsEnd && (index <= count)) {
						// we have less than was asked for. we fail here just in case.
						return null;
					}
				}
			}

			return entries;
		}

		public override ChannelsEntries<SafeArrayHandle> QueryBytes(uint adjustedBlockId) {

			var results = new ChannelsEntries<SafeArrayHandle>(this.EssentialChannelTypes);

			var indices = this.QueryIndex(adjustedBlockId);

			if(indices != null) {
				foreach(var provider in this.EssentialProviders) {
					(long start, int end) index = indices[provider.Key];
					results[provider.Key] = provider.Value.DataFile.ReadBytes(index.start, index.end);
				}
			}
			else {
				//throw new BlockLoadException("Failed to load indicies from disk.");
			}

			return results;
		}

		public override ChannelsEntries<SafeArrayHandle> QueryPartialBlockBytes(uint adjustedBlockId, ChannelsEntries<(int offset, int length)> offsets) {
			var indices = this.QueryIndex(adjustedBlockId);

			if(indices == null) {
				//throw new BlockLoadException("Failed to load indicies from disk.");
				return null;
			}
			var results = new ChannelsEntries<SafeArrayHandle>(this.EssentialChannelTypes);

			foreach(var provider in this.EssentialProviders) {
				(long start, int end) index = indices[provider.Key];
				(int offset, int length) subIndex = offsets[provider.Key];

				if(subIndex.length > (index.end - subIndex.offset)) {
					subIndex.length = index.end - subIndex.offset;
				}

				results[provider.Key] = provider.Value.DataFile.ReadBytes(index.start + subIndex.offset, subIndex.length);
			}

			return results;
		}

		public override SafeArrayHandle QueryMasterTransactionOffsets(uint adjustedBlockId, int masterTransactionIndex) {
			var indices = this.QueryIndex(adjustedBlockId);

			if(indices == null) {
				//throw new BlockLoadException("Failed to load indicies from disk.");
				return null;
			}
			
			if(!indices.Entries.Any()) {
				return null;
			}

			(long start, int end) = indices[BlockChannelUtils.BlockChannelTypes.Keys];

			return this.KeyedIndexProvider.DataFile.ReadBytes(start, end);
		}

		public override ChannelsEntries<long> QueryProviderFileSizes() {

			var results = new ChannelsEntries<long>(this.EssentialChannelTypes);

			foreach(var provider in this.EssentialProviders) {

				results[provider.Key] = provider.Value.DataFile.FileSize;
			}

			return results;
		}

		/// <summary>
		///     Break down the block Value into its L1 and L2 specs
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected (int l1Index, long adjustedL2Id, int l2Index) GetBlockIdSpecs(long id) {
			int l1Index = (int) (id / this.L1Interval);
			long adjustedL2Id = id - (l1Index * this.L1Interval);
			int l2Index = (int) (adjustedL2Id / this.L2Interval);

			return (l1Index, adjustedL2Id, l2Index);
		}

		public string GetBlocksL1IndexFile((long index, long startingBlockId, long endingBlockId) blockIndex) {

			return Path.Combine(this.GetBlocksIndexFolderPath(blockIndex.index), this.GetL1IndexFileName());
		}

		public string GetBlocksL2IndexFile((long index, long startingBlockId, long endingBlockId) blockIndex) {

			return Path.Combine(this.GetBlocksIndexFolderPath(blockIndex.index), this.GetL2IndexFileName());
		}

		public string GetBlocksL3IndexFile((long index, long startingBlockId, long endingBlockId) blockIndex) {

			return Path.Combine(this.GetBlocksIndexFolderPath(blockIndex.index), this.GetL3IndexFileName());
		}

		public string GetL1IndexFileName() {

			return string.Format(INDEX_FILE_NAME_TEMPLATE, L1_INDEX_BASE_NAME);
		}

		public string GetL2IndexFileName() {

			return string.Format(INDEX_FILE_NAME_TEMPLATE, L2_INDEX_BASE_NAME);
		}

		public string GetL3IndexFileName() {

			return string.Format(INDEX_FILE_NAME_TEMPLATE, L3_INDEX_BASE_NAME);
		}
	}
}