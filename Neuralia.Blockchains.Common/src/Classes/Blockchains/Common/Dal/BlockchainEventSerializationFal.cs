using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.KeyDictionary;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
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
		Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)?> LoadAccountKeyFromIndex(AccountId accountId, byte ordinal);
		bool TestKeyDictionaryPath();
		Task<List<(AccountId accountId, SafeArrayHandle key, byte treeheight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)>> LoadKeyDictionary(List<(AccountId accountId, byte ordinal)> accountIdKeys, LockContext lockContext);
		Task SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupBits, byte ordinal);
		void EnsureKeyDictionaryIndex();
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

		protected readonly KeyDictionaryProvider KeyDictionaryProvider;

		protected readonly FileSystemWrapper fileSystem;
		protected string digestFolderPath;

		public BlockchainEventSerializationFal(ChainConfigurations configurations, BlockChannelUtils.BlockChannelTypes enabledChannels, string blocksFolderPath, string digestFolderPath, IBlockchainDigestChannelFactory blockchainDigestChannelFactory, FileSystemWrapper fileSystem) {
			this.blocksFolderPath = blocksFolderPath;
			this.digestFolderPath = digestFolderPath;
			this.blockchainDigestChannelFactory = blockchainDigestChannelFactory;

			this.configurations = configurations;
			this.enabledChannels = enabledChannels;

			this.fileSystem = fileSystem ?? FileSystemWrapper.CreatePhysical();

			// add one scheduler per chain
			lock(schedulerLocker) {
				if(!indexSchedulers.ContainsKey(blocksFolderPath)) {
					BlockchainFiles blockchainIndexFiles = new BlockchainFiles(blocksFolderPath, configurations.BlockCacheL1Interval, configurations.BlockCacheL2Interval, enabledChannels, fileSystem);

					SimpleResourceAccessScheduler<BlockchainFiles> resourceAccessScheduler = new SimpleResourceAccessScheduler<BlockchainFiles>(blockchainIndexFiles);

					indexSchedulers.Add(blocksFolderPath, resourceAccessScheduler);
				}
			}

			if(configurations.EnableKeyDictionaryIndex) {
				this.KeyDictionaryProvider = new KeyDictionaryProvider(this.GetKeyDictionaryIndexPath(), configurations.EnabledKeyDictionaryTypes);
			}

			NLog.Default.Information($"Key dictionary provider is {(configurations.EnableKeyDictionaryIndex ? "enabled" : "disabled")}.");

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
			foreach(string directory in this.fileSystem.EnumerateDirectories(Path.GetDirectoryName(digestFolderPath))) {
				if(directory != digestFolderPath) {
					this.fileSystem.DeleteDirectory(directory, true);
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

					if(this.fileSystem.DirectoryExists(folderPath)) {
						this.fileSystem.DeleteDirectory(folderPath, true);
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

			FileExtensions.EnsureDirectoryStructure(dirName, this.fileSystem);

			FileExtensions.WriteAllBytes(digestHeaderFilepath, digestHeader, this.fileSystem);
		}

		public void SaveDigestChannelDescription(string digestFolderPath, BlockchainDigestDescriptor blockchainDigestDescriptor) {

			BlockchainDigestSimpleChannelSetDescriptor descriptor = DigestChannelSetFactory.ConvertToDigestSimpleChannelSetDescriptor(blockchainDigestDescriptor);

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			descriptor.Dehydrate(dehydrator);

			string digestDescFilePath = this.GetDigestChannelDescriptionFileName(digestFolderPath);

			this.fileSystem.WriteAllBytes(digestDescFilePath, dehydrator.ToArray().ToExactByteArray());
		}

		public SafeArrayHandle LoadDigestStandardKey(AccountId accountId, byte ordinal) {
			if(this.DigestChannelSet != null) {
				if(accountId.IsUser)
					return ((IStandardAccountKeysDigestChannel) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.UserAccountKeys]).GetKey(accountId.ToLongRepresentation(), ordinal).Branch();
				else if(accountId.IsServer)
					return ((IStandardAccountKeysDigestChannel) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ServerAccountKeys]).GetKey(accountId.ToLongRepresentation(), ordinal).Branch();
				else if(accountId.IsModerator)
					return ((IStandardAccountKeysDigestChannel) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ModeratorAccountKeys]).GetKey(accountId.ToLongRepresentation(), ordinal).Branch();

			}

			return null;
		}

		public int GetDigestHeaderSize(int digestId, string filename) {
			return (int) this.fileSystem.GetFileLength(filename);
		}

		public List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId) {

			var account = accountId.ToAccountId();
			if(account.IsUser)
				return ((IUserAccountKeysDigestChannel<IStandardAccountKeysDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.UserAccountKeys]).GetKeys(accountId).ToList();
			else if(account.IsServer)
				return ((IUserAccountKeysDigestChannel<IStandardAccountKeysDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ServerAccountKeys]).GetKeys(accountId).ToList();
			else if(account.IsModerator)
				return ((IUserAccountKeysDigestChannel<IStandardAccountKeysDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ModeratorAccountKeys]).GetKeys(accountId).ToList();

			throw null;
		}

		public IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType) {
			if(AccountId.IsStandardAccountType(accountType)) {
				return this.LoadDigestStandardAccount(accountSequenceId);
			}

			if(AccountId.IsJointAccountType(accountType)) {
				return this.LoadDigestJointAccount(accountSequenceId);
			}

			return null;
		}

		public IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId) {
			
			var account = accountId.ToAccountId();
			if(account.IsUser)
				return ((IAccountSnapshotDigestChannel<IStandardAccountSnapshotDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.UserAccountSnapshot]).GetAccount(accountId);
			else if(account.IsServer)
				return ((IAccountSnapshotDigestChannel<IStandardAccountSnapshotDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ServerAccountSnapshot]).GetAccount(accountId);
			else if(account.IsModerator)
				return ((IAccountSnapshotDigestChannel<IStandardAccountSnapshotDigestChannelCard>) this.DigestChannelSet.Channels[DigestChannelTypes.Instance.ModeratorAccountSnapshot]).GetAccount(accountId);

			throw null;
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

			if(!this.fileSystem.FileExists(filename)) {
				return null;
			}

			return this.fileSystem.GetFileLength(filename);

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

			using SafeArrayHandle genesisBytes = FileExtensions.ReadAllBytes(this.GetGenesisBlockFilename(), this.fileSystem);

			IDehydratedBlock genesisBlock = new DehydratedBlock();
			genesisBlock.Rehydrate(genesisBytes);

			return genesisBlock.GetEssentialDataChannels();

		}

		public void WriteGenesisBlock(ChannelsEntries<SafeArrayHandle> genesisBlockdata, List<(int offset, int length)> keyedOffsets) {

			// this is a special case, where we save it as a dehydrated block
			IDehydratedBlock genesisBlock = new DehydratedBlock();
			genesisBlock.Rehydrate(genesisBlockdata);

			this.fileSystem.CreateDirectory(this.GetGenesisBlockFolderPath());

			ChannelsEntries<SafeArrayHandle> dataChannels = genesisBlock.GetRawDataChannels();

			dataChannels[BlockChannelUtils.BlockChannelTypes.Keys] = this.PrepareIndexedTransactionData(keyedOffsets);

			dataChannels.RunForAll((band, data) => {

				FileExtensions.WriteAllBytes(this.GetGenesisBlockBandFilename(band.ToString()), data, this.fileSystem);
			});

			using SafeArrayHandle fullBytes = genesisBlock.Dehydrate();

			FileExtensions.WriteAllBytes(this.GetGenesisBlockFilename(), fullBytes, this.fileSystem);

			// and now the compressed
			BrotliCompression compressor = new BrotliCompression();
			FileExtensions.WriteAllBytes(this.GetGenesisBlockCompressedFilename(), compressor.Compress(fullBytes), this.fileSystem);

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
			if(!this.fileSystem.FileExists(filename)) {
				return null;
			}

			return SafeArrayHandle.WrapAndOwn(this.fileSystem.ReadAllBytes(filename));
		}

		public SafeArrayHandle LoadDigestBytes(int digestId, int offset, int length, string filename) {
			if(!this.fileSystem.FileExists(filename)) {
				return null;
			}

			return FileExtensions.ReadBytes(filename, offset, length, this.fileSystem);
		}

		public SafeArrayHandle LoadBlockPartialTransactionBytes(PublishedAddress keyAddress, (long index, long startingBlockId, long endingBlockId) blockIndex) {

			(int offset, int length) offsets = this.LoadBlockIndexedTransactionOffsets(keyAddress.AnnouncementBlockId.Value, blockIndex, keyAddress.IndexedTransactionIndex);

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
		public async Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)?> LoadAccountKeyFromIndex(AccountId accountId, byte ordinal) {

			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || (ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
				if(this.KeyDictionaryProvider == null) {
					return null;
				}

				return await this.KeyDictionaryProvider.LoadKeyFileAsync(accountId, ordinal, this.fileSystem).ConfigureAwait(false);
			}

			throw new InvalidOperationException($"Key ordinal ID must be either '{GlobalsService.TRANSACTION_KEY_ORDINAL_ID}' or '{GlobalsService.MESSAGE_KEY_ORDINAL_ID}'. Value '{ordinal}' provided.");
		}

		public bool TestKeyDictionaryPath() {

			return this.KeyDictionaryProvider?.Test() ?? true;
		}

		public Task<List<(AccountId accountId, SafeArrayHandle key, byte treeheight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)>> LoadKeyDictionary(List<(AccountId accountId, byte ordinal)> accountIdKeys, LockContext lockContext) {
			return this.KeyDictionaryProvider?.LoadKeyDictionary(accountIdKeys, lockContext) ?? Task.FromResult(new List<(AccountId accountId, SafeArrayHandle key, byte treeheight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)>());
		}

		public async Task SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupBits, byte ordinal) {
			if(this.KeyDictionaryProvider != null) {
				await this.KeyDictionaryProvider.WriteKey(accountId, key, treeHeight, hashType, backupBits, ordinal, this.fileSystem).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     ensure the base structure exists
		/// </summary>
		public void EnsureKeyDictionaryIndex() {
			foreach(var type in AccountId.StandardAccountTypes) {
				this.KeyDictionaryProvider?.EnsureBaseFileExists(this.fileSystem, type);

			}
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
				ChannelsEntries<int> genesisSize = this.LoadBlockSize(blockId, blockIndex);

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
						List<string> files = new List<string>();

						// try to erase the genesis files
						blockData.RunForAll((band, data) => {
							files.Add(this.GetGenesisBlockBandFilename(band.ToString()));
						});

						files.Add(this.GetGenesisBlockFilename());
						files.Add(this.GetGenesisBlockCompressedFilename());

						foreach(string name in files) {
							try {
								if(this.fileSystem.FileExists(name)) {
									this.fileSystem.DeleteFile(name);
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
				using SafeArrayHandle bytes = this.PrepareIndexedTransactionData(keyedOffsets);

				this.ChainScheduler.ScheduleWrite(indexer => {
					result = indexer.SaveBlockBytes(blockId, blockIndex, blockData, bytes);
				});

			}

			return result;
		}

		public BlockchainMessagesMetadata GetMessagesMetadata(string filename) {

			return JsonSerializer.Deserialize<BlockchainMessagesMetadata>(this.fileSystem.ReadAllText(filename));
		}

		public void InsertNextMessagesIndex(string filename) {

			BlockchainMessagesMetadata metadata = this.GetMessagesMetadata(filename);

			metadata.Counts.Add(metadata.Counts.Count + 1, 0);

			this.fileSystem.DeleteFile(filename);
			this.fileSystem.WriteAllText(filename, JsonSerializer.Serialize(metadata));
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

		public DigestChannelSet RecreateDigestChannelSet(string digestFolderPath, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor) {

			FileExtensions.EnsureDirectoryStructure(digestFolderPath, this.fileSystem);

			return DigestChannelSetFactory.CreateDigestChannelSet(digestFolderPath, blockchainDigestDescriptor.Channels.ToDictionary(e => e.Key, e => e.Value), this.blockchainDigestChannelFactory);

		}

		public (int offset, int length) LoadGenesisIndexedTransactionOffsets(int indexedTransactionIndex) {

			using SafeArrayHandle data = FileExtensions.ReadAllBytes(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.Keys.ToString()), this.fileSystem);

			return this.ExtractBlockIndexedTransactionOffsets(data, indexedTransactionIndex);

		}

		/// <summary>
		///     a special method to read a keyed transaction from a block
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockIndex"></param>
		/// <param name="indexedTransactionIndex"></param>
		/// <returns></returns>
		public (int offset, int length) LoadBlockIndexedTransactionOffsets(long blockId, (long index, long startingBlockId, long endingBlockId) blockIndex, int indexedTransactionIndex) {
			if(blockId == 1) {
				return this.LoadGenesisIndexedTransactionOffsets(indexedTransactionIndex);
			}

			SafeArrayHandle data = null;

			// get the block
			this.ChainScheduler.ScheduleRead(indexer => {
				data = indexer.QueryBlockIndexedTransactionOffsets(blockId, blockIndex, indexedTransactionIndex);
			});

			using(data) {
				return this.ExtractBlockIndexedTransactionOffsets(data, indexedTransactionIndex);
			}
		}

		protected (int offset, int length) ExtractBlockIndexedTransactionOffsets(SafeArrayHandle data, int indexedTransactionIndex) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			AdaptiveInteger2_5 numberWriter = new AdaptiveInteger2_5();
			numberWriter.Rehydrate(rehydrator);

			int count = (int) numberWriter.Value;

			if(count > 0) {

				numberWriter.Rehydrate(rehydrator);
				int offset = (int) numberWriter.Value;

				for(int i = 0; i <= indexedTransactionIndex; i++) {

					numberWriter.Rehydrate(rehydrator);
					int length = (int) numberWriter.Value;

					if(i == indexedTransactionIndex) {
						return (offset, length);
					}

					offset += length;
				}
			}

			return default;
		}

		protected SafeArrayHandle PrepareIndexedTransactionData(List<(int offset, int length)> keyedOffsets) {
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

			if(this.fileSystem.DirectoryExists(this.digestFolderPath)) {
				string digestDescFilePath = this.GetDigestChannelDescriptionFileName();

				if(this.fileSystem.FileExists(digestDescFilePath)) {
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

			byte[] bytes = this.fileSystem.ReadAllBytes(digestDescFilePath);

			BlockchainDigestSimpleChannelSetDescriptor descriptor = new BlockchainDigestSimpleChannelSetDescriptor();

			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes)) {
				descriptor.Rehydrate(rehydrator);
			}

			return descriptor;
		}

		public ChannelsEntries<SafeArrayHandle> LoadGenesisBytes(ChannelsEntries<(int offset, int length)> offsets) {

			ChannelsEntries<SafeArrayHandle> results = new ChannelsEntries<SafeArrayHandle>();

			offsets.RunForAll((band, bandOffsets) => {
				results[band] = FileExtensions.ReadBytes(this.GetGenesisBlockBandFilename(band.ToString()), bandOffsets.offset, bandOffsets.length, this.fileSystem);

			});

			return results;
		}

		public ChannelsEntries<int> LoadGenesisBlockSize() {

			ChannelsEntries<int> sizes = new ChannelsEntries<int>(this.enabledChannels);

			sizes.RunForAll((band, bandOffsets) => {
				sizes[band] = (int) this.fileSystem.GetFileLength(this.GetGenesisBlockBandFilename(band.ToString()));

			});

			return sizes;
		}

		public int LoadGenesisBlockHighHeaderSize() {

			return (int) this.fileSystem.GetFileLength(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.HighHeader.ToString()));
		}

		public int LoadGenesisBlockLowHeaderSize() {

			return (int) this.fileSystem.GetFileLength(this.GetGenesisBlockBandFilename(BlockChannelUtils.BlockChannelTypes.LowHeader.ToString()));
		}

		public int LoadGenesisBlockWholeHeaderSize() {

			return this.LoadGenesisBlockHighHeaderSize() + this.LoadGenesisBlockLowHeaderSize();
		}

		public string GetKeyDictionaryIndexPath() {
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
						foreach(IResourceAccessScheduler<BlockchainFiles> entry in indexSchedulers.Values) {
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