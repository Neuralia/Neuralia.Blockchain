using System;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public static class ChainDataProvider {
		public enum BlockFilesetSyncManifestStatuses {
			None,
			InProgress,
			Completed
		}
	}

	public interface IChainDataProvider: IChainProvider {

		string GetDigestSyncManifestFileName();

		string GetBlockSyncManifestFileName(BlockId blockId);
		string GetBlockSyncManifestCompletedFileName(BlockId blockId);
		string GetBlockSyncManifestCacheFolder(BlockId blockId);
		ChainDataProvider.BlockFilesetSyncManifestStatuses GetBlockSyncManifestStatus(BlockId blockId);

		string GetDigestFileSyncManifestFileName();

		string GetBlocksGossipCacheFolderPath();
		string GetBlocksCacheFolderPath();

		string GetBlocksFolderPath();

		string GetGeneralCachePath();
	}

	public interface IChainDataProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainDataProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     The base class for the data access providers
	/// </summary>
	public abstract class ChainDataProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainDataProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public const string EVENTS_FOLDER_NAME = "store";
		public const string BLOCKS_FOLDER_NAME = "blocks";

		public const string GENERAL_ACHE_FOLDER_NAME = "caches";
		public const string BLOCKS_CACHE_FOLDER_NAME = "caches";
		public const string GOSSIP_CACHE_FOLDER_NAME = "gossip";
		public const string SYNC_CACHE_FOLDER_NAME = "sync";

		public const string BLOCKS_CONFIG_FILE_NAME = "config";
		public const string MESSAGES_CONFIG_FILE_NAME = "config";

		private const string BLOCK_SYNC_MANIFEST_FILE = "block-sync-manifest.json";
		private const string BLOCK_SYNC_MANIFEST_COMPLETED_FILE = "block-sync-manifest-completed.mark";

		private const string DIGEST_SYNC_MANIFEST_FILE = "digest-sync-manifest.json";
		private const string DIGEST_FILE_SYNC_MANIFEST_FILE = "digest-files-sync-manifest.json";

		public const string DIGESTS_FOLDER_NAME = "digests";
		public const string DIGESTS_SCOPED_FOLDER_NAME = "digest-{0}";
		public const string DIGESTS_HEADER_FILE_NAME = "digest-{0}.neuralia";
		public const string DIGEST_HASH_FOLDER_PATH = "hashes";

		public const string BLOCKS_FILE_NAME = "blocks.neuralia";
		public const string BLOCKS_CONTENTS_FILE_NAME = "blocks.contents.neuralia";

		public const string BLOCKS_INDEX_L1_FILE_NAME = "blocks.l1.index";
		public const string BLOCKS_INDEX_L2_FILE_NAME = "blocks.l2.index";
		public const string BLOCKS_INDEX_L3_FILE_NAME = "blocks.l3.index";

		public const string MESSAGES_FOLDER_NAME = "messages";

		public const string MESSAGES_FILE_NAME = "messages.{0}.neuralia";
		public const string MESSAGES_INDEX_FILE_NAME = "messages.{0}.index";
		public const string MESSAGES_METADATA_FILE_NAME = "messages.meta";

		public const int BLOCKS_COUNT_PER_GROUP = 1000;
		public const int MESSAGE_COUNT_PER_GROUP = 10_000;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		protected readonly IGuidService guidService;

		protected readonly object locker = new object();

		protected readonly ITimeService timeService;

		private IBlockchainEventSerializationFalReadonly blockchainEventSerializationFal;

		private BlockGroupingConfigs? blockGroupingConfig;

		protected IFileSystem fileSystem;

		private MessageGroupingConfigs? messageGroupingConfig;

		public ChainDataProvider(CENTRAL_COORDINATOR centralCoordinator) {

			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
			this.centralCoordinator = centralCoordinator;
			this.fileSystem = centralCoordinator.FileSystem;
		}

		protected IBlockchainEventSerializationFalReadonly BlockchainEventSerializationFal {
			get {
				if(this.blockchainEventSerializationFal == null) {

					int currentDigestHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.DigestHeight;

					this.blockchainEventSerializationFal = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainDalCreationFactoryBase.CreateSerializedArchiveFal(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration(), this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels, this.GetBlocksFolderPath(), this.GetDigestsScoppedFolderPath(currentDigestHeight), this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.CreateDigestChannelfactory(), this.fileSystem);
				}

				return this.blockchainEventSerializationFal;
			}
		}

		protected BlockGroupingConfigs BlockGroupingConfig {
			get {
				if(this.blockGroupingConfig == null) {

					string blocksConfigFile = this.GetBlocksGroupingConfigPath();
					FileExtensions.EnsureDirectoryStructure(this.GetBlocksFolderPath(), this.centralCoordinator.FileSystem);

					if(this.centralCoordinator.FileSystem.File.Exists(blocksConfigFile)) {

						IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(FileExtensions.ReadAllBytes(blocksConfigFile, this.centralCoordinator.FileSystem));

						this.blockGroupingConfig = new BlockGroupingConfigs {GroupingCount = rehydrator.ReadInt()};

					} else {
						ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();

						int groupingCount = chainConfiguration.BlockFileGroupSize ?? BLOCKS_COUNT_PER_GROUP;
						this.blockGroupingConfig = new BlockGroupingConfigs {GroupingCount = groupingCount};

						IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
						dehydrator.Write(this.blockGroupingConfig.Value.GroupingCount);

						SafeArrayHandle bytes = dehydrator.ToArray();

						FileExtensions.WriteAllBytes(blocksConfigFile, bytes, this.centralCoordinator.FileSystem);

						bytes.Return();
					}
				}

				return this.blockGroupingConfig.Value;
			}
		}

		protected MessageGroupingConfigs MessageGroupingConfig {
			get {
				if(this.messageGroupingConfig == null) {

					string messagesConfigFile = this.GetMessageGroupingConfigPath();

					FileExtensions.EnsureDirectoryStructure(this.GetMessagesFolderPath(), this.centralCoordinator.FileSystem);

					if(this.centralCoordinator.FileSystem.File.Exists(messagesConfigFile)) {
						IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(FileExtensions.ReadAllBytes(messagesConfigFile, this.centralCoordinator.FileSystem));

						this.messageGroupingConfig = new MessageGroupingConfigs {GroupingCount = rehydrator.ReadInt()};
					} else {
						ChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();

						int groupingCount = chainConfiguration.MessageFileGroupSize ?? MESSAGE_COUNT_PER_GROUP;
						this.messageGroupingConfig = new MessageGroupingConfigs {GroupingCount = groupingCount};

						IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
						dehydrator.Write(this.messageGroupingConfig.Value.GroupingCount);

						SafeArrayHandle bytes = dehydrator.ToArray();

						FileExtensions.WriteAllBytes(messagesConfigFile, bytes, this.centralCoordinator.FileSystem);

						bytes.Return();
					}
				}

				return this.messageGroupingConfig.Value;
			}
		}

		protected struct BlockGroupingConfigs {
			public int GroupingCount { get; set; }

		}

		protected struct MessageGroupingConfigs {
			public int GroupingCount { get; set; }

		}

	#region Block Utility methods

	#endregion

	#region Path methods

		public string GetWalletPath() {

			return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainDirectoryPath();
		}

		public string GetEventsFolderPath() {

			return Path.Combine(this.GetWalletPath(), EVENTS_FOLDER_NAME);
		}

		public string GetGeneralCachePath() {
			return Path.Combine(this.GetEventsFolderPath(), GENERAL_ACHE_FOLDER_NAME);
		}

		public string GetBlocksFolderPath() {

			return Path.Combine(this.GetEventsFolderPath(), BLOCKS_FOLDER_NAME);
		}

		public string GetBlocksGroupingConfigPath() {

			return Path.Combine(this.GetBlocksFolderPath(), BLOCKS_CONFIG_FILE_NAME);
		}

		public string GetBlocksCacheFolderPath() {

			return Path.Combine(this.GetBlocksFolderPath(), BLOCKS_CACHE_FOLDER_NAME);
		}

		public string GetBlocksGossipCacheFolderPath() {

			return Path.Combine(this.GetBlocksCacheFolderPath(), GOSSIP_CACHE_FOLDER_NAME);
		}

		public string GetBlocksSyncCacheFolderPath() {

			return Path.Combine(this.GetBlocksCacheFolderPath(), SYNC_CACHE_FOLDER_NAME);
		}

		public string GetDigestsFolderPath() {

			return Path.Combine(this.GetEventsFolderPath(), DIGESTS_FOLDER_NAME);
		}

		public string GetDigestsHashesFolderPath() {

			return Path.Combine(this.GetEventsFolderPath(), DIGEST_HASH_FOLDER_PATH);
		}

		public string GetDigestsScoppedFolderPath(int digestId) {

			return Path.Combine(this.GetDigestsFolderPath(), string.Format(DIGESTS_SCOPED_FOLDER_NAME, digestId));
		}

		public string GetDigestsPackedFolderPath(int digestId) {

			return Path.Combine(this.GetDigestsScoppedFolderPath(digestId), DigestChannelBandFileNamingProvider.ARCHIVE_DIRECTORY_NAME);
		}

		public string GetDigestsHeaderFilePath(int digestId) {

			return Path.Combine(this.GetDigestsPackedFolderPath(digestId), string.Format(DIGESTS_HEADER_FILE_NAME, digestId));
		}

		public string GetDigestsExpandedFolderPath(int digestId) {

			return Path.Combine(this.GetDigestsScoppedFolderPath(digestId), DigestChannelBandFileNamingProvider.EXPANDED_DIRECTORY_NAME);
		}

		public string GetDigestSyncManifestFileName() {
			return Path.Combine(this.GetDigestsFolderPath(), DIGEST_SYNC_MANIFEST_FILE);
		}

		public string GetBlockSyncManifestCacheFolder(BlockId blockId) {

			return Path.Combine(this.GetBlocksSyncCacheFolderPath(), blockId.ToString());
		}

		public string GetBlockSyncManifestFileName(BlockId blockId) {

			return Path.Combine(this.GetBlockSyncManifestCacheFolder(blockId), BLOCK_SYNC_MANIFEST_FILE);
		}

		public string GetBlockSyncManifestCompletedFileName(BlockId blockId) {

			return Path.Combine(this.GetBlockSyncManifestCacheFolder(blockId), BLOCK_SYNC_MANIFEST_COMPLETED_FILE);
		}

		public ChainDataProvider.BlockFilesetSyncManifestStatuses GetBlockSyncManifestStatus(BlockId blockId) {

			string path = this.GetBlockSyncManifestCompletedFileName(blockId);

			if(this.centralCoordinator.FileSystem.File.Exists(path)) {
				return ChainDataProvider.BlockFilesetSyncManifestStatuses.Completed;
			}

			path = this.GetBlockSyncManifestFileName(blockId);

			if(this.centralCoordinator.FileSystem.File.Exists(path)) {
				return ChainDataProvider.BlockFilesetSyncManifestStatuses.InProgress;
			}

			return ChainDataProvider.BlockFilesetSyncManifestStatuses.None;
		}

		public string GetDigestFileSyncManifestFileName() {
			return Path.Combine(this.GetDigestsFolderPath(), DIGEST_FILE_SYNC_MANIFEST_FILE);
		}

		protected string GetUnvalidatedBlockGossipMessageFullFileName(long blockId, long xxHash) {
			return Path.Combine(this.GetBlocksGossipCacheFolderPath(), this.GetUnvalidatedBlockGossipMessageFileName(blockId, xxHash));
		}

		protected string GetUnvalidatedBlockGossipMessageFileName(long blockId, long xxHash) {
			return $"{blockId}-{(ulong) xxHash}.cache";
		}

		// messages
		public string GetMessagesFolderPath() {

			return Path.Combine(this.GetEventsFolderPath(), MESSAGES_FOLDER_NAME);
		}

		public string GetMessageGroupingConfigPath() {

			return Path.Combine(this.GetMessagesFolderPath(), MESSAGES_CONFIG_FILE_NAME);
		}

		public string GetMessageTimestampFolderPath(Guid uuid) {

			return Path.Combine(this.GetEventsFolderPath(), this.guidService.GetTimestamp(uuid).ToString(@"yyyy\/MM\/dd/HH/"));
		}

		public BlockchainEventSerializationFal.BlockchainMessagesMetadata GetMessagesMetadata(Guid uuid) {

			string filename = this.GetMessagesMetadataFile(uuid);

			return this.BlockchainEventSerializationFal.GetMessagesMetadata(filename);
		}

		public int GetMessagesLatestIndex(Guid uuid) {
			BlockchainEventSerializationFal.BlockchainMessagesMetadata metadata = this.GetMessagesMetadata(uuid);

			if(metadata.Counts.Count == 0) {
				return 1; // the first index in case of nothing
			}

			return metadata.Counts.Count;
		}

		public string GetMessagesFile(Guid uuid, int index) {

			return Path.Combine(this.GetMessageTimestampFolderPath(uuid), string.Format(MESSAGES_FILE_NAME, index));
		}

		public string GetMessagesFile(Guid uuid) {

			// get the latest index
			return this.GetMessagesFile(uuid, this.GetMessagesLatestIndex(uuid));
		}

		public string GetMessagesMetadataFile(Guid uuid) {

			return Path.Combine(this.GetMessageTimestampFolderPath(uuid), MESSAGES_METADATA_FILE_NAME);
		}

		public string GetMessagesIndexFile(Guid uuid, int index) {

			return Path.Combine(this.GetMessageTimestampFolderPath(uuid), string.Format(MESSAGES_INDEX_FILE_NAME, index));
		}

		public string GetMessagesIndexFile(Guid uuid) {

			// get the latest index
			return this.GetMessagesIndexFile(uuid, this.GetMessagesLatestIndex(uuid));
		}

	#endregion

	}
}