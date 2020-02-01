using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.ChannelProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Serialization.V1;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	
	public interface ITrippleChannelBandFileInterpretationProvider<in KEY> {
		SafeArrayHandle QueryCard(KEY id);
		(long start, int end)? QueryIndex(uint adjustedBlockId);

		string GetExpandedL1IndexFile(string band,(uint index, long startingBlockId) indexEntry);
		string GetExpandedL2IndexFile(string band,(uint index, long startingBlockId) indexEntry);
		string GetExpandedL3IndexFile(string band,(uint index, long startingBlockId) indexEntry);
		
		string GetArchivedL1IndexFile(string band,(uint index, long startingBlockId) indexEntry);
		string GetArchivedL2IndexFile(string band,(uint index, long startingBlockId) indexEntry);
		string GetArchivedL3IndexFile(string band,(uint index, long startingBlockId) indexEntry);

		void ResetAllFileSpecs(string mainFilePath, uint adjustedId, (uint index, long startingGroupId) entryIndex);
	}

	public interface ITrippleChannelBandFileInterpretationProvider<NAMING_PROVIDER, KEY> : IDigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ITrippleChannelBandFileInterpretationProvider<KEY>
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {
	}

	/// <summary>
	/// A special interpreter for 3 file indices
	/// </summary>
	/// <typeparam name="NAMING_PROVIDER"></typeparam>
	/// <typeparam name="KEY"></typeparam>
	public class TrippleChannelBandFileInterpretationProvider<NAMING_PROVIDER, KEY> : DigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ITrippleChannelBandFileInterpretationProvider<NAMING_PROVIDER, KEY>
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider 
		where KEY : struct, IEquatable<KEY>, IConvertible{

		private readonly string baseFolder;
		private readonly string scopeFolder;
		private readonly string bandName;
		public TrippleChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider,string baseFolder, string scopeFolder, string bandName, IFileSystem fileSystem) : base(namingProvider, fileSystem) {
			this.L1Interval = 1000;
			this.L2Interval = 100;
			
			
			// determine the size of an entry
			this.L1_ENTRY_SIZE = SIZE_L1_L3FILE_POINTER_ENTRY + SIZE_L1_CHANNEL_POINTER_ENTRY;
			this.L2_ENTRY_SIZE = SIZE_L2_L3_INCREMENT_POINTER_ENTRY + SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY;

			this.baseFolder = baseFolder;
			this.scopeFolder = scopeFolder;
			this.bandName = bandName;
			this.mainFileSpec = new FileSpecs(scopeFolder, fileSystem);
			
		}

		public SafeArrayHandle QueryCard(KEY id) {
			
			uint adjustedIndex = id.ToUInt32(CultureInfo.InvariantCulture);
			
			(long start, int end)? index = this.QueryIndex(adjustedIndex);

			if(!index.HasValue) {
				throw new ApplicationException();
			}

			if(index.Value.start == index.Value.end) {
				return new SafeArrayHandle();
			}

			return this.mainFileSpec.ReadBytes(index.Value.start, (int) index.Value.end);
		}
		
		
		public const ushort INDEX_TYPE = 1;
		
		public const int SIZE_L1_STRUCTURE_TYPE_ENTRY = sizeof(ushort);

		public const int BLOCK_INDEX_INTRO = SIZE_L1_STRUCTURE_TYPE_ENTRY;

		protected const int SIZE_L1_L3FILE_POINTER_ENTRY = sizeof(ushort);
		protected const int SIZE_L1_CHANNEL_POINTER_ENTRY = sizeof(uint);

		protected const int SIZE_L2_L3_INCREMENT_POINTER_ENTRY = sizeof(ushort);
		protected const int SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY = sizeof(uint);

		protected const string L1_INDEX_BASE_NAME = "L1Index.L1";
		protected const string L2_INDEX_BASE_NAME = "L1Index.L2";
		protected const string L3_INDEX_BASE_NAME = "L1Index.L3";

		protected const string INDEX_FILE_NAME_TEMPLATE = "{0}.index";

		// pointer in blocks file, external contents, and then in l3
		protected readonly int L1_ENTRY_SIZE;

		protected readonly int L1Interval;

		//  l1 increment block file, and l1 increment external contents and then  pointer into l3 (relative to l1)
		protected readonly int L2_ENTRY_SIZE;
		protected readonly int L2Interval;

		/// <summary>
		///     The id offset of this current set of index entries
		/// </summary>
		public uint? StartingId { get; private set; }
		
		public ushort? IndexType { get; private set; }

		protected readonly FileSpecs mainFileSpec;
		protected readonly Dictionary<string, FileSpecs> fileSpecs = new Dictionary<string, FileSpecs>();
		
		public FileSpecs L1_FileSpec => this.fileSpecs[L1_INDEX_BASE_NAME];
		public FileSpecs L2_FileSpec => this.fileSpecs[L2_INDEX_BASE_NAME];
		public FileSpecs L3_FileSpec => this.fileSpecs[L3_INDEX_BASE_NAME];
		
		protected (uint index, long startingGroupId) entryIndex;
		

		/// <summary>
		///     Break down the block Value into its L1 and L2 specs
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected (int l1Index, long adjustedL2Id, int l2Index) GetIdSpecs(long id) {
			int l1Index = (int) (id / this.L1Interval);
			long adjustedL2Id = id - (l1Index * this.L1Interval);
			int l2Index = (int) (adjustedL2Id / this.L2Interval);

			return (l1Index, adjustedL2Id, l2Index);
		}


		public void ResetAllFileSpecs(string mainFilePath, uint adjustedId, (uint index, long startingGroupId) entryIndex) {


			this.mainFileSpec.FilePath = mainFilePath;
			this.entryIndex = entryIndex;
			this.fileSpecs.Clear();
			this.fileSpecs.Add(L1_INDEX_BASE_NAME, this.CreateL1FileSpec(entryIndex));
			this.fileSpecs.Add(L2_INDEX_BASE_NAME, new FileSpecs(this.GetExpandedL2IndexFile(this.bandName, entryIndex), new FileSystem()));
			this.fileSpecs.Add(L3_INDEX_BASE_NAME, new FileSpecs(this.GetExpandedL3IndexFile(this.bandName,entryIndex), new FileSystem()));
		}

		private FileSpecs CreateL1FileSpec((uint index, long startingGroupId) groupIndex) {
			return new FileSpecs(this.GetExpandedL1IndexFile(this.bandName,groupIndex), new FileSystem());
		}

		/// <summary>
		///     Read an entry in the L1 index
		/// </summary>
		/// <param name="l1Index"></param>
		/// <returns></returns>
		protected (ushort l3relativeSize, uint l1relativeSize)? ReadL1Entry(int l1Index) {

			uint l1relativeSize = 0;

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

			TypeSerializer.Deserialize(bytes.Span.Slice(offset, SIZE_L1_CHANNEL_POINTER_ENTRY), out uint l1RelativeSizeResult);
			l1relativeSize = l1RelativeSizeResult;

			return (l3RelativeSize, l1relativeSize);
		}

		/// <summary>
		///     Read an entry in the L2 index
		/// </summary>
		/// <param name="l1Index"></param>
		/// <param name="l2Index"></param>
		/// <param name="BlockCacheL1Interval"></param>
		/// <param name="BlockCacheL2Interval"></param>
		/// <returns></returns>
		protected (ushort l3relativeSize2, uint l2relativeSize)? ReadL2Entry(int l1Index, int l2Index) {

			uint l2relativeSize = 0;
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

			TypeSerializer.Deserialize(bytes.Span.Slice(offset, SIZE_L2_L1_INCREMENT_CHANNEL_POINTER_ENTRY), out uint l2RelativeSizeResult);
			l2relativeSize = l2RelativeSizeResult;

			return (l3RelativeSize, l2relativeSize);
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
		protected (long blockOffset, int blockLength)? SumL3SectionEntries(ushort l3RelativeSize, ushort l3Length, int count) {

			(long blockOffset, int blockLength) entry = default;

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

						value.Rehydrate(rehydrator);

						if(index < count) {

							entry = (entry.blockOffset + value.Value, entry.blockLength);
						} else {
							entry = (entry.blockOffset, (int) value.Value);
						}

						index++;
					}

					if(rehydrator.IsEnd && (index <= count)) {
						// we have less than was asked for. we fail here just in case.
						return null;
					}
				}
			}

			return entry;
		}

		/// <summary>
		///     Get the string of bytes for an entire l3 section
		/// </summary>
		/// <param name="id"></param>
		/// <param name="l3FileSize"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		protected (uint l1RelativeSize, ushort l3relativeSize, long startingId)? GetL3SectionOffsets(long id) {
			// the L1 pointer into the block file

			uint l1RelativeSize = 0;

			// the L1 pointer into the l3 file
			ushort l3RelativeSize = 0;

			// which L1 entry index do we fall on
			(int l1Index, long adjustedL2Id, int l2Index) specs = this.GetIdSpecs(id);

			//L1 level
			// w2 dont write the 0 entry since we already know the offset at the beginning ;)
			if(specs.l1Index != 0) {
				var entry = this.ReadL1Entry(specs.l1Index);

				if(!entry.HasValue) {
					// we reached the end of the file, it does not exist
					return null;
				}

				l3RelativeSize = entry.Value.l3relativeSize;


				l1RelativeSize = entry.Value.l1relativeSize;
			}

			//L2 level
			if(specs.l2Index != 0) {

				var entry = this.ReadL2Entry(specs.l1Index, specs.l2Index);

				if(!entry.HasValue) {
					// we reached the end of the file, it does not exist
					return null;
				}

				l3RelativeSize += entry.Value.l3relativeSize2;

				l1RelativeSize += entry.Value.l2relativeSize;
			}

			return (l1RelativeSize, l3RelativeSize, (specs.l1Index * this.L1Interval) + (specs.l2Index * this.L2Interval));
		}
		
		public (long start, int end)? QueryIndex(uint adjustedBlockId) {
			
			// now we get the info for our section.
			var sectionOffset = this.GetL3SectionOffsets(adjustedBlockId);

			if(sectionOffset == null) {
				return null; // it was not found
			}

			(int l1Index, long adjustedL2Id, int l2Index) specs = this.GetIdSpecs(adjustedBlockId);

			// first, lets artificially generate an id that would fall in the next section
			// ok, here we must get the next l2 index, so we will artificially find an id that will fall into it
			uint nextSectionId = (uint) ((specs.l1Index * this.L1Interval) + ((specs.l2Index + 1) * this.L2Interval));

			// ok, we have the section start. but sadly, we dont have the length. we attempt to read the next one.  it's start is this one's end.
			var nextSectionOffset = this.GetL3SectionOffsets(nextSectionId);

			if(nextSectionOffset == null) {
				
				uint newL3OffsetEntry = this.mainFileSpec.FileSize;

				// ok, if its null, its that there is no next section, so we will take the end of the file
				nextSectionOffset = (newL3OffsetEntry, (ushort) this.L3_FileSpec.FileSize, 0);
			}

			// now we sum the sizes of the previous entries in the section. this will give us the offset relative to L2 of the block position in the file
			var sum = this.SumL3SectionEntries(sectionOffset.Value.l3relativeSize, (ushort) (nextSectionOffset.Value.l3relativeSize - sectionOffset.Value.l3relativeSize), (int) (adjustedBlockId - sectionOffset.Value.startingId));

			if(sum == null) {
				return null; // it was not found or something went wrong
			}


			return ((int) sectionOffset.Value.l1RelativeSize + sum.Value.blockOffset, sum.Value.blockLength);
		}

		public string GetExpandedL1IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedExpandedFileName(band,this.GetL1IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
		}

		public string GetExpandedL2IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedExpandedFileName(band,this.GetL2IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
		}

		public string GetExpandedL3IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedExpandedFileName(band,this.GetL3IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
		}

		public string GetArchivedL1IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedArchivedFileName(band,this.GetL1IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
		}

		public string GetArchivedL2IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedArchivedFileName(band,this.GetL2IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
		}

		public string GetArchivedL3IndexFile(string band,(uint index, long startingBlockId) indexEntry) {

			return Path.Combine(this.baseFolder,this.NamingProvider.GeneratedArchivedFileName(band,this.GetL3IndexFileName(), this.scopeFolder, new object[] {indexEntry.index}));
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