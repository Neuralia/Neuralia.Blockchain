using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainDataLoadProvider : IChainDataProvider {

		string GenesisFolderPath { get; }
		string DigestHashesPath { get; }
		IBlock LoadBlock(long blockId);
		IBlock LoadLatestBlock();
		
		(IBlock block, IDehydratedBlock dehydratedBlock) LoadBlockAndMetadata(long blockId);

		T LoadBlock<T>(long blockId)
			where T : class, IBlock;

		(T block, IDehydratedBlock dehydratedBlock) LoadBlockAndMetadata<T>(long blockId)
			where T : class, IBlock;

		IBlockchainDigest LoadDigestHeader(int digestId);

		SafeArrayHandle LoadDigestHeaderArchiveData(int digestId, int offset, int length);

		SafeArrayHandle LoadDigestHeaderArchiveData(int digestId);

		SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length);

		IBlock GetCachedBlock(long blockId);

		T LoadDigestHeader<T>(int digestId)
			where T : class, IBlockchainDigest;

		int GetDigestHeaderSize(int digestId);

		ValidatingDigestChannelSet CreateValidationDigestChannelSet(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor);

		IEnumerable<IBlock> LoadBlocks(IEnumerable<long> blockIds);

		IEnumerable<T> LoadBlocks<T>(IEnumerable<long> blockIds)
			where T : class, IBlock;

		IKeyedTransaction LoadKeyedTransaction(KeyAddress keyAddress);
		SafeArrayHandle LoadDigestKey(AccountId accountId, byte ordinal);

		IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType);
		IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId);
		IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId);

		List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId);

		List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards();
		IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id);

		ChannelsEntries<SafeArrayHandle> LoadBlockData(long blockId);

		IBlockHeader LoadBlockHeader(long blockId);

		ChannelsEntries<SafeArrayHandle> LoadBlockPartialData(long blockId, ChannelsEntries<(int offset, int length)> offsets);
		SafeArrayHandle LoadBlockPartialHighHeaderData(long blockId, int offset, int length);
		SafeArrayHandle LoadBlockPartialContentsData(long blockId, int offset, int length);

		ChannelsEntries<int> LoadBlockSize(long blockId);
		(ChannelsEntries<int> sizes, SafeArrayHandle hash)? LoadBlockSizeAndHash(long blockId, int hashOffset, int hashLength);

		int? LoadBlockHighHeaderSize(long blockId);
		int? LoadBlockLowHeaderSize(long blockId);
		int? LoadBlockWholeHeaderSize(long blockId);
		int? LoadBlockContentsSize(long blockId);

		long? GetBlockHighFileSize((int index, long startingBlockId) index);
		long? GetBlockLowFileSize((int index, long startingBlockId) index);
		long? GetBlockWholeFileSize((int index, long startingBlockId) index);
		long? GetBlockContentsFileSize((int index, long startingBlockId) index);

		long? GetMessagesFileSize(Guid uuid);
		int? GetMessagesFileCount(Guid uuid);

		(int index, long startingBlockId) FindBlockIndex(long blockId);
		bool TestFastKeysPath();

		SafeArrayHandle LoadBlockPartialTransactionBytes(long blockId, int offset, int length);
		(SafeArrayHandle keyBytes, byte treeheight, byte hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal);
		bool FastKeyEnabled(byte ordinal);
		List<(IBlockEnvelope envelope, long xxHash)> GetCachedUnvalidatedBlockGossipMessage(long blockId);
		bool GetUnvalidatedBlockGossipMessageCached(long blockId);

		Dictionary<string, long> GetBlockFileSizes(long blockId);
	}

	public interface IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainDataProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IChainDataLoadProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     The main provider for all data loading of chain events. This provider is ABSOLUTELY read only!
	/// </summary>
	public abstract class ChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainDataProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private const string INDEX_FILE = "index.db";
		private const string INDEX_ID_SYNC_FILE = "index-id-sync.db";

		private const string RELATIONSHIPS_FILE = "relationships.db";

		private const string QUARANTINE = "quarantine";

		public const int BLOCK_CACHE_SIZE = 10;

		/// <summary>
		///     a cache to store the latest blocks, for quick access since they may be requested often
		/// </summary>
		protected readonly ConcurrentDictionary<BlockId, (IBlock block, ChannelsEntries<SafeArrayHandle> channels, IDehydratedBlock dehydratedBlock)> blocksCache = new ConcurrentDictionary<BlockId, (IBlock block, ChannelsEntries<SafeArrayHandle> channels, IDehydratedBlock dehydratedBlock)>();

		protected ChainDataLoadProvider(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {
		}

		public SafeArrayHandle LoadDigestKey(AccountId accountId, byte ordinal) {
			return this.BlockchainEventSerializationFal.LoadDigestStandardKey(accountId, ordinal);
		}

		public IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType) {
			return this.BlockchainEventSerializationFal.LoadDigestAccount(accountSequenceId, accountType);
		}

		public IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId) {
			return this.BlockchainEventSerializationFal.LoadDigestStandardAccount(accountId);
		}

		public IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId) {
			return this.BlockchainEventSerializationFal.LoadDigestJointAccount(accountId);
		}

		public List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId) {
			return this.BlockchainEventSerializationFal.LoadDigestStandardAccountKeyCards(accountId);
		}

		public List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards() {
			return this.BlockchainEventSerializationFal.LoadDigestAccreditationCertificateCards();
		}

		public IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id) {
			return this.BlockchainEventSerializationFal.LoadDigestAccreditationCertificateCard(id);
		}

		public (SafeArrayHandle keyBytes, byte treeheight, byte hashBits)? LoadAccountKeyFromIndex(AccountId accountId, byte ordinal) {
			return this.BlockchainEventSerializationFal.LoadAccountKeyFromIndex(accountId, ordinal);
		}

		public bool FastKeyEnabled(byte ordinal) {
			BlockChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			return configuration.EnableFastKeyIndex && ((configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions) && (ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID)) || (configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages) && (ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID)));
		}

		public IKeyedTransaction LoadKeyedTransaction(KeyAddress keyAddress) {

			IKeyedTransaction keyedTransaction = null;

			lock(this.locker) {
				if(this.blocksCache.ContainsKey(keyAddress.AnnouncementBlockId.Value)) {
					IBlock block = this.blocksCache[keyAddress.AnnouncementBlockId.Value].block;

					keyedTransaction = block.ConfirmedKeyedTransactions.SingleOrDefault(t => t.TransactionId == keyAddress.DeclarationTransactionId);
				}
			}

			if(keyedTransaction == null) {
				(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(keyAddress.AnnouncementBlockId.Value);

				SafeArrayHandle keyedBytes = this.BlockchainEventSerializationFal.LoadBlockPartialTransactionBytes(keyAddress, blockGroupIndex);

				if((keyedBytes != null) && keyedBytes.HasData) {
					IBlockchainEventsRehydrationFactory rehydrationFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase;

					using(IDataRehydrator keyedRehydrator = DataSerializationFactory.CreateRehydrator(keyedBytes)) {

						keyedRehydrator.ReadArraySize();

						DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
						dehydratedTransaction.Rehydrate(keyedRehydrator);

						keyedTransaction = rehydrationFactory.CreateKeyedTransaction(dehydratedTransaction);
						keyedTransaction.Rehydrate(dehydratedTransaction, rehydrationFactory);
					}
				}
			}

			// ensure the key address transaction comes from the key address account
			if((keyedTransaction != null) && !keyedTransaction.TransactionId.Account.Equals(keyAddress.DeclarationTransactionId.Account)) {
				throw new InvalidOperationException("The keyed transaction loaded does not match the calling key address account");
			}

			return keyedTransaction;
		}

		public SafeArrayHandle LoadBlockPartialTransactionBytes(long blockId, int offset, int length) {
			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockPartialHighHeaderBytes(blockId, blockGroupIndex, offset, length);
		}

		/// <summary>
		///     Load all block data
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public ChannelsEntries<SafeArrayHandle> LoadBlockData(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockBytes(blockId, blockGroupIndex);
		}

		public (T block, IDehydratedBlock dehydratedBlock) LoadBlockAndMetadata<T>(long blockId)
			where T : class, IBlock {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					var entry = this.blocksCache[blockId];

					return ((T) entry.block, entry.dehydratedBlock);
				}
			}

			var result = this.LoadBlockData(blockId);

			if((result == null) || result.Entries.Values.All(e => (e == null) || e.IsEmpty)) {
				return default;
			}

			IDehydratedBlock dehydratedBlock = new DehydratedBlock();

			dehydratedBlock.Rehydrate(result);

			dehydratedBlock.RehydrateBlock(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, false);

			// store in memory for quick access when required
			this.CacheBlock(dehydratedBlock.RehydratedBlock, dehydratedBlock.GetEssentialDataChannels(), dehydratedBlock);

			return ((T) dehydratedBlock.RehydratedBlock, dehydratedBlock);
		}

		public T LoadBlock<T>(long blockId)
			where T : class, IBlock {

			return this.LoadBlockAndMetadata<T>(blockId).block;
		}

		public IBlock LoadBlock(long blockId) {
			return this.LoadBlock<IBlock>(blockId);
		}

		public IBlock LoadLatestBlock() {
			return this.LoadBlock(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight);
		}

		public (IBlock block, IDehydratedBlock dehydratedBlock) LoadBlockAndMetadata(long blockId) {
			return this.LoadBlockAndMetadata<IBlock>(blockId);
		}

		public IBlockHeader LoadBlockHeader(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					var entry = this.blocksCache[blockId];

					return entry.block;
				}
			}

			SafeArrayHandle result = this.LoadBlockHighHeaderData(blockId);

			if(result == null) {
				return null;
			}

			return DehydratedBlock.RehydrateBlockHeader(result, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
		}

		public IBlockchainDigest LoadDigestHeader(int digestId) {
			return this.LoadDigestHeader<IBlockchainDigest>(digestId);
		}

		public SafeArrayHandle LoadDigestHeaderArchiveData(int digestId) {
			return this.BlockchainEventSerializationFal.LoadDigestBytes(digestId, this.GetDigestsHeaderFilePath(digestId));
		}

		public SafeArrayHandle LoadDigestHeaderArchiveData(int digestId, int offset, int length) {
			return this.BlockchainEventSerializationFal.LoadDigestBytes(digestId, offset, length, this.GetDigestsHeaderFilePath(digestId));
		}

		public SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length) {
			return this.BlockchainEventSerializationFal.LoadDigestFile(channelId, indexId, fileId, partIndex, offset, length);
		}

		public T LoadDigestHeader<T>(int digestId)
			where T : class, IBlockchainDigest {

			SafeArrayHandle result = this.LoadDigestData(digestId);

			if(result == null) {
				return null;
			}

			IDehydratedBlockchainDigest dehydratedDigest = new DehydratedBlockchainDigest();

			dehydratedDigest.Rehydrate(result);

			dehydratedDigest.RehydrateDigest(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

			return (T) dehydratedDigest.RehydratedDigest;
		}

		public int GetDigestHeaderSize(int digestId) {
			return this.BlockchainEventSerializationFal.GetDigestHeaderSize(digestId, this.GetDigestsHeaderFilePath(digestId));
		}

		public ValidatingDigestChannelSet CreateValidationDigestChannelSet(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor) {
			return DigestChannelSetFactory.CreateValidatingDigestChannelSet(this.GetDigestsScoppedFolderPath(digestId), blockchainDigestDescriptor, this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.CreateDigestChannelfactory());
		}

		public IEnumerable<IBlock> LoadBlocks(IEnumerable<long> blockIds) {
			return this.LoadBlocks<IBlock>(blockIds);
		}

		public IEnumerable<T> LoadBlocks<T>(IEnumerable<long> blockIds)
			where T : class, IBlock {
			var blocks = new List<T>();

			foreach(long blockId in blockIds) {
				blocks.Add(this.LoadBlock<T>(blockId));
			}

			return blocks;
		}

		public ChannelsEntries<SafeArrayHandle> LoadBlockPartialData(long blockId, ChannelsEntries<(int offset, int length)> offsets) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					var entry = this.blocksCache[blockId];

					var results = new ChannelsEntries<SafeArrayHandle>();

					offsets.RunForAll((channel, channelOffsets) => {
						results[channel] = SafeArrayHandle.Create(entry.channels[channel].Entry.Slice(channelOffsets.offset, channelOffsets.length));
					});

					return results;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockPartialBytes(blockId, blockGroupIndex, offsets);
		}

		public string GenesisFolderPath => this.BlockchainEventSerializationFal.GenesisFolderPath;
		public string DigestHashesPath => this.GetDigestsHashesFolderPath();

		public SafeArrayHandle LoadBlockPartialHighHeaderData(long blockId, int offset, int length) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return SafeArrayHandle.Create(this.blocksCache[blockId].channels.HighHeaderData.Entry.Slice(offset, length));
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockPartialHighHeaderBytes(blockId, blockGroupIndex, offset, length);
		}

		public SafeArrayHandle LoadBlockPartialContentsData(long blockId, int offset, int length) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return SafeArrayHandle.Create(this.blocksCache[blockId].channels.ContentsData.Entry.Slice(offset, length));
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockPartialContentsBytes(blockId, blockGroupIndex, offset, length);
		}

		public IBlock GetCachedBlock(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].block;
				}
			}

			return null;
		}

		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? LoadBlockSizeAndHash(long blockId, int hashOffset, int hashLength) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {

					var results = new ChannelsEntries<int>();

					this.blocksCache[blockId].channels.RunForAll((channel, data) => {
						results[channel] = data.Length;
					});

					return (results, this.blocksCache[blockId].block.Hash);
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockSizeAndHash(blockId, blockGroupIndex, hashOffset, hashLength);
		}

		public ChannelsEntries<int> LoadBlockSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {

					var results = new ChannelsEntries<int>();

					this.blocksCache[blockId].channels.RunForAll((channel, data) => {
						results[channel] = data.Length;
					});

					return results;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockHighHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.HighHeaderData.Length;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockHighHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockLowHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.LowHeaderData.Length;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockLowHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockWholeHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.HighHeaderData.Length + this.blocksCache[blockId].channels.LowHeaderData.Length;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockWholeHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockContentsSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.ContentsData.Length;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockContentsSize(blockId, blockGroupIndex);
		}

		public long? GetBlockHighFileSize((int index, long startingBlockId) index) {

			return this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.HighHeader).HighHeaderData;
		}

		public long? GetBlockLowFileSize((int index, long startingBlockId) index) {

			return this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.LowHeader).LowHeaderData;
		}

		public long? GetBlockWholeFileSize((int index, long startingBlockId) index) {
			var results = this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.Headers);

			return results.HighHeaderData + results.LowHeaderData;
		}

		public long? GetBlockContentsFileSize((int index, long startingBlockId) index) {
			return this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.Contents).ContentsData;
		}

		public long? GetMessagesFileSize(Guid uuid) {
			lock(this.locker) {
				string filename = this.GetMessagesFile(uuid);

				return this.BlockchainEventSerializationFal.GetFileSize(filename);
			}
		}

		public int? GetMessagesFileCount(Guid uuid) {
			lock(this.locker) {

				BlockchainEventSerializationFal.BlockchainMessagesMetadata metadata = this.GetMessagesMetadata(uuid);

				if(metadata.Counts.Count == 0) {
					return 0;
				}

				// get the lateest entry
				return metadata.Counts[metadata.Counts.Count];
			}
		}

		/// <summary>
		///     Determine where the block falls in the split blocks files
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public (int index, long startingBlockId) FindBlockIndex(long blockId) {

			if(blockId <= 0) {
				throw new ApplicationException("Block Id must be 1 or more.");
			}

			return IndexCalculator.ComputeIndex(blockId, this.BlockGroupingConfig.GroupingCount);
		}

		public bool TestFastKeysPath() {
			return this.BlockchainEventSerializationFal.TestFastKeysPath();
		}
		
		
		/// <summary>
		///     Return the sizes of all the files inside the block index
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public Dictionary<string, long> GetBlockFileSizes(long blockId) {

			(int index, long startingBlockId) index = this.FindBlockIndex(blockId);

			string folderPath = this.BlockchainEventSerializationFal.GetBlockPath(index.index);

			var results = new Dictionary<string, long>();

			if(this.centralCoordinator.FileSystem.Directory.Exists(folderPath)) {
				foreach(string entry in this.centralCoordinator.FileSystem.Directory.GetFiles(folderPath)) {
					long size = 0;

					if(this.centralCoordinator.FileSystem.File.Exists(entry)) {
						size = this.centralCoordinator.FileSystem.FileInfo.FromFileName(entry).Length;
					}

					results.Add(Path.GetFileName(entry), size);
				}
			}

			return results;
		}

		/// <summary>
		///     insert a block into our memory cache. keep only 10 entries
		/// </summary>
		/// <param name="block"></param>
		protected void CacheBlock(IBlock block, ChannelsEntries<SafeArrayHandle> channels, IDehydratedBlock dehydratedBlock) {
			lock(this.locker) {
				if(!this.blocksCache.ContainsKey(block.BlockId)) {
					this.blocksCache.AddSafe(block.BlockId, (block, channels, dehydratedBlock));
				}

				if(this.blocksCache.Count > BLOCK_CACHE_SIZE) {
					foreach(BlockId entry in this.blocksCache.Keys.OrderByDescending(k => k.Value).Skip(BLOCK_CACHE_SIZE)) {
						this.blocksCache.RemoveSafe(entry);
					}
				}
			}
		}

		public SafeArrayHandle LoadBlockHighHeaderData(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.HighHeaderData;
				}
			}

			(int index, long startingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockHighHeaderData(blockId, blockGroupIndex);
		}

		public SafeArrayHandle LoadDigestData(int digestId) {

			return Compressors.DigestCompressor.Decompress(this.LoadDigestHeaderArchiveData(digestId));
		}

	#region Message Cache

		public bool GetUnvalidatedBlockGossipMessageCached(long blockId) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
			IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			lock(this.locker) {
				return messageRegistryDal.GetUnvalidatedBlockGossipMessageCached(blockId);
			}
		}

		public List<(IBlockEnvelope envelope, long xxHash)> GetCachedUnvalidatedBlockGossipMessage(long blockId) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
			IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			List<long> messageHashes = null;

			lock(this.locker) {
				messageHashes = messageRegistryDal.GetCachedUnvalidatedBlockGossipMessage(blockId);
			}

			string folderPath = this.GetBlocksGossipCacheFolderPath();
			FileExtensions.EnsureDirectoryStructure(folderPath, this.centralCoordinator.FileSystem);

			var results = new List<(IBlockEnvelope envelope, long xxHash)>();

			if(messageHashes.Any()) {

				foreach(long xxHash in messageHashes) {
					string completeFile = this.GetUnvalidatedBlockGossipMessageFullFileName(blockId, xxHash);

					try {
						SafeArrayHandle bytes = FileExtensions.ReadAllBytes(completeFile, this.centralCoordinator.FileSystem);

						IBlockEnvelope envelope = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<IBlockEnvelope>(bytes);

						if(envelope != null) {
							results.Add((envelope, xxHash));
						}
					} catch(Exception ex) {
						Log.Error(ex, "Failed to load a cached gossip block message");
					}
				}
			}

			return results;
		}

	#endregion

	}

}