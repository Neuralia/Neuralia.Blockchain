using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainStateProvider : IChainStateEntryFields, IChainProvider, IDisposableExtended {
		public bool IsChainLikelySynchronized { get; }
		public bool IsChainSynced { get; }
		public bool IsChainDesynced { get; }

		public void InsertModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key);
		public void UpdateModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key);
		public ICryptographicKey GetModeratorKey(byte keyId);

		public T GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey;

		public SafeArrayHandle GetModeratorKeyBytes(byte keyId);

		public Enums.ChainSyncState GetChainSyncState();

		public bool BlockWithinDigest(long blockId);

		public string GetBlocksIdFilePath();

		public void ResetChainState();

		Task SetChainInception(DateTime value);
		Task SetLastBlockHash(byte[] value);
		Task SetLastBlockTimestamp(DateTime value);
		Task SetLastBlockLifespan(ushort value);
		Task SetBlockInterpretationStatus(ChainStateEntryFields.BlockInterpretationStatuses value);
		Task SetGenesisBlockHash(byte[] value);
		Task SetBlockHeight(long value);
		Task SetDiskBlockHeight(long value);
		Task SetDownloadBlockHeight(long value);
		Task SetPublicBlockHeight(long value);
		Task SetDigestHeight(int value);
		Task SetDigestBlockHeight(long value);
		Task SetLastDigestHash(byte[] value);
		Task SetLastDigestTimestamp(DateTime value);
		Task SetPublicDigestHeight(int value);
		Task SetLastSync(DateTime value);
		Task SetMaximumVersionAllowed(string value);
		Task SetMinimumWarningVersionAllowed(string value);
		Task SetMinimumVersionAllowed(string value);
		Task SetMaxBlockInterval(int value);
		Task SetAllowGossipPresentations(bool value);
		Task SetMiningPassword(long value);
		Task SetMiningAutograph(byte[] value);
		Task SetLastMiningRegistrationUpdate(DateTime? value);

		Task UpdateFields(IEnumerable<Func<IChainStateProvider, string>> actions);

		string SetChainInceptionField(DateTime value);
		string SetLastBlockHashField(byte[] value);
		string SetLastBlockTimestampField(DateTime value);
		string SetLastBlockLifespanField(ushort value);
		string SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses value);
		string SetGenesisBlockHashField(byte[] value);
		string SetBlockHeightField(long value);
		string SetDiskBlockHeightField(long value);
		string SetDownloadBlockHeightField(long value);
		string SetPublicBlockHeightField(long value);
		string SetDigestHeightField(int value);
		string SetDigestBlockHeightField(long value);
		string SetLastDigestHashField(byte[] value);
		string SetLastDigestTimestampField(DateTime value);
		string SetPublicDigestHeightField(int value);
		string SetLastSyncField(DateTime value);
		string SetMaximumVersionAllowedField(string value);
		string SetMinimumWarningVersionAllowedField(string value);
		string SetMinimumVersionAllowedField(string value);
		string SetMaxBlockIntervalField(int value);
		string SetAllowGossipPresentationsField(bool value);
		string SetMiningPasswordField(long value);
		string SetMiningAutographField(byte[] value);
		string SetLastMiningRegistrationUpdateField(DateTime? value);
	}

	public interface IChainStateProvider<CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT> : IChainStateProvider
		where CHAIN_STATE_DAL : IChainStateDal
		where CHAIN_STATE_CONTEXT : class, IChainStateContext {

	}

	public interface IChainStateProvider<CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateProvider<CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT>
		where CHAIN_STATE_DAL : IChainStateDal
		where CHAIN_STATE_CONTEXT : class, IChainStateContext
		where CHAIN_STATE_SNAPSHOT : class, IChainStateEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
	}

	/// <summary>
	///     A provider that offers the chain state parameters from the DB
	/// </summary>
	/// <typeparam name="CHAIN_STATE_DAL"></typeparam>
	/// <typeparam name="CHAIN_STATE_CONTEXT"></typeparam>
	/// <typeparam name="CHAIN_STATE_ENTRY"></typeparam>
	public abstract class ChainStateProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateProvider<CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_STATE_DAL : class, IChainStateDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_CONTEXT : class, IChainStateContext
		where CHAIN_STATE_SNAPSHOT : class, IChainStateEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new()
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new() {

		private const string BLOCKS_ID_FILE = "block.id";

		private readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly string folderPath;

		private readonly object locker = new object();

		private const int MAX_CONCURRENT_READERS = 9;
		/// <summary>
		/// allow 3 parallel readers
		/// </summary>
		private readonly SemaphoreSlim semaphore = new SemaphoreSlim(MAX_CONCURRENT_READERS);

		protected readonly ITimeService timeService;
		private CHAIN_STATE_DAL chainStateDal;

		protected (CHAIN_STATE_SNAPSHOT entry, bool full)? chainStateEntry;

		public ChainStateProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
		}

		protected bool IsMaster => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType == AppSettingsBase.SerializationTypes.Master;

		private CHAIN_STATE_DAL ChainStateDal {
			get {

				lock(this.locker) {
					if(this.chainStateDal == null) {
						this.chainStateDal = this.centralCoordinator.ChainDalCreationFactory.CreateChainStateDal<CHAIN_STATE_DAL, CHAIN_STATE_SNAPSHOT>(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);

						// make sure we tell it how to create a new entry from our casted children
						this.chainStateDal.CreateNewEntry = this.CreateNewEntry;
					}
				}

				return this.chainStateDal;
			}
		}

		public string GetBlocksIdFilePath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlocksFolderPath(), BLOCKS_ID_FILE);
		}

		/// <summary>
		/// make sure the chain state will be requeried.
		/// </summary>
		public void ResetChainState() {
			this.chainStateEntry = null;
		}

		public DateTime ChainInception {
			get { return this.GetField(entry => DateTime.SpecifyKind(entry.ChainInception, DateTimeKind.Utc)); }
			set => this.SetChainInception(value).Wait();
		}

		public string SetChainInceptionField(DateTime value) {
			this.chainStateEntry.Value.entry.ChainInception = value;

			return nameof(this.chainStateEntry.Value.entry.ChainInception);
		}

		public Task SetChainInception(DateTime value) {
			return this.UpdateFields(prov => prov.SetChainInceptionField(value));
		}

		public byte[] LastBlockHash {
			get { return this.GetField(entry => entry.LastBlockHash); }
			set => this.SetLastBlockHash(value).Wait();
		}

		public string SetLastBlockHashField(byte[] value) {
			this.chainStateEntry.Value.entry.LastBlockHash = value;

			return nameof(this.chainStateEntry.Value.entry.LastBlockHash);
		}

		public Task SetLastBlockHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetLastBlockHashField(value));
		}

		public DateTime LastBlockTimestamp {
			get { return this.GetField(entry => entry.LastBlockTimestamp); }
			set => this.SetLastBlockTimestamp(value).Wait();
		}

		public string SetLastBlockTimestampField(DateTime value) {
			this.chainStateEntry.Value.entry.LastBlockTimestamp = value;

			return nameof(this.chainStateEntry.Value.entry.LastBlockTimestamp);
		}

		public Task SetLastBlockTimestamp(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastBlockTimestampField(value));
		}

		public ushort LastBlockLifespan {
			get { return this.GetField(entry => entry.LastBlockLifespan); }
			set => this.SetLastBlockLifespan(value).Wait();
		}

		public string SetLastBlockLifespanField(ushort value) {
			this.chainStateEntry.Value.entry.LastBlockLifespan = value;

			return nameof(this.chainStateEntry.Value.entry.LastBlockLifespan);
		}

		public Task SetLastBlockLifespan(ushort value) {
			return this.UpdateFields(prov => prov.SetLastBlockLifespanField(value));
		}

		public ChainStateEntryFields.BlockInterpretationStatuses BlockInterpretationStatus {
			get { return this.GetField(entry => entry.BlockInterpretationStatus); }
			set => this.SetBlockInterpretationStatus(value).Wait();
		}

		public string SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses value) {
			this.chainStateEntry.Value.entry.BlockInterpretationStatus = value;

			return nameof(this.chainStateEntry.Value.entry.BlockInterpretationStatus);
		}

		public Task SetBlockInterpretationStatus(ChainStateEntryFields.BlockInterpretationStatuses value) {
			return this.UpdateFields(prov => prov.SetBlockInterpretationStatusField(value));
		}

		public byte[] GenesisBlockHash {
			get { return this.GetField(entry => entry.GenesisBlockHash); }
			set => this.SetGenesisBlockHash(value).Wait();
		}

		public string SetGenesisBlockHashField(byte[] value) {
			var stateEntry = this.chainStateEntry;

			if(stateEntry != null) {
				stateEntry.Value.entry.GenesisBlockHash = value;
			}

			return nameof(this.chainStateEntry.Value.entry.GenesisBlockHash);
		}

		public Task SetGenesisBlockHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetGenesisBlockHashField(value));
		}

		public long BlockHeight {
			get { return this.GetField(entry => entry.BlockHeight); }
			set => this.SetBlockHeight(value).Wait();
		}

		public string SetBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.BlockHeight = value;

			return nameof(this.chainStateEntry.Value.entry.BlockHeight);
		}

		public async Task SetBlockHeight(long value) {
			await this.UpdateFields(prov => prov.SetBlockHeightField(value));

			//make sure it is always at least worth the block height
			if(value > this.PublicBlockHeight) {
				await this.SetPublicBlockHeight(value);
			}

			if(value > this.DiskBlockHeight) {
				await this.SetDiskBlockHeight(value);
			}

			if(value > this.DownloadBlockHeight) {
				await this.SetDownloadBlockHeight(value);
			}
		}

		public long DiskBlockHeight {
			get { return this.GetField(entry => entry.DiskBlockHeight); }
			set => this.SetDiskBlockHeight(value).Wait();
		}

		public string SetDiskBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.DiskBlockHeight = value;

			return nameof(this.chainStateEntry.Value.entry.DiskBlockHeight);
		}

		public async Task SetDiskBlockHeight(long value) {
			await this.UpdateFields(prov => prov.SetDiskBlockHeightField(value));

			//make sure it is always at least worth the block height
			if(value > this.DownloadBlockHeight) {
				await this.SetDownloadBlockHeight(value);
			}

			if(value > this.PublicBlockHeight) {
				await this.SetPublicBlockHeight(value);
			}

			// finally, if we are a master, we write the block id into the path
			if(this.IsMaster && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.NodeShareType().HasBlocks) {

				byte[] bytes = new byte[sizeof(long)];

				TypeSerializer.Serialize(value, bytes);

				string path = this.GetBlocksIdFilePath();
				FileExtensions.EnsureFileExists(path, this.centralCoordinator.FileSystem);
				FileExtensions.WriteAllBytes(path, bytes, this.centralCoordinator.FileSystem);
			}
		}

		public long DownloadBlockHeight {
			get { return this.GetField(entry => entry.DownloadBlockHeight); }
			set => this.SetDownloadBlockHeight(value).Wait();
		}

		public string SetDownloadBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.DownloadBlockHeight = value;

			return nameof(this.chainStateEntry.Value.entry.DownloadBlockHeight);
		}

		public async Task SetDownloadBlockHeight(long value) {
			await this.UpdateFields(prov => prov.SetDownloadBlockHeightField(value));

			if(value > this.PublicBlockHeight) {
				await this.SetPublicBlockHeight(value);
			}
		}

		public long PublicBlockHeight {
			get {
				long publicHeight = this.GetField(entry => entry.PublicBlockHeight);
				long blockHeight = this.DiskBlockHeight;

				//make sure it is always at least worth the block height
				if(publicHeight < blockHeight) {
					this.PublicBlockHeight = blockHeight;
					publicHeight = blockHeight;
				}

				return publicHeight;
			}
			set {
				long publicHeight = value;
				long blockHeight = this.DiskBlockHeight;

				if(publicHeight < blockHeight) {
					publicHeight = blockHeight;
				}

				this.SetPublicBlockHeight(value).Wait();
			}
		}

		public string SetPublicBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.PublicBlockHeight = value;

			return nameof(this.chainStateEntry.Value.entry.PublicBlockHeight);
		}

		public Task SetPublicBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetPublicBlockHeightField(value));
		}

		public int DigestHeight {
			get { return this.GetField(entry => entry.DigestHeight); }
			set => this.SetDigestHeight(value).Wait();
		}

		public string SetDigestHeightField(int value) {
			this.chainStateEntry.Value.entry.DigestHeight = value;

			return nameof(this.chainStateEntry.Value.entry.DigestHeight);
		}

		public Task SetDigestHeight(int value) {
			return this.UpdateFields(prov => prov.SetDigestHeightField(value));
		}

		public long DigestBlockHeight {
			get { return this.GetField(entry => entry.DigestBlockHeight); }
			set => this.SetDigestBlockHeight(value).Wait();
		}

		public string SetDigestBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.DigestBlockHeight = value;

			return nameof(this.chainStateEntry.Value.entry.DigestBlockHeight);
		}

		public Task SetDigestBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetDigestBlockHeightField(value));
		}

		public byte[] LastDigestHash {
			get { return this.GetField(entry => entry.LastDigestHash); }
			set => this.SetLastDigestHash(value).Wait();
		}

		public string SetLastDigestHashField(byte[] value) {
			this.chainStateEntry.Value.entry.LastDigestHash = value;

			return nameof(this.chainStateEntry.Value.entry.LastDigestHash);
		}

		public Task SetLastDigestHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetLastDigestHashField(value));
		}

		public DateTime LastDigestTimestamp {
			get { return this.GetField(entry => entry.LastDigestTimestamp); }
			set => this.SetLastDigestTimestamp(value).Wait();
		}

		public string SetLastDigestTimestampField(DateTime value) {
			this.chainStateEntry.Value.entry.LastDigestTimestamp = value;

			return nameof(this.chainStateEntry.Value.entry.LastDigestTimestamp);
		}

		public Task SetLastDigestTimestamp(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastDigestTimestampField(value));
		}

		public int PublicDigestHeight {
			get { return this.GetField(entry => entry.PublicDigestHeight); }
			set => this.SetPublicDigestHeight(value).Wait();
		}

		public string SetPublicDigestHeightField(int value) {
			this.chainStateEntry.Value.entry.PublicDigestHeight = value;

			return nameof(this.chainStateEntry.Value.entry.PublicDigestHeight);
		}

		public Task SetPublicDigestHeight(int value) {
			return this.UpdateFields(prov => prov.SetPublicDigestHeightField(value));
		}

		public DateTime LastSync {
			get { return this.GetField(entry => entry.LastSync); }
			set => this.SetLastSync(value).Wait();
		}

		public string SetLastSyncField(DateTime value) {
			this.chainStateEntry.Value.entry.LastSync = value;

			return nameof(this.chainStateEntry.Value.entry.LastSync);
		}

		public Task SetLastSync(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastSyncField(value));
		}

		public string MaximumVersionAllowed {
			get { return this.GetField(entry => entry.MaximumVersionAllowed); }
			set => this.SetMaximumVersionAllowed(value).Wait();
		}

		public string SetMaximumVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MaximumVersionAllowed = value;

			return nameof(this.chainStateEntry.Value.entry.MaximumVersionAllowed);
		}

		public Task SetMaximumVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMaximumVersionAllowedField(value));
		}

		public string MinimumWarningVersionAllowed {
			get { return this.GetField(entry => entry.MinimumWarningVersionAllowed); }
			set => this.SetMinimumWarningVersionAllowed(value).Wait();
		}

		public string SetMinimumWarningVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MinimumWarningVersionAllowed = value;

			return nameof(this.chainStateEntry.Value.entry.MinimumWarningVersionAllowed);
		}

		public Task SetMinimumWarningVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMinimumWarningVersionAllowedField(value));
		}

		public string MinimumVersionAllowed {
			get { return this.GetField(entry => entry.MinimumVersionAllowed); }
			set => this.SetMinimumVersionAllowed(value).Wait();
		}

		public string SetMinimumVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MinimumVersionAllowed = value;

			return nameof(this.chainStateEntry.Value.entry.MinimumVersionAllowed);
		}

		public Task SetMinimumVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMinimumVersionAllowedField(value));
		}

		public int MaxBlockInterval {
			get { return this.GetField(entry => entry.MaxBlockInterval); }
			set => this.SetMaxBlockInterval(value).Wait();
		}

		public string SetMaxBlockIntervalField(int value) {
			this.chainStateEntry.Value.entry.MaxBlockInterval = value;

			return nameof(this.chainStateEntry.Value.entry.MaxBlockInterval);
		}

		public Task SetMaxBlockInterval(int value) {
			return this.UpdateFields(prov => prov.SetMaxBlockIntervalField(value));
		}

		public bool AllowGossipPresentations {
			get { return this.GetField(entry => entry.AllowGossipPresentations); }
			set => this.SetAllowGossipPresentations(value).Wait();
		}

		public string SetAllowGossipPresentationsField(bool value) {
			this.chainStateEntry.Value.entry.AllowGossipPresentations = value;

			return nameof(this.chainStateEntry.Value.entry.AllowGossipPresentations);
		}

		public Task SetAllowGossipPresentations(bool value) {
			return this.UpdateFields(prov => prov.SetAllowGossipPresentationsField(value));
		}

		public long MiningPassword {
			get { return this.GetField(entry => entry.MiningPassword); }
			set => this.SetMiningPassword(value).Wait();
		}

		public string SetMiningPasswordField(long value) {
			this.chainStateEntry.Value.entry.MiningPassword = value;

			return nameof(this.chainStateEntry.Value.entry.MiningPassword);
		}

		public Task SetMiningPassword(long value) {
			return this.UpdateFields(prov => prov.SetMiningPasswordField(value));
		}

		public byte[] MiningAutograph {
			get { return this.GetField(entry => entry.MiningAutograph); }
			set => this.SetMiningAutograph(value).Wait();
		}

		public string SetMiningAutographField(byte[] value) {
			this.chainStateEntry.Value.entry.MiningAutograph = value;

			return nameof(this.chainStateEntry.Value.entry.MiningAutograph);
		}

		public Task SetMiningAutograph(byte[] value) {
			return this.UpdateFields(prov => prov.SetMiningAutographField(value));
		}

		public DateTime? LastMiningRegistrationUpdate {
			get { return this.GetField(entry => entry.LastMiningRegistrationUpdate); }
			set => this.SetLastMiningRegistrationUpdate(value).Wait();
		}

		public string SetLastMiningRegistrationUpdateField(DateTime? value) {
			this.chainStateEntry.Value.entry.LastMiningRegistrationUpdate = value;

			return nameof(this.chainStateEntry.Value.entry.LastMiningRegistrationUpdate);
		}

		public Task SetLastMiningRegistrationUpdate(DateTime? value) {
			return this.UpdateFields(prov => prov.SetLastMiningRegistrationUpdateField(value));
		}

		public bool IsChainSynced => this.GetChainSyncState() == Enums.ChainSyncState.Synchronized;
		public bool IsChainDesynced => !this.IsChainSynced;

		public bool IsChainLikelySynchronized {
			get {
				Enums.ChainSyncState state = this.GetChainSyncState();

				return (state == Enums.ChainSyncState.Synchronized) || (state == Enums.ChainSyncState.LikelyDesynchronized);
			}
		}

		/// <summary>
		///     Get the likely synchronization state of the chain
		/// </summary>
		/// <returns></returns>
		public Enums.ChainSyncState GetChainSyncState() {

			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				// syncless is always synced
				return Enums.ChainSyncState.Synchronized;
			}

			if(BlockchainUtilities.DoesNotShare(GlobalSettings.ApplicationSettings.GetChainConfiguration(this.centralCoordinator.ChainId).BlockSavingMode)) {
				// we dont use block, hence we are always synced
				return Enums.ChainSyncState.Synchronized;
			}

			long blockHeight = this.DiskBlockHeight;

			// a 0 chain is essentially desynced
			if(blockHeight == 0) {
				return Enums.ChainSyncState.Desynchronized;
			}

			long publicBlockHeight = this.PublicBlockHeight;

			// obviously, we are out of sync if this is true
			if(blockHeight < publicBlockHeight) {
				return Enums.ChainSyncState.Desynchronized;
			}

			int maxBlockInterval = 120;

			DateTime lastSync = this.LastSync;
			ushort lifespan = this.LastBlockLifespan;

			if(lifespan == 0) {
				// infinite lifespan, lets use something else

				// take the block interval, otherwise a minute
				var interval = this.MaxBlockInterval;
				maxBlockInterval = interval != 0 ? interval : (int) TimeSpan.FromMinutes(1).TotalSeconds;
			} else {
				maxBlockInterval = lifespan * 2; // we give it the chance of 2 blocks since we may get it through gossip
			}

			// make sure we really dont wait more thn x minutes
			maxBlockInterval = Math.Min(maxBlockInterval, (int) TimeSpan.FromMinutes(3).TotalSeconds);

			// make sure we dont go faster than 15 seconds
			maxBlockInterval = Math.Max(maxBlockInterval, 15);

			// now lets do a play on time
			DateTime syncDeadline = lastSync.AddSeconds(maxBlockInterval);
			DateTime doubleSyncDeadline = lastSync.AddSeconds(maxBlockInterval * 2);

			DateTime now = DateTime.UtcNow;

			if(now > doubleSyncDeadline) {
				return Enums.ChainSyncState.Desynchronized;
			}

			if(now > syncDeadline) {
				return Enums.ChainSyncState.LikelyDesynchronized;
			}

			// ok, if we get here, we can be considered to be synchronized
			return Enums.ChainSyncState.Synchronized;
		}

		/// <summary>
		///     Get a quick access to a moderator key
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public ICryptographicKey GetModeratorKey(byte keyId) {
			return this.GetModeratorKey<ICryptographicKey>(keyId);
		}

		public SafeArrayHandle GetModeratorKeyBytes(byte keyId) {
			MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.GetJoinedField(entry => entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId), true);

			return ByteArray.Wrap(chainKeyEntry?.PublicKey);
		}

		public T GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey {

			using(SafeArrayHandle bytes = this.GetModeratorKeyBytes(keyId)) {

				if(bytes == null) {
					return null;
				}

				using(var rehydrator = DataSerializationFactory.CreateRehydrator(bytes)) {
					return KeyFactory.RehydrateKey<T>(rehydrator);
				}
			}
		}

		/// <summary>
		///     insert a new moderator key in the chainstate for quick access
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public void InsertModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key) {

			this.UpdateJoinedFields(() => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry?.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry != null) {
					throw new ApplicationException($"Moderator key {keyId} is already defined in the chain state");
				}

				chainKeyEntry = new MODERATOR_KEYS_SNAPSHOT();

				chainKeyEntry.OrdinalId = keyId;
				chainKeyEntry.IsCurrent = true;
				chainKeyEntry.PublicKey = key?.ToExactByteArrayCopy();
				chainKeyEntry.DeclarationTransactionId = transactionId.ToString();

				this.chainStateEntry?.entry.ModeratorKeys.Add(chainKeyEntry);
			});
		}

		/// <summary>
		///     update a moderator key
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public void UpdateModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key) {
			this.UpdateJoinedFields(() => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry?.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry == null) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} does not exist.");
				}

				if(key?.IsZero ?? false) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} is null and wont be saved.");
				}

				chainKeyEntry.PublicKey = key?.ToExactByteArrayCopy();
				chainKeyEntry.DeclarationTransactionId = transactionId.ToString();
			});
		}

		/// <summary>
		///     determine if a block height falls within the jurisdiction of a digest
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public bool BlockWithinDigest(long blockId) {
			return blockId <= this.DigestBlockHeight;
		}

		/// <summary>
		///     feeders must check to see if a block has changed. if it did, we must update our state
		/// </summary>
		/// <returns></returns>
		protected bool BlockIdChanged() {
			if(this.IsMaster) {
				// as a master, we dont bother with this
				return false;
			}

			if(this.chainStateEntry?.entry == null) {
				return true;
			}

			string filePath = this.GetBlocksIdFilePath();

			SafeArrayHandle bytes = FileExtensions.ReadAllBytes(filePath, this.centralCoordinator.FileSystem);

			TypeSerializer.Deserialize(bytes.Span, out long fileBlockId);

			return this.chainStateEntry?.entry.DiskBlockHeight != fileBlockId;
		}

		protected virtual CHAIN_STATE_SNAPSHOT CreateNewEntry() {
			CHAIN_STATE_SNAPSHOT chainStateEntry = new CHAIN_STATE_SNAPSHOT();

			chainStateEntry.ChainInception = DateTime.MinValue;
			chainStateEntry.DownloadBlockHeight = 0;
			chainStateEntry.DiskBlockHeight = 0;
			chainStateEntry.BlockHeight = 0;
			chainStateEntry.PublicBlockHeight = 0;
			chainStateEntry.LastBlockTimestamp = DateTime.MinValue;
			chainStateEntry.LastBlockLifespan = 0;
			chainStateEntry.BlockInterpretationStatus = ChainStateEntryFields.BlockInterpretationStatuses.Blank;

			chainStateEntry.DigestHeight = 0;
			chainStateEntry.LastDigestTimestamp = DateTime.MinValue;
			chainStateEntry.MaxBlockInterval = 0;

			chainStateEntry.MaximumVersionAllowed = new SoftwareVersion(0, 0, 1, 5).ToString();
			chainStateEntry.MinimumWarningVersionAllowed = new SoftwareVersion(0, 0, 1, 5).ToString();
			chainStateEntry.MinimumVersionAllowed = new SoftwareVersion(0, 0, 1, 5).ToString();

			return chainStateEntry;
		}

		public async Task UpdateFields(IEnumerable<Func<IChainStateProvider, string>> actions) {
			
			int locks = 0;
			try{
				for(int i = 0; i < MAX_CONCURRENT_READERS; i++) {
					await this.semaphore.WaitAsync();
					locks++;
				}
				
				await Repeater.RepeatAsync(async () => {
					await this.ChainStateDal.PerformOperationAsync(async db => {

						if(this.chainStateEntry == null) {
							this.chainStateEntry = (this.ChainStateDal.LoadSimpleState(db), false);
						}

						var dbEntry = db.Context.Entry(this.chainStateEntry.Value.entry);

						foreach(var action in actions) {
							string propertyName = action(this);
							dbEntry.Property(propertyName).IsModified = true;
						}

						await db.SaveChangesAsync().ConfigureAwait(false);
					});
				});
			}finally {
				if(locks != 0) {
					this.semaphore.Release(locks);
				}
			}

		}

		protected Task UpdateFields(Func<IChainStateProvider, string> action) {

			return this.UpdateFields(new[] {action});
		}

		protected T GetField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function) {

			T value = default;

			this.semaphore.Wait();
			try{
				if((this.chainStateEntry == null) || this.BlockIdChanged()) {
					Repeater.Repeat(() => {
						this.ChainStateDal.PerformOperation(db => {

							this.chainStateEntry = (this.ChainStateDal.LoadSimpleState(db), false);
						});
					});
				}

				value = function(this.chainStateEntry?.entry);
			}finally {
				this.semaphore.Release();
			}

			return value;
		}

		protected void UpdateJoinedFields(Action action) {

			int locks = 0;
			try {
				
				for(int i = 0; i < MAX_CONCURRENT_READERS; i++) {
					this.semaphore.Wait();
					locks++;
				}
				
				Repeater.Repeat(() => {
					this.ChainStateDal.PerformOperation(db => {

						// always refresh the entry from the database
						this.chainStateEntry = (this.ChainStateDal.LoadFullState(db), true);

						// since the entry is detached and not tracked, we attach it here
						db.Context.Attach(this.chainStateEntry.Value.entry).State = EntityState.Modified;

						action();

						db.SaveChanges();
					});
				});

			} finally {
				if(locks != 0) {
					this.semaphore.Release(locks);
				}
			}
		}

		protected T GetJoinedField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function, bool force) {

			T value = default;
			this.semaphore.Wait();

			try {
				// if we have no entry, we must update it from the DB
				if(force || (this.chainStateEntry == null) || !this.chainStateEntry.Value.full) {
					Repeater.Repeat(() => {
						this.ChainStateDal.PerformOperation(db => {

							this.chainStateEntry = (this.ChainStateDal.LoadFullState(db), true);
						});
					});
				}

				value = function(this.chainStateEntry?.entry);

			} finally {
				this.semaphore.Release();
			}

			return value;
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
					this.semaphore.Dispose();

				} catch(Exception ex) {

				}
			}

			this.IsDisposed = true;
		}

		~ChainStateProvider() {
			this.Dispose(false);
		}

	#endregion

	}
}