using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.FastKeyIndex;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using System.Text.Json;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {

	/// <summary>
	///     readonly and thread safe methods only
	/// </summary>
	public interface IBlockchainEventSerializationFalReadonly : IDisposableExtended {

		string GenesisFolderPath { get; }

		long? GetFileSize(string filename);
		BlockchainEventSerializationFal.BlockchainMessagesMetadata GetMessagesMetadata(string filename);
		void InsertNextMessagesIndex(string filename);

		ChannelsEntries<SafeArrayHandle> LoadBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);
		ChannelsEntries<SafeArrayHandle> LoadGenesisBlock();
		void WriteGenesisBlock(ChannelsEntries<SafeArrayHandle> genesisBlockdata, List<(int offset, int length)> keyedOffsets);

		SafeArrayHandle LoadGenesisHeaderBytes(int offset, int length);

		SafeArrayHandle LoadDigestBytes(int digestId, string filename);

		SafeArrayHandle LoadDigestBytes(int digestId, int offset, int length, string filename);

		SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length);
		DigestChannelSet RecreateDigestChannelSet(string digestFolderPath, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor);
		void WriteDigestFile(DigestChannelSet digestChannelSet, DigestChannelType channelId, int indexId, int fileId, uint partIndex, SafeArrayHandle data);
		
		SafeArrayHandle LoadBlockHighHeaderData(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);
		SafeArrayHandle LoadGenesisHighHeaderBytes();

		SafeArrayHandle LoadBlockPartialTransactionBytes(PublishedAddress keyAddress, (long index, long startingBlockId, long endingBlockId) blockIndex);
		(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal);
		bool TestFastKeysPath();
		void SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashBits hashBits, byte ordinal);
		void EnsureFastKeysIndex();
		ChannelsEntries<int> LoadBlockSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);
		(ChannelsEntries<int> sizes, SafeArrayHandle hash)? LoadBlockSizeAndHash(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int hashOffset, int hashLength);
		int? LoadBlockHighHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);
		int? LoadBlockLowHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);
		int? LoadBlockWholeHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);

		int? LoadBlockContentsSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex);

		ChannelsEntries<SafeArrayHandle> LoadBlockPartialBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets);
		SafeArrayHandle LoadBlockPartialHighHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length);
		SafeArrayHandle LoadBlockPartialContentsBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length);

		ChannelsEntries<long> GetBlockChannelFileSize((long index, long startingBlockId, long endingBlockId) blockIndex, BlockChannelUtils.BlockChannelTypes channelType);

		void SaveDigestChannelDescription(string digestFolderPath, BlockchainDigestDescriptor blockchainDigestDescriptor);

		void UpdateCurrentDigest(long digestBlockHeight, string digestFolderPath, bool deletePreviousBlocks, (long index, long startingBlockId, long endingBlockId)? blockGroupIndex);

		string GetBlockPath(long index);

		void SaveDigestHeader(string digestFolderPath, SafeArrayHandle digestHeader);

		SafeArrayHandle LoadDigestStandardKey(AccountId accountId, byte ordinal);

		int GetDigestHeaderSize(int digestId, string filename);

		List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId);

		IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType);
		IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountSequenceId);
		IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountSequenceId);

		List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards();
		IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id);
	}

	public interface IBlockchainEventSerializationFalReadWrite : IBlockchainEventSerializationFalReadonly {

		bool InsertBlockEntry(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<SafeArrayHandle> blockData, List<(int offset, int length)> keyedOffsets);

		void InsertMessagesIndexEntry(string filename, Guid uuid, long blockOffset, int length);
		void InsertMessagesEntry(string filename, SafeArrayHandle data);

		void EnsureFileExists(string filename);
	}

	/// <summary>
	///     The main provider for the custom serialized transaction archive format
	/// </summary>
	public abstract class BlockchainEventSerializationFal : IBlockchainEventSerializationFalReadWrite {

		public const string DIGEST_CHANNEL_DESC_FILE = "digest-desc.neuralia";

		public const string GENESIS_FOLDER_NAME = "genesis";
		public const string GENESIS_BLOCK_FILE_NAME = "genesis.block";
		public const string GENESIS_BLOCK_BAND_FILE_NAME = "genesis.{0}.neuralia";
		public const string GENESIS_BLOCK_COMPRESSED_FILE_NAME = "genesis.block.arch";

		public const string FAST_INDEX_FOLDER_NAME = "keyindices";

		public const int SIZE_BLOCK_INDEX_OFFSET_ENTRY = sizeof(long);

		public const int SIZE_BLOCK_INDEX_LENGTH_ENTRY = sizeof(int);

		// times two because we have the main block file first, then the contents file
		public const int SIZE_BLOCK_SINGLE_INDEX_ENTRY = SIZE_BLOCK_INDEX_OFFSET_ENTRY + SIZE_BLOCK_INDEX_LENGTH_ENTRY;
		public const int SIZE_BLOCK_INDEX_SNAPSHOT = SIZE_BLOCK_SINGLE_INDEX_ENTRY + SIZE_BLOCK_SINGLE_INDEX_ENTRY;

		public const int SIZE_MESSAGES_INDEX_UUID_ENTRY = 16; // size of a Guid
		public const int SIZE_MESSAGES_INDEX_OFFSET_ENTRY = sizeof(long);
		public const int SIZE_MESSAGES_INDEX_LENGTH_ENTRY = sizeof(int);
		public const int SIZE_MESSAGES_INDEX_ENTRY = SIZE_MESSAGES_INDEX_UUID_ENTRY + SIZE_MESSAGES_INDEX_OFFSET_ENTRY + SIZE_MESSAGES_INDEX_LENGTH_ENTRY;

		public static object schedulerLocker = new object();

		/// <summary>
		///     the application wide index scheduler. static to ensure everyone accesses it through the same means
		/// </summary>
		protected static readonly Dictionary<string, IResourceAccessScheduler<BlockchainFiles>> indexSchedulers = new Dictionary<string, IResourceAccessScheduler<BlockchainFiles>>();

		protected readonly IBlockchainDigestChannelFactory blockchainDigestChannelFactory;
		protected readonly string blocksFolderPath;

		private readonly ChainConfigurations configurations;

		protected readonly BlockChannelUtils.BlockChannelTypes enabledChannels;

		protected readonly FastKeyProvider fastKeyProvider;

		protected readonly IFileSystem fileSystem;
		protected string digestFolderPath;

		public BlockchainEventSerializationFal(ChainConfigurations configurations, BlockChannelUtils.BlockChannelTypes enabledChannels, string blocksFolderPath, string digestFolderPath, IBlockchainDigestChannelFactory blockchainDigestChannelFactory, IFileSystem fileSystem) {
			this.blocksFolderPath = blocksFolderPath;
			this.digestFolderPath = digestFolderPath;
			this.blockchainDigestChannelFactory = blockchainDigestChannelFactory;

			this.configurations = configurations;
			this.enabledChannels = enabledChannels;

			this.fileSystem = fileSystem ?? new FileSystem();

			// add one scheduler per chain
			lock(schedulerLocker) {
				if(!indexSchedulers.ContainsKey(blocksFolderPath)) {
					BlockchainFiles blockchainIndexFiles = new BlockchainFiles(blocksFolderPath, configurations.BlockCacheL1Interval, configurations.BlockCacheL2Interval, enabledChannels, fileSystem);

					var resourceAccessScheduler = new SimpleResourceAccessScheduler<BlockchainFiles>(blockchainIndexFiles);
					
					indexSchedulers.Add(blocksFolderPath, resourceAccessScheduler);
				}
			}
			if(configurations.EnableFastKeyIndex) {
				this.fastKeyProvider = new FastKeyProvider(this.GetFastKeyIndexPath(), configurations.EnabledFastKeyTypes, this.fileSystem);
			}
			
			Log.Verbose($"Fast key provider is {(configurations.EnableFastKeyIndex?"enabled":"disabled")}.");

			// create the digest access channels
			this.CreateDigestChannelSet(digestFolderPath);
		}

		public DigestChannelSet DigestChannelSet { get; private set; }
		protected IResourceAccessScheduler<BlockchainFiles> ChainScheduler => indexSchedulers[this.blocksFolderPath];

		public string GetBlockPath(long index) {
			return Path.Combine(this.blocksFolderPath, $"{index}");
		}

		/// <summary>
		///     this is a big deal, here we install a new digest, so we delete everything before it
		/// </summary>
		/// <param name="digestFolderPath"></param>
		public void UpdateCurrentDigest(long digestBlockHeight, string digestFolderPath, bool deletePreviousBlocks, (long index, long startingBlockId, long endingBlockId)? blockGroupIndex) {
			// change our channels so we point to the right new digest now
			this.CreateDigestChannelSet(digestFolderPath);

			// delete the other digest folders
			foreach(string directory in this.fileSystem.Directory.GetDirectories(Path.GetDirectoryName(digestFolderPath))) {
				if(directory != digestFolderPath) {
					this.fileSystem.Directory.Delete(directory, true);
				}
			}

			if(deletePreviousBlocks && blockGroupIndex.HasValue) {
				// ok, we must delete all previous blocks.

				long deleteGroupIndex = blockGroupIndex.Value.index;

				// if the digest block falls inside a block group, then we dont delete it as we need some blocks that come after it.
				if(digestBlockHeight != blockGroupIndex.Value.endingBlockId) {
					deleteGroupIndex -= 1;
				}
				
				for(long i = deleteGroupIndex; i != 0; i--) {
					string folderPath = this.GetBlockPath(i);

					if(this.fileSystem.Directory.Exists(folderPath)) {
						this.fileSystem.Directory.Delete(folderPath, true);
					}
				}
			}
		}

		/// <summary>
		///     here we save the compressed header file directly
		/// </summary>
		/// <param name="digestFolderPath"></param>
		/// <param name="digestHeader"></param>
		public void SaveDigestHeader(string digestHeaderFilepath, SafeArrayHandle digestHeader) {
			string dirName = Path.GetDirectoryName(digestHeaderFilepath);

			FileExtensions.EnsureDirectoryStructure(dirName ,this.fileSystem);

			FileExtensions.WriteAllBytes(digestHeaderFilepath, digestHeader, this.fileSystem);
		}

		public void SaveDigestChannelDescription(string digestFolderPath, BlockchainDigestDescriptor blockchainDigestDescriptor) {

			BlockchainDigestSimpleChannelSetDescriptor descriptor = DigestChannelSetFactory.ConvertToDigestSimpleChannelSetDescriptor(blockchainDigestDescriptor);

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			descriptor.Dehydrate(dehydrator);

			string digestDescFilePath = this.GetDigestChannelDescriptionFileName(digestFolderPath);

			this.fileSystem.File.WriteAllBytes(digestDescFilePath, dehydrator.ToArray().ToExactByteArray());
		}

		public SafeArrayHandle LoadDigestStandardKey(AccountId accountId, byte ordinal) {
			if(this.DigestChannelSet != null) {
				return ((IStandardAccountKeysDigestChannel) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.StandardAccountKeys]).GetKey(accountId.ToLongRepresentation(), ordinal).Branch();
			}

			return null;
		}

		public int GetDigestHeaderSize(int digestId, string filename) {
			return (int) this.fileSystem.FileInfo.FromFileName(filename).Length;
		}

		public List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId) {

			return ((IStandardAccountKeysDigestChannel<IStandardAccountKeysDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.StandardAccountKeys]).GetKeys(accountId).ToList();
		}

		public IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType) {
			if(accountType == Enums.AccountTypes.Standard) {
				return this.LoadDigestStandardAccount(accountSequenceId);
			}

			if(accountType == Enums.AccountTypes.Joint) {
				return this.LoadDigestJointAccount(accountSequenceId);
			}

			return null;
		}

		public IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId) {
			return ((IAccountSnapshotDigestChannel<IStandardAccountSnapshotDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.StandardAccountSnapshot]).GetAccount(accountId);
		}

		public IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId) {
			return ((IAccountSnapshotDigestChannel<IJointAccountSnapshotDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.JointAccountSnapshot]).GetAccount(accountId);
		}

		public List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards() {
			return ((IAccreditationCertificateDigestChannel<IAccreditationCertificateDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.AccreditationCertificates]).GetAccreditationCertificates().ToList();
		}

		public IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id) {
			return ((IAccreditationCertificateDigestChannel<IAccreditationCertificateDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.AccreditationCertificates]).GetAccreditationCertificate(id);
		}

		public SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length) {
			return this.DigestChannelSet.Channels[channelId].GetFileBytes(indexId, fileId, partIndex, offset, length);
		}
		
		public void WriteDigestFile(DigestChannelSet digestChannelSet, DigestChannelType channelId, int indexId, int fileId, uint partIndex, SafeArrayHandle data) {
			digestChannelSet.Channels[channelId].WriteFileBytes(indexId, fileId, partIndex, data);
		}
		
		public long? GetFileSize(string filename) {
			IFileInfo fileinfo = this.fileSystem.FileInfo.FromFileName(filename);

			if(!fileinfo.Exists) {
				return null;
			}

			return fileinfo.Length;

		}

		public ChannelsEntries<SafeArrayHandle> LoadBlockBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			ChannelsEntries<SafeArrayHandle> result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlock();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {

				result = indexer.QueryBlockBytes(blockId, blockIndex);
			});

			return result;
		}

		public ChannelsEntries<SafeArrayHandle> LoadGenesisBlock() {

			using(SafeArrayHandle genesisBytes = FileExtensions.ReadAllBytes(this.GetGenesisBlockFilename(), this.fileSystem)) {

				IDehydratedBlock genesisBlock = new DehydratedBlock();
				genesisBlock.Rehydrate(genesisBytes);

				return genesisBlock.GetEssentialDataChannels();
			}
		}

		public void WriteGenesisBlock(ChannelsEntries<SafeArrayHandle> genesisBlockdata, List<(int offset, int length)> keyedOffsets) {

			// this is a special case, where we save it as a dehydrated block
			IDehydratedBlock genesisBlock = new DehydratedBlock();
			genesisBlock.Rehydrate(genesisBlockdata);

			this.fileSystem.Directory.CreateDirectory(this.GetGenesisBlockFolderPath());

			var dataChannels = genesisBlock.GetRawDataChannels();

			dataChannels[BlockChannelUtils.BlockChannelTypes.Keys] = this.PrepareMasterTransactionData(keyedOffsets);

			dataChannels.RunForAll((band, data) => {

				FileExtensions.WriteAllBytes(this.GetGenesisBlockBandFilename(band.ToString()), data, this.fileSystem);
			});

			using(SafeArrayHandle fullBytes = genesisBlock.Dehydrate()) {

				FileExtensions.WriteAllBytes(this.GetGenesisBlockFilename(), fullBytes, this.fileSystem);

				// and now the compressed
				BrotliCompression compressor = new BrotliCompression();
				FileExtensions.WriteAllBytes(this.GetGenesisBlockCompressedFilename(), compressor.Compress(fullBytes), this.fileSystem);
			}
		}

		public SafeArrayHandle LoadBlockHighHeaderData(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			if(blockId == 1) {
				return this.LoadGenesisHighHeaderBytes();
			}

			SafeArrayHandle headerBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				headerBytes = indexer.QueryBlockHighHeaderBytes(blockId, blockIndex);
			});

			return headerBytes;
		}

		public SafeArrayHandle LoadGenesisHighHeaderBytes() {

			return FileExtensions.ReadAllBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString()), this.fileSystem);
		}

		public SafeArrayHandle LoadGenesisHeaderBytes(int offset, int length) {

			return FileExtensions.ReadBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString()), offset, length, this.fileSystem);
		}

		public SafeArrayHandle LoadDigestBytes(int digestId, string filename) {
			if(!this.fileSystem.File.Exists(filename)) {
				return null;
			}

			return ByteArray.WrapAndOwn(this.fileSystem.File.ReadAllBytes(filename));
		}

		public SafeArrayHandle LoadDigestBytes(int digestId, int offset, int length, string filename) {
			if(!this.fileSystem.File.Exists(filename)) {
				return null;
			}

			return FileExtensions.ReadBytes(filename, offset, length, this.fileSystem);
		}

		public SafeArrayHandle LoadBlockPartialTransactionBytes(PublishedAddress keyAddress, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			(int offset, int length) offsets = this.LoadBlockMasterTransactionOffsets(keyAddress.AnnouncementBlockId.Value, blockIndex, keyAddress.MasterTransactionIndex);

			if(offsets == default) {
				return null;
			}

			return this.LoadBlockPartialHighHeaderBytes(keyAddress.AnnouncementBlockId.Value, blockIndex, offsets.offset, offsets.length);
		}

		/// <summary>
		///     attempt to load a key form the fast file index, if possible
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="ordinal"></param>
		/// <returns></returns>
		public (SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal) {
			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || (ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
				return this.fastKeyProvider?.LoadKeyFile(accountId, ordinal);
			}

			throw new InvalidOperationException($"Key ordinal ID must be either '{GlobalsService.TRANSACTION_KEY_ORDINAL_ID}' or '{GlobalsService.MESSAGE_KEY_ORDINAL_ID}'. Value '{ordinal}' provided.");
		}

		public bool TestFastKeysPath() {

			return this.fastKeyProvider.Test();
		}
		
		public void SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashBits hashBits, byte ordinal) {
			this.fastKeyProvider?.WriteKey(accountId, key, treeHeight, hashBits, ordinal);
		}
		
		/// <summary>
		/// ensure the base structure exists
		/// </summary>
		public void EnsureFastKeysIndex() {
			this.fastKeyProvider?.EnsureBaseFileExists();
		}

		public SafeArrayHandle LoadBlockPartialHighHeaderBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length) {
			if(blockId == 1) {
				return this.LoadGenesisHeaderBytes(offset, length);
			}

			SafeArrayHandle headerBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				headerBytes = indexer.QueryPartialBlockHighHeaderBytes(blockId, blockIndex, offset, length);
			});

			return headerBytes;
		}

		public ChannelsEntries<SafeArrayHandle> LoadBlockPartialBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets) {

			ChannelsEntries<SafeArrayHandle> result = null;

			if(blockId == 1) {
				return this.LoadGenesisBytes(offsets);
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryPartialBlockBytes(blockId, blockIndex, offsets);
			});

			return result;
		}

		public SafeArrayHandle LoadBlockPartialContentsBytes(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int offset, int length) {
			if(blockId == 1) {
				return null;
			}

			SafeArrayHandle contentsBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				contentsBytes = indexer.QueryPartialBlockContentBytes(blockId, blockIndex, offset, length);
			});

			return contentsBytes;
		}

		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? LoadBlockSizeAndHash(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int hashOffset, int hashLength) {

			if(blockId == 1) {
				var genesisSize = this.LoadBlockSize(blockId, blockIndex);

				return (genesisSize, this.LoadGenesisHeaderBytes(hashOffset, hashLength));
			}

			(ChannelsEntries<int> channelEntries, SafeArrayHandle hash)? result = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryFullBlockSizeAndHash(blockId, blockIndex, hashOffset, hashLength);
			});

			return result;
		}

		public ChannelsEntries<int> LoadBlockSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			ChannelsEntries<int> result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlockSize();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryBlockSize(blockId, blockIndex);
			});

			return result;
		}

		public int? LoadBlockHighHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			int? result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlockHighHeaderSize();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryBlockHighHeaderSize(blockId, blockIndex);
			});

			return result;
		}

		public int? LoadBlockLowHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			int? result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlockLowHeaderSize();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryBlockLowHeaderSize(blockId, blockIndex);
			});

			return result;
		}

		public int? LoadBlockWholeHeaderSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			int? result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlockWholeHeaderSize();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryBlockWholeHeaderSize(blockId, blockIndex);
			});

			return result;
		}

		public int? LoadBlockContentsSize(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex) {
			int? result = null;

			if(blockId == 1) {
				return 0;
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryBlockContentsSize(blockId, blockIndex);
			});

			return result;
		}

		public bool InsertBlockEntry(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, ChannelsEntries<SafeArrayHandle> blockData, List<(int offset, int length)> keyedOffsets) {
			bool result = false;

			if(blockId == 1) {
				this.WriteGenesisBlock(blockData, keyedOffsets);

				try {
					// also write a 0 entry in the index to ensure it has an offset spot
					this.ChainScheduler.ScheduleWrite(indexer => {
						result = indexer.SaveBlockBytes(blockId, blockIndex, new ChannelsEntries<SafeArrayHandle>(), BlockchainHashingUtils.GenesisBlockHash);
					});
				} catch {

					// try to clean up
					try {
						var files = new List<string>();

						// try to erase the genesis files
						blockData.RunForAll((band, data) => {
							files.Add(this.GetGenesisBlockBandFilename(band.ToString()));
						});

						files.Add(this.GetGenesisBlockFilename());
						files.Add(this.GetGenesisBlockCompressedFilename());

						foreach(string name in files) {
							try {
								if(this.fileSystem.File.Exists(name)) {
									this.fileSystem.File.Delete(name);
								}
							} catch {
								// do nothing, we tried
							}
						}
					} catch {
						// do nothing, we tried
					}

					throw;
				}
			} else {
				// write a regular block
				using(var bytes = this.PrepareMasterTransactionData(keyedOffsets)) {
					this.ChainScheduler.ScheduleWrite(indexer => {
						result = indexer.SaveBlockBytes(blockId, blockIndex, blockData, bytes);
					});
				}
			}

			return result;
		}

		public BlockchainMessagesMetadata GetMessagesMetadata(string filename) {

			return JsonSerializer.Deserialize<BlockchainMessagesMetadata>(this.fileSystem.File.ReadAllText(filename));
		}

		public void InsertNextMessagesIndex(string filename) {

			BlockchainMessagesMetadata metadata = this.GetMessagesMetadata(filename);

			metadata.Counts.Add(metadata.Counts.Count + 1, 0);

			this.fileSystem.File.Delete(filename);
			this.fileSystem.File.WriteAllText(filename, JsonSerializer.Serialize(metadata));
		}

		public void InsertMessagesIndexEntry(string filename, Guid uuid, long blockOffset, int length) {

			Span<byte> data = stackalloc byte[SIZE_MESSAGES_INDEX_ENTRY];

			TypeSerializer.Serialize(uuid, data.Slice(0, SIZE_MESSAGES_INDEX_UUID_ENTRY));
			TypeSerializer.Serialize(blockOffset, data.Slice(SIZE_MESSAGES_INDEX_UUID_ENTRY, SIZE_MESSAGES_INDEX_OFFSET_ENTRY));
			TypeSerializer.Serialize(length, data.Slice(SIZE_MESSAGES_INDEX_UUID_ENTRY + SIZE_MESSAGES_INDEX_OFFSET_ENTRY, SIZE_MESSAGES_INDEX_LENGTH_ENTRY));

			FileExtensions.OpenAppend(filename, data, this.fileSystem);
		}

		public void InsertMessagesEntry(string filename, SafeArrayHandle data) {
			FileExtensions.OpenAppend(filename, data, this.fileSystem);
		}

		public ChannelsEntries<long> GetBlockChannelFileSize((long index, long startingBlockId, long endingBlockId) blockIndex, BlockChannelUtils.BlockChannelTypes channelType) {
			ChannelsEntries<long> result = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.GetBlockChannelFileSize(blockIndex, channelType);
			});

			return result;
		}

		public void EnsureFileExists(string filename) {
			FileExtensions.EnsureFileExists(filename, this.fileSystem);
		}

		public string GenesisFolderPath => this.GetGenesisBlockFolderPath();

		public (int offset, int length) LoadGenesisMasterTransactionOffsets(int masterTransactionIndex) {

			using(SafeArrayHandle data = FileExtensions.ReadAllBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.Keys.ToString()), this.fileSystem)) {

				return this.ExtractBlockMasterTransactionOffsets(data, masterTransactionIndex);
			}
		}

		/// <summary>
		///     a special method to read a keyed transaction from a block
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="masterTransactionIndex"></param>
		/// <returns></returns>
		public (int offset, int length) LoadBlockMasterTransactionOffsets(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int masterTransactionIndex) {
			if(blockId == 1) {
				return this.LoadGenesisMasterTransactionOffsets(masterTransactionIndex);
			}

			SafeArrayHandle data = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				data = indexer.QueryBlockMasterTransactionOffsets(blockId, blockIndex, masterTransactionIndex);
			});

			using(data) {
				return this.ExtractBlockMasterTransactionOffsets(data, masterTransactionIndex);
			}
		}

		protected (int offset, int length) ExtractBlockMasterTransactionOffsets(SafeArrayHandle data, int masterTransactionIndex) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			AdaptiveInteger2_5 numberWriter = new AdaptiveInteger2_5();
			numberWriter.Rehydrate(rehydrator);

			int count = (int) numberWriter.Value;

			if(count > 0) {

				numberWriter.Rehydrate(rehydrator);
				int offset = (int) numberWriter.Value;

				for(int i = 0; i <= masterTransactionIndex; i++) {

					numberWriter.Rehydrate(rehydrator);
					int length = (int) numberWriter.Value;

					if(i == masterTransactionIndex) {
						return (offset, length);
					}

					offset += length;
				}
			}

			return default;
		}

		protected SafeArrayHandle PrepareMasterTransactionData(List<(int offset, int length)> keyedOffsets) {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			AdaptiveInteger2_5 numberWriter = new AdaptiveInteger2_5();

			numberWriter.Value = (uint) keyedOffsets.Count;
			numberWriter.Dehydrate(dehydrator);

			if(keyedOffsets.Count > 0) {

				numberWriter.Value = (uint) keyedOffsets.First().offset;
				numberWriter.Dehydrate(dehydrator);

				foreach((int offset, int length) entry in keyedOffsets) {

					numberWriter.Value = (uint) entry.length;
					numberWriter.Dehydrate(dehydrator);
				}
			}

			return dehydrator.ToArray();
		}

		private void CreateDigestChannelSet(string digestFolderPath) {
			this.digestFolderPath = digestFolderPath;

			if(this.fileSystem.Directory.Exists(this.digestFolderPath)) {
				string digestDescFilePath = this.GetDigestChannelDescriptionFileName();

				if(this.fileSystem.File.Exists(digestDescFilePath)) {
					this.DigestChannelSet = DigestChannelSetFactory.CreateDigestChannelSet(this.digestFolderPath, this.LoadBlockchainDigestSimpleChannelDescriptor(digestDescFilePath), this.blockchainDigestChannelFactory);
				}
			}
		}
		
		public DigestChannelSet RecreateDigestChannelSet(string digestFolderPath, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor) {

			FileExtensions.EnsureDirectoryStructure(digestFolderPath, this.fileSystem);

			return DigestChannelSetFactory.CreateDigestChannelSet(digestFolderPath,  blockchainDigestDescriptor.Channels.ToDictionary(e => (DigestChannelType)e.Key, e => e.Value), this.blockchainDigestChannelFactory);

		}

		private string GetDigestChannelDescriptionFileName(string folderBase) {
			return Path.Combine(folderBase, DIGEST_CHANNEL_DESC_FILE);
		}

		private string GetDigestChannelDescriptionFileName() {
			return this.GetDigestChannelDescriptionFileName(this.digestFolderPath);
		}

		protected BlockchainDigestSimpleChannelSetDescriptor LoadBlockchainDigestSimpleChannelDescriptor(string digestDescFilePath) {

			var bytes = this.fileSystem.File.ReadAllBytes(digestDescFilePath);

			BlockchainDigestSimpleChannelSetDescriptor descriptor = new BlockchainDigestSimpleChannelSetDescriptor();

			using(var rehydrator = DataSerializationFactory.CreateRehydrator(bytes)) {
				descriptor.Rehydrate(rehydrator);
			}

			return descriptor;
		}

		public ChannelsEntries<SafeArrayHandle> LoadGenesisBytes(ChannelsEntries<(int offset, int length)> offsets) {

			var results = new ChannelsEntries<SafeArrayHandle>();

			offsets.RunForAll((band, bandOffsets) => {
				results[band] = FileExtensions.ReadBytes(this.GetGenesisBlockBandFilename(band.ToString()), bandOffsets.offset, bandOffsets.length, this.fileSystem);

			});

			return results;
		}

		public ChannelsEntries<int> LoadGenesisBlockSize() {

			var sizes = new ChannelsEntries<int>(this.enabledChannels);

			sizes.RunForAll((band, bandOffsets) => {
				sizes[band] = (int) this.fileSystem.FileInfo.FromFileName(this.GetGenesisBlockBandFilename(band.ToString())).Length;

			});

			return sizes;
		}

		public int LoadGenesisBlockHighHeaderSize() {

			return (int) this.fileSystem.FileInfo.FromFileName(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString())).Length;
		}

		public int LoadGenesisBlockLowHeaderSize() {

			return (int) this.fileSystem.FileInfo.FromFileName(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.LowHeader.ToString())).Length;
		}

		public int LoadGenesisBlockWholeHeaderSize() {

			return this.LoadGenesisBlockHighHeaderSize() + this.LoadGenesisBlockLowHeaderSize();
		}

		public string GetFastKeyIndexPath() {
			return Path.Combine(this.blocksFolderPath, FAST_INDEX_FOLDER_NAME);
		}

		public string GetGenesisBlockFolderPath() {
			return Path.Combine(this.blocksFolderPath, GENESIS_FOLDER_NAME);
		}

		public string GetGenesisBlockFilename() {
			return Path.Combine(this.GetGenesisBlockFolderPath(), GENESIS_BLOCK_FILE_NAME);
		}

		public string GetGenesisBlockBandFilename(string channelName) {
			return Path.Combine(this.GetGenesisBlockFolderPath(), string.Format(GENESIS_BLOCK_BAND_FILE_NAME, channelName));
		}

		public string GetGenesisBlockCompressedFilename() {
			return Path.Combine(this.GetGenesisBlockFolderPath(), GENESIS_BLOCK_COMPRESSED_FILE_NAME);
		}

		public class BlockchainMessagesMetadata {
			public Dictionary<int, int> Counts { get; set; }
		}
		
	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				try {

					lock(schedulerLocker) {
						foreach(var entry in indexSchedulers.Values) {
							try {
								entry?.Dispose();
							} catch {
							}
						}
					}

				} catch(Exception ex) {

				} 
			}
			this.IsDisposed = true;
		}

		~BlockchainEventSerializationFal() {
			this.Dispose(false);
		}

	#endregion
	}
}