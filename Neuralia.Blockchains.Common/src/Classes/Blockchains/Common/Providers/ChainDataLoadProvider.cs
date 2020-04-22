using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;
using Zio;

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

		(List<int> sliceHashes, int hash)? BuildBlockSliceHashes(BlockId blockId, List<ChannelsEntries<(int offset, int length)>> slices);
		
		ValidatingDigestChannelSet CreateValidationDigestChannelSet(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor);

		IEnumerable<IBlock> LoadBlocks(IEnumerable<long> blockIds);

		IEnumerable<T> LoadBlocks<T>(IEnumerable<long> blockIds)
			where T : class, IBlock;

		IMasterTransaction LoadMasterTransaction(PublishedAddress keyAddress);
		SafeArrayHandle LoadDigestKey(KeyAddress keyAddress);
		SafeArrayHandle LoadDigestKey(AccountId accountId, byte ordinal);

		IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType);
		IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId);
		IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId);

		List<IStandardAccountKeysDigestChannelCard> LoadDigestStandardAccountKeyCards(long accountId);

		List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards();
		IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id);

		ChannelsEntries<SafeArrayHandle> LoadBlockData(long blockId);

		IBlockHeader LoadBlockHeader(long blockId);

		ChannelsEntries<SafeArrayHandle> LoadBlockSlice(BlockId blockId, ChannelsEntries<(int offset, int length)> offsets);
		ChannelsEntries<SafeArrayHandle> LoadBlockPartialData(long blockId, ChannelsEntries<(int offset, int length)> offsets);
		SafeArrayHandle LoadBlockPartialHighHeaderData(long blockId, int offset, int length);
		SafeArrayHandle LoadBlockPartialContentsData(long blockId, int offset, int length);

		ChannelsEntries<int> LoadBlockSize(long blockId);
		(ChannelsEntries<int> sizes, SafeArrayHandle hash)? LoadBlockSizeAndHash(long blockId, int hashOffset, int hashLength);

		int? LoadBlockHighHeaderSize(long blockId);
		int? LoadBlockLowHeaderSize(long blockId);
		int? LoadBlockWholeHeaderSize(long blockId);
		int? LoadBlockContentsSize(long blockId);

		long? GetBlockHighFileSize((long index, long startingBlockId, long endingBlockId) index);
		long? GetBlockLowFileSize((long index, long startingBlockId, long endingBlockId) index);
		long? GetBlockWholeFileSize((long index, long startingBlockId, long endingBlockId) index);
		long? GetBlockContentsFileSize((long index, long startingBlockId, long endingBlockId) index);

		long? GetMessagesFileSize(Guid uuid);
		int? GetMessagesFileCount(Guid uuid);

		(long index, long startingBlockId, long endingBlockId) FindBlockIndex(long blockId);
		bool TestFastKeysPath();

		SafeArrayHandle LoadBlockPartialTransactionBytes(long blockId, int offset, int length);
		Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)?> LoadAccountKeyFromIndex(AccountId accountId, byte ordinal);
		bool FastKeyEnabled(byte ordinal);
		Task<List<(IBlockEnvelope envelope, long xxHash)>> GetCachedUnvalidatedBlockGossipMessage(long blockId);
		Task<bool> GetUnvalidatedBlockGossipMessageCached(long blockId);
		Task<bool> CheckRegistryMessageInCache(long messagexxHash, bool validated);
		
		Dictionary<string, long> GetBlockFileSizes(long blockId);

		string LoadBlockJson(BlockId blockId);

		(ChannelsEntries<int> sizes, SafeArrayHandle hash)? GetBlockSizeAndHash(BlockId blockId);
		SafeArrayHandle LoadBlockHash(BlockId blockId);
		Dictionary<AccountId, SafeArrayHandle> LoadKeys(List<KeyAddress> keyAddresses);
		Dictionary<AccountId, ICryptographicKey> LoadFullKeys(List<KeyAddress> keyAddresses) ;
		Dictionary<AccountId, T> LoadFullKeys<T>(List<KeyAddress> keyAddresses) where T : class, ICryptographicKey;
		SafeArrayHandle LoadKey(KeyAddress keyAddress);
		ICryptographicKey LoadFullKey(KeyAddress keyAddress);
		T LoadFullKey<T>(KeyAddress keyAddress) where T : class, ICryptographicKey;
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

		public SafeArrayHandle LoadDigestKey(KeyAddress keyAddress) {
			return this.LoadDigestKey(keyAddress.DeclarationTransactionId.Account, keyAddress.OrdinalId);
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

		public Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)?> LoadAccountKeyFromIndex(AccountId accountId, byte ordinal) {
			return this.BlockchainEventSerializationFal.LoadAccountKeyFromIndex(accountId, ordinal);
		}

		public bool FastKeyEnabled(byte ordinal) {
			BlockChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			return configuration.EnableFastKeyIndex && (configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions) && ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID || configuration.EnabledFastKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages) && ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID);
		}

		public IMasterTransaction LoadMasterTransaction(PublishedAddress keyAddress) {

			IBlock cachedBlock = this.GetCachedBlock(keyAddress.AnnouncementBlockId.Value);

			if(cachedBlock != null) {
				IMasterTransaction transaction = cachedBlock.ConfirmedMasterTransactions.SingleOrDefault(t => t.TransactionId == keyAddress.DeclarationTransactionId);

				if(transaction != null) {
					return transaction;
				}
			}
			
			IMasterTransaction masterTransaction = null;

			lock(this.locker) {
				if(this.blocksCache.ContainsKey(keyAddress.AnnouncementBlockId.Value)) {
					IBlock block = this.blocksCache[keyAddress.AnnouncementBlockId.Value].block;

					masterTransaction = block.ConfirmedMasterTransactions.SingleOrDefault(t => t.TransactionId == keyAddress.DeclarationTransactionId);
				}
			}

			if(masterTransaction == null) {
				(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(keyAddress.AnnouncementBlockId.Value);

				SafeArrayHandle keyedBytes = this.BlockchainEventSerializationFal.LoadBlockPartialTransactionBytes(keyAddress, blockGroupIndex);

				if(keyedBytes != null && keyedBytes.HasData) {
					IBlockchainEventsRehydrationFactory rehydrationFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase;

					using IDataRehydrator keyedRehydrator = DataSerializationFactory.CreateRehydrator(keyedBytes);

					keyedRehydrator.ReadArraySize();

					DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
					dehydratedTransaction.Rehydrate(keyedRehydrator);

					masterTransaction = rehydrationFactory.CreateMasterTransaction(dehydratedTransaction);
					masterTransaction.Rehydrate(dehydratedTransaction, rehydrationFactory);

				}
			}

			// ensure the key address transaction comes from the key address account
			if(masterTransaction != null && !masterTransaction.TransactionId.Account.Equals(keyAddress.DeclarationTransactionId.Account)) {
				throw new InvalidOperationException("The keyed transaction loaded does not match the calling key address account");
			}

			return masterTransaction;
		}

		public SafeArrayHandle LoadBlockPartialTransactionBytes(long blockId, int offset, int length) {
			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

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

			if(result == null || result.Entries.Values.All(e => e == null || e.IsEmpty)) {
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
		
		public string LoadBlockJson(BlockId blockId) {
			IBlock block = this.LoadBlock(blockId);
		
			if(block == null) {
				return "";
			}
		
			return JsonUtils.SerializeJsonSerializable(block);
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

		/// <summary>
		///     a special optimization method to query the size and hash in a single call
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? GetBlockSizeAndHash(BlockId blockId) {

			(int offset, int length) hashOffsets = BlockHeader.GetBlockHashOffsets(blockId);

			return this.LoadBlockSizeAndHash(blockId.Value, hashOffsets.offset, hashOffsets.length);
		}
		
		public ChannelsEntries<SafeArrayHandle> LoadBlockSlice(BlockId blockId, ChannelsEntries<(int offset, int length)> offsets) {

			return this.LoadBlockPartialData(blockId.Value, offsets);
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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockPartialHighHeaderBytes(blockId, blockGroupIndex, offset, length);
		}

		public SafeArrayHandle LoadBlockPartialContentsData(long blockId, int offset, int length) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return SafeArrayHandle.Create(this.blocksCache[blockId].channels.ContentsData.Entry.Slice(offset, length));
				}
			}

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockHighHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.HighHeaderData.Length;
				}
			}

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockHighHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockLowHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.LowHeaderData.Length;
				}
			}

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockLowHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockWholeHeaderSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.HighHeaderData.Length + this.blocksCache[blockId].channels.LowHeaderData.Length;
				}
			}

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockWholeHeaderSize(blockId, blockGroupIndex);
		}

		public int? LoadBlockContentsSize(long blockId) {
			lock(this.locker) {
				if(this.blocksCache.ContainsKey(blockId)) {
					return this.blocksCache[blockId].channels.ContentsData.Length;
				}
			}

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockContentsSize(blockId, blockGroupIndex);
		}

		public long? GetBlockHighFileSize((long index, long startingBlockId, long endingBlockId) index) {

			return this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.HighHeader).HighHeaderData;
		}

		public long? GetBlockLowFileSize((long index, long startingBlockId, long endingBlockId) index) {

			return this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.LowHeader).LowHeaderData;
		}

		public long? GetBlockWholeFileSize((long index, long startingBlockId, long endingBlockId) index) {
			var results = this.BlockchainEventSerializationFal.GetBlockChannelFileSize(index, BlockChannelUtils.BlockChannelTypes.Headers);

			return results.HighHeaderData + results.LowHeaderData;
		}

		public long? GetBlockContentsFileSize((long index, long startingBlockId, long endingBlockId) index) {
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
		public (long index, long startingBlockId, long endingBlockId) FindBlockIndex(long blockId) {

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

			(long index, long startingBlockId, long endingBlockId) index = this.FindBlockIndex(blockId);

			string folderPath = this.BlockchainEventSerializationFal.GetBlockPath(index.index);

			var results = new Dictionary<string, long>();

			if(this.centralCoordinator.FileSystem.DirectoryExists(folderPath)) {
				foreach(string entry in this.centralCoordinator.FileSystem.EnumerateFiles(folderPath)) {
					long size = 0;

					if(this.centralCoordinator.FileSystem.FileExists(entry)) {
						size = this.centralCoordinator.FileSystem.GetFileLength(entry);
					}

					results.Add(Path.GetFileName(entry), size);
				}
			}

			return results;
		}

		public (List<int> sliceHashes, int hash)? BuildBlockSliceHashes(BlockId blockId, List<ChannelsEntries<(int offset, int length)>> slices) {

			List<int> sliceHashes = new List<int>();
			HashNodeList topNodes = new HashNodeList();
			
			foreach(var slice in slices) {
				var sliceInfo = this.LoadBlockPartialData(blockId.Value, slice);

				List<SafeArrayHandle> datas = new List<SafeArrayHandle>();
				sliceInfo.RunForAll((flag, data) => {
					datas.Add(data);
				});
				
				int sliceHash = HashingUtils.GenerateBlockDataSliceHash(datas);
				sliceHashes.Add(sliceHash);
				topNodes.Add(sliceHash);
			}
			 
			return (sliceHashes, HashingUtils.HashxxTree32(topNodes));
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
					foreach(BlockId entry in this.blocksCache.Keys.ToArray().OrderByDescending(k => k.Value).Skip(BLOCK_CACHE_SIZE)) {
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

			(long index, long startingBlockId, long endingBlockId) blockGroupIndex = this.FindBlockIndex(blockId);

			return this.BlockchainEventSerializationFal.LoadBlockHighHeaderData(blockId, blockGroupIndex);
		}

		public SafeArrayHandle LoadDigestData(int digestId) {

			return Compressors.DigestCompressor.Decompress(this.LoadDigestHeaderArchiveData(digestId));
		}

	#region Message Cache

		public Task<bool> CheckRegistryMessageInCache(long messagexxHash, bool validated) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();

			//TODO: must revise this below. caused by refactoring

			IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			return messageRegistryDal.CheckMessageInCache(messagexxHash, validated);
		}

		
		public Task<bool> GetUnvalidatedBlockGossipMessageCached(long blockId) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
			IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			lock(this.locker) {
				return messageRegistryDal.GetUnvalidatedBlockGossipMessageCached(blockId);
			}
		}

		public async Task<List<(IBlockEnvelope envelope, long xxHash)>> GetCachedUnvalidatedBlockGossipMessage(long blockId) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
			IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			List<long> messageHashes = await messageRegistryDal.GetCachedUnvalidatedBlockGossipMessage(blockId).ConfigureAwait(false);
			
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
		
		#region import from serialization manager
			
			public SafeArrayHandle LoadBlockHash(BlockId blockId) {
			IBlock cachedBlock = this.GetCachedBlock(blockId.Value);

			if(cachedBlock != null) {
				return cachedBlock.Hash;
			}

			(int offset, int length) hashOffsets = BlockHeader.GetBlockHashOffsets(blockId);

			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockPartialTransactionBytes(blockId.Value, hashOffsets.offset, hashOffsets.length);
		}

			public Dictionary<AccountId, SafeArrayHandle> LoadKeys(List<KeyAddress> keyAddresses) {
			var accountKeys = new Dictionary<AccountId, SafeArrayHandle>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadKey(keyAddress));
			}

			return accountKeys;
		}

		public Dictionary<AccountId, ICryptographicKey> LoadFullKeys(List<KeyAddress> keyAddresses) {
			var accountKeys = new Dictionary<AccountId, ICryptographicKey>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadFullKey(keyAddress));
			}

			return accountKeys;
		}

		public Dictionary<AccountId, T> LoadFullKeys<T>(List<KeyAddress> keyAddresses)
			where T : class, ICryptographicKey {
			var accountKeys = new Dictionary<AccountId, T>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadFullKey<T>(keyAddress));
			}

			return accountKeys;
		}

		public SafeArrayHandle LoadKey(KeyAddress keyAddress) {

			bool digestScope = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockWithinDigest(keyAddress.AnnouncementBlockId.Value);

			if(digestScope) {
				return this.LoadDigestKey(keyAddress);
			}

			//TODO: loading the entire keyset is not very efficient if we want only one key. optimize
			// lets load from the block
			IMasterTransaction masterTransaction = this.LoadMasterTransaction(keyAddress);

			if(masterTransaction is IKeyedTransaction keyedTransaction) {
				ICryptographicKey key = keyedTransaction.Keyset.Keys[keyAddress.OrdinalId];

				using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

				key.Dehydrate(dehydrator);

				return dehydrator.ToArray();

			}

			return null;
		}

		public ICryptographicKey LoadFullKey(KeyAddress keyAddress) {

			return this.LoadFullKey<ICryptographicKey>(keyAddress);
		}

		public T LoadFullKey<T>(KeyAddress keyAddress)
			where T : class, ICryptographicKey {

			bool digestScope = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockWithinDigest(keyAddress.AnnouncementBlockId.Value);

			if(digestScope) {
				SafeArrayHandle keyBytes = this.LoadDigestKey(keyAddress);

				return KeyFactory.RehydrateKey<T>(DataSerializationFactory.CreateRehydrator(keyBytes));
			}

			//TODO: loading the entire keyset is not very efficient if we want only one key. optimize
			// lets load from the block
			IMasterTransaction masterTransaction = this.LoadMasterTransaction(keyAddress);

			if(masterTransaction is IKeyedTransaction keyedTransaction) {

				return (T) keyedTransaction.Keyset.Keys[keyAddress.OrdinalId];
			}

			return null;
		}
			#endregion

	}

}