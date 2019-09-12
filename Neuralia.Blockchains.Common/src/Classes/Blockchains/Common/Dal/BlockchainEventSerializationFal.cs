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
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {

	/// <summary>
	///     readonly and thread safe methods only
	/// </summary>
	public interface IBlockchainEventSerializationFalReadonly {

		string GenesisFolderPath { get; }

		long? GetFileSize(string filename);
		BlockchainEventSerializationFal.BlockchainMessagesMetadata GetMessagesMetadata(string filename);
		void InsertNextMessagesIndex(string filename);

		ChannelsEntries<IByteArray> LoadBlockBytes(long blockId, (int index, long startingBlockId) blockIndex);
		ChannelsEntries<IByteArray> LoadGenesisBlock();
		void WriteGenesisBlock(ChannelsEntries<IByteArray> genesisBlockdata, List<(int offset, int length)> keyedOffsets);

		IByteArray LoadGenesisHeaderBytes(int offset, int length);

		IByteArray LoadDigestBytes(int digestId, string filename);

		IByteArray LoadDigestBytes(int digestId, int offset, int length, string filename);

		IByteArray LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length);

		IByteArray LoadBlockHighHeaderData(long blockId, (int index, long startingBlockId) blockIndex);
		IByteArray LoadGenesisHighHeaderBytes();

		IByteArray LoadBlockPartialTransactionBytes(KeyAddress keyAddress, (int index, long startingBlockId) blockIndex);
		(IByteArray keyBytes, byte treeheight, byte hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal);
		void SaveAccountKeyIndex(AccountId accountId, IByteArray key, byte treeHeight, byte hashBits, byte ordinal);
		ChannelsEntries<int> LoadBlockSize(long blockId, (int index, long startingBlockId) blockIndex);
		(ChannelsEntries<int> sizes, IByteArray hash)? LoadBlockSizeAndHash(long blockId, (int index, long startingBlockId) blockIndex, int hashOffset, int hashLength);
		int? LoadBlockHighHeaderSize(long blockId, (int index, long startingBlockId) blockIndex);
		int? LoadBlockLowHeaderSize(long blockId, (int index, long startingBlockId) blockIndex);
		int? LoadBlockWholeHeaderSize(long blockId, (int index, long startingBlockId) blockIndex);

		int? LoadBlockContentsSize(long blockId, (int index, long startingBlockId) blockIndex);

		ChannelsEntries<IByteArray> LoadBlockPartialBytes(long blockId, (int index, long startingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets);
		IByteArray LoadBlockPartialHighHeaderBytes(long blockId, (int index, long startingBlockId) blockIndex, int offset, int length);
		IByteArray LoadBlockPartialContentsBytes(long blockId, (int index, long startingBlockId) blockIndex, int offset, int length);

		ChannelsEntries<long> GetBlockChannelFileSize((int index, long startingBlockId) blockIndex, BlockChannelUtils.BlockChannelTypes channelType);

		void SaveDigestChannelDescription(string digestFolderPath, BlockchainDigestDescriptor blockchainDigestDescriptor);

		void UpdateCurrentDigest(string digestFolderPath, bool deletePreviousBlocks, (int index, long startingBlockId)? blockGroupIndex);

		string GetBlockPath(int index);

		void SaveDigestHeader(string digestFolderPath, IByteArray digestHeader);

		IByteArray LoadDigestStandardKey(AccountId accountId, byte ordinal);

		int GetDigestHeaderSize(int digestId, string filename);

		List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId);

		IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType);
		IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountSequenceId);
		IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountSequenceId);

		List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards();
		IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id);
	}

	public interface IBlockchainEventSerializationFalReadWrite : IBlockchainEventSerializationFalReadonly {

		bool InsertBlockEntry(long blockId, (int index, long startingBlockId) blockIndex, ChannelsEntries<IByteArray> blockData, List<(int offset, int length)> keyedOffsets);

		void InsertMessagesIndexEntry(string filename, Guid uuid, long blockOffset, int length);
		void InsertMessagesEntry(string filename, IByteArray data);

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

					var resourceAccessScheduler = new ResourceAccessScheduler<BlockchainFiles>(blockchainIndexFiles, fileSystem);

					resourceAccessScheduler.Start();

					indexSchedulers.Add(blocksFolderPath, resourceAccessScheduler);
				}
			}

			if(configurations.EnableFastKeyIndex) {
				this.fastKeyProvider = new FastKeyProvider(this.GetFastKeyIndexPath(), configurations.EnabledFastKeyTypes, this.fileSystem);
			}

			// create the digest access channels
			this.CreateDigestChannelSet(digestFolderPath);
		}

		public DigestChannelSet DigestChannelSet { get; private set; }
		protected IResourceAccessScheduler<BlockchainFiles> ChainScheduler => indexSchedulers[this.blocksFolderPath];

		public string GetBlockPath(int index) {
			return Path.Combine(this.blocksFolderPath, $"{index}");
		}

		/// <summary>
		///     this is a big deal, here we install a new digest, so we delete everything before it
		/// </summary>
		/// <param name="digestFolderPath"></param>
		public void UpdateCurrentDigest(string digestFolderPath, bool deletePreviousBlocks, (int index, long startingBlockId)? blockGroupIndex) {
			// change our channels so we point to the right new digest now
			this.CreateDigestChannelSet(digestFolderPath);

			// delete the other digest folders
			foreach(string directory in this.fileSystem.Directory.GetDirectories(Path.GetDirectoryName(digestFolderPath))) {
				if(directory != digestFolderPath) {
					this.fileSystem.Directory.Delete(directory, true);
				}
			}

			if(deletePreviousBlocks && blockGroupIndex.HasValue) {
				// ok, we must delete all previous blocks
				for(int i = blockGroupIndex.Value.index; i != 0; i--) {
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
		public void SaveDigestHeader(string digestFolderPath, IByteArray digestHeader) {
			string dirName = Path.GetDirectoryName(digestFolderPath);

			this.fileSystem.Directory.CreateDirectory(dirName);

			FileExtensions.WriteAllBytes(digestFolderPath, digestHeader, this.fileSystem);
		}

		public void SaveDigestChannelDescription(string digestFolderPath, BlockchainDigestDescriptor blockchainDigestDescriptor) {

			BlockchainDigestSimpleChannelSetDescriptor descriptor = new BlockchainDigestSimpleChannelSetDescriptor();

			foreach(var channel in blockchainDigestDescriptor.Channels) {
				BlockchainDigestSimpleChannelDescriptor channelDescriptor = new BlockchainDigestSimpleChannelDescriptor();

				channelDescriptor.Version = channel.Value.Version;

				channelDescriptor.TotalEntries = channel.Value.TotalEntries;
				channelDescriptor.LastEntryId = channel.Value.LastEntryId;
				channelDescriptor.GroupSize = channel.Value.GroupSize;

				descriptor.Channels.Add(channel.Key, channelDescriptor);
			}

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			descriptor.Dehydrate(dehydrator);

			string digestDescFilePath = this.GetDigestChannelDescriptionFileName(digestFolderPath);

			this.fileSystem.File.WriteAllBytes(digestDescFilePath, dehydrator.ToArray().ToExactByteArray());
		}

		public IByteArray LoadDigestStandardKey(AccountId accountId, byte ordinal) {
			if(this.DigestChannelSet != null) {
				return ((IStandardAccountKeysDigestChannel) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.StandardAccountKeys]).GetKey(accountId.ToLongRepresentation(), ordinal);
			}

			return null;
		}

		public int GetDigestHeaderSize(int digestId, string filename) {
			return (int) this.fileSystem.FileInfo.FromFileName(filename).Length;
		}

		public List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId) {

			return ((IStandardAccountKeysDigestChannel<IStandardAccountKeysDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.StandardAccountKeys]).GetKeys(accountId);
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
			return ((IAccreditationCertificateDigestChannel<IAccreditationCertificateDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.AccreditationCertificates]).GetAccreditationCertificates();
		}

		public IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id) {
			return ((IAccreditationCertificateDigestChannel<IAccreditationCertificateDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.AccreditationCertificates]).GetAccreditationCertificate(id);
		}

		public IByteArray LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length) {
			return this.DigestChannelSet.Channels[channelId].GetFileBytes(indexId, fileId, partIndex, offset, length);
		}

		public long? GetFileSize(string filename) {
			IFileInfo fileinfo = this.fileSystem.FileInfo.FromFileName(filename);

			if(!fileinfo.Exists) {
				return null;
			}

			return fileinfo.Length;

		}

		public ChannelsEntries<IByteArray> LoadBlockBytes(long blockId, (int index, long startingBlockId) blockIndex) {
			ChannelsEntries<IByteArray> result = null;

			if(blockId == 1) {
				return this.LoadGenesisBlock();
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {

				result = indexer.QueryBlockBytes(blockId, blockIndex);
			});

			return result;
		}

		public ChannelsEntries<IByteArray> LoadGenesisBlock() {

			IByteArray genesisBytes = FileExtensions.ReadAllBytes(this.GetGenesisBlockFilename(), this.fileSystem);

			IDehydratedBlock genesisBlock = new DehydratedBlock();
			genesisBlock.Rehydrate(genesisBytes);

			return genesisBlock.GetEssentialDataChannels();
		}

		public void WriteGenesisBlock(ChannelsEntries<IByteArray> genesisBlockdata, List<(int offset, int length)> keyedOffsets) {

			// this is a special case, where we save it as a dehydrated block
			IDehydratedBlock genesisBlock = new DehydratedBlock();
			genesisBlock.Rehydrate(genesisBlockdata);

			this.fileSystem.Directory.CreateDirectory(this.GetGenesisBlockFolderPath());

			var dataChannels = genesisBlock.GetRawDataChannels();

			dataChannels[BlockChannelUtils.BlockChannelTypes.Keys] = this.PrepareKeyedTransactionData(keyedOffsets);

			dataChannels.RunForAll((band, data) => {

				FileExtensions.WriteAllBytes(this.GetGenesisBlockBandFilename(band.ToString()), data, this.fileSystem);
			});

			IByteArray fullBytes = genesisBlock.Dehydrate();

			FileExtensions.WriteAllBytes(this.GetGenesisBlockFilename(), fullBytes, this.fileSystem);

			// and now the compressed
			GzipCompression compressor = new GzipCompression();
			FileExtensions.WriteAllBytes(this.GetGenesisBlockCompressedFilename(), compressor.Compress(fullBytes), this.fileSystem);
			fullBytes.Return();
		}

		public IByteArray LoadBlockHighHeaderData(long blockId, (int index, long startingBlockId) blockIndex) {
			if(blockId == 1) {
				return this.LoadGenesisHighHeaderBytes();
			}

			IByteArray headerBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				headerBytes = indexer.QueryBlockHighHeaderBytes(blockId, blockIndex);
			});

			return headerBytes;
		}

		public IByteArray LoadGenesisHighHeaderBytes() {

			return FileExtensions.ReadAllBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString()), this.fileSystem);
		}

		public IByteArray LoadGenesisHeaderBytes(int offset, int length) {

			return FileExtensions.ReadBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString()), offset, length, this.fileSystem);
		}

		public IByteArray LoadDigestBytes(int digestId, string filename) {
			if(!this.fileSystem.File.Exists(filename)) {
				return null;
			}

			return (ByteArray) this.fileSystem.File.ReadAllBytes(filename);
		}

		public IByteArray LoadDigestBytes(int digestId, int offset, int length, string filename) {
			if(!this.fileSystem.File.Exists(filename)) {
				return null;
			}

			return FileExtensions.ReadBytes(filename, offset, length, this.fileSystem);
		}

		public IByteArray LoadBlockPartialTransactionBytes(KeyAddress keyAddress, (int index, long startingBlockId) blockIndex) {

			(int offset, int length) offsets = this.LoadBlockKeyedTransactionOffsets(keyAddress.AnnouncementBlockId.Value, blockIndex, keyAddress.KeyedTransactionIndex);

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
		public (IByteArray keyBytes, byte treeheight, byte hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal) {
			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || (ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
				return this.fastKeyProvider?.LoadKeyFile(accountId, ordinal);
			}

			throw new InvalidOperationException($"Key ordinal ID must be either '{GlobalsService.TRANSACTION_KEY_ORDINAL_ID}' or '{GlobalsService.MESSAGE_KEY_ORDINAL_ID}'. Value '{ordinal}' provided.");
		}

		public void SaveAccountKeyIndex(AccountId accountId, IByteArray key, byte treeHeight, byte hashBits, byte ordinal) {
			this.fastKeyProvider?.WriteKey(accountId, key, treeHeight, hashBits, ordinal);
		}

		public IByteArray LoadBlockPartialHighHeaderBytes(long blockId, (int index, long startingBlockId) blockIndex, int offset, int length) {
			if(blockId == 1) {
				return this.LoadGenesisHeaderBytes(offset, length);
			}

			IByteArray headerBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				headerBytes = indexer.QueryPartialBlockHighHeaderBytes(blockId, blockIndex, offset, length);
			});

			return headerBytes;
		}

		public ChannelsEntries<IByteArray> LoadBlockPartialBytes(long blockId, (int index, long startingBlockId) blockIndex, ChannelsEntries<(int offset, int length)> offsets) {

			ChannelsEntries<IByteArray> result = null;

			if(blockId == 1) {
				return this.LoadGenesisBytes(offsets);
			}

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryPartialBlockBytes(blockId, blockIndex, offsets);
			});

			return result;
		}

		public IByteArray LoadBlockPartialContentsBytes(long blockId, (int index, long startingBlockId) blockIndex, int offset, int length) {
			if(blockId == 1) {
				return null;
			}

			IByteArray contentsBytes = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				contentsBytes = indexer.QueryPartialBlockContentBytes(blockId, blockIndex, offset, length);
			});

			return contentsBytes;
		}

		public (ChannelsEntries<int> sizes, IByteArray hash)? LoadBlockSizeAndHash(long blockId, (int index, long startingBlockId) blockIndex, int hashOffset, int hashLength) {

			if(blockId == 1) {
				var genesisSize = this.LoadBlockSize(blockId, blockIndex);

				return (genesisSize, this.LoadGenesisHeaderBytes(hashOffset, hashLength));
			}

			(ChannelsEntries<int> channelEntries, IByteArray hash)? result = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				result = indexer.QueryFullBlockSizeAndHash(blockId, blockIndex, hashOffset, hashLength);
			});

			return result;
		}

		public ChannelsEntries<int> LoadBlockSize(long blockId, (int index, long startingBlockId) blockIndex) {
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

		public int? LoadBlockHighHeaderSize(long blockId, (int index, long startingBlockId) blockIndex) {
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

		public int? LoadBlockLowHeaderSize(long blockId, (int index, long startingBlockId) blockIndex) {
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

		public int? LoadBlockWholeHeaderSize(long blockId, (int index, long startingBlockId) blockIndex) {
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

		public int? LoadBlockContentsSize(long blockId, (int index, long startingBlockId) blockIndex) {
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

		public bool InsertBlockEntry(long blockId, (int index, long startingBlockId) blockIndex, ChannelsEntries<IByteArray> blockData, List<(int offset, int length)> keyedOffsets) {
			bool result = false;

			if(blockId == 1) {
				this.WriteGenesisBlock(blockData, keyedOffsets);

				try {
					// also write a 0 entry in the index to ensure it has an offset spot
					this.ChainScheduler.ScheduleWrite(indexer => {
						result = indexer.SaveBlockBytes(blockId, blockIndex, new ChannelsEntries<IByteArray>(), new ByteArray());
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
				this.ChainScheduler.ScheduleWrite(indexer => {
					result = indexer.SaveBlockBytes(blockId, blockIndex, blockData, this.PrepareKeyedTransactionData(keyedOffsets));
				});
			}

			return result;
		}

		public BlockchainMessagesMetadata GetMessagesMetadata(string filename) {

			return JsonConvert.DeserializeObject<BlockchainMessagesMetadata>(this.fileSystem.File.ReadAllText(filename));
		}

		public void InsertNextMessagesIndex(string filename) {

			BlockchainMessagesMetadata metadata = this.GetMessagesMetadata(filename);

			metadata.Counts.Add(metadata.Counts.Count + 1, 0);

			this.fileSystem.File.Delete(filename);
			this.fileSystem.File.WriteAllText(filename, JsonConvert.SerializeObject(metadata));
		}

		public void InsertMessagesIndexEntry(string filename, Guid uuid, long blockOffset, int length) {

			Span<byte> data = stackalloc byte[SIZE_MESSAGES_INDEX_ENTRY];

			TypeSerializer.Serialize(uuid, data.Slice(0, SIZE_MESSAGES_INDEX_UUID_ENTRY));
			TypeSerializer.Serialize(blockOffset, data.Slice(SIZE_MESSAGES_INDEX_UUID_ENTRY, SIZE_MESSAGES_INDEX_OFFSET_ENTRY));
			TypeSerializer.Serialize(length, data.Slice(SIZE_MESSAGES_INDEX_UUID_ENTRY + SIZE_MESSAGES_INDEX_OFFSET_ENTRY, SIZE_MESSAGES_INDEX_LENGTH_ENTRY));

			FileExtensions.OpenAppend(filename, data, this.fileSystem);
		}

		public void InsertMessagesEntry(string filename, IByteArray data) {
			FileExtensions.OpenAppend(filename, data, this.fileSystem);
		}

		public ChannelsEntries<long> GetBlockChannelFileSize((int index, long startingBlockId) blockIndex, BlockChannelUtils.BlockChannelTypes channelType) {
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

		public (int offset, int length) LoadGenesisKeyedTransactionOffsets(int keyedTransactionIndex) {

			IByteArray data = FileExtensions.ReadAllBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.Keys.ToString()), this.fileSystem);

			return this.ExtractBlockKeyedTransactionOffsets(data, keyedTransactionIndex);
		}

		/// <summary>
		///     a special method to read a keyed transaction from a block
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="keyedTransactionIndex"></param>
		/// <returns></returns>
		public (int offset, int length) LoadBlockKeyedTransactionOffsets(long blockId, (int index, long startingBlockId) blockIndex, int keyedTransactionIndex) {
			if(blockId == 1) {
				return this.LoadGenesisKeyedTransactionOffsets(keyedTransactionIndex);
			}

			IByteArray data = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				data = indexer.QueryBlockKeyedTransactionOffsets(blockId, blockIndex, keyedTransactionIndex);
			});

			return this.ExtractBlockKeyedTransactionOffsets(data, keyedTransactionIndex);
		}

		protected (int offset, int length) ExtractBlockKeyedTransactionOffsets(IByteArray data, int keyedTransactionIndex) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			AdaptiveInteger2_5 numberWriter = new AdaptiveInteger2_5();
			numberWriter.Rehydrate(rehydrator);

			int count = (int) numberWriter.Value;

			if(count > 0) {

				numberWriter.Rehydrate(rehydrator);
				int offset = (int) numberWriter.Value;

				for(int i = 0; i <= keyedTransactionIndex; i++) {

					numberWriter.Rehydrate(rehydrator);
					int length = (int) numberWriter.Value;

					if(i == keyedTransactionIndex) {
						return (offset, length);
					}

					offset += length;
				}
			}

			return default;
		}

		protected IByteArray PrepareKeyedTransactionData(List<(int offset, int length)> keyedOffsets) {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
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

		private string GetDigestChannelDescriptionFileName(string folderBase) {
			return Path.Combine(folderBase, DIGEST_CHANNEL_DESC_FILE);
		}

		private string GetDigestChannelDescriptionFileName() {
			return this.GetDigestChannelDescriptionFileName(this.digestFolderPath);
		}

		protected BlockchainDigestSimpleChannelSetDescriptor LoadBlockchainDigestSimpleChannelDescriptor(string digestDescFilePath) {

			var bytes = this.fileSystem.File.ReadAllBytes(digestDescFilePath);

			BlockchainDigestSimpleChannelSetDescriptor descriptor = new BlockchainDigestSimpleChannelSetDescriptor();

			descriptor.Rehydrate(DataSerializationFactory.CreateRehydrator(bytes));

			return descriptor;
		}

		public ChannelsEntries<IByteArray> LoadGenesisBytes(ChannelsEntries<(int offset, int length)> offsets) {

			var results = new ChannelsEntries<IByteArray>();

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
	}
}