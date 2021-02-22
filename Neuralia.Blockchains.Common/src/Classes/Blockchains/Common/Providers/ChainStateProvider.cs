using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralium.Blockchains.Neuralium.Classes.NeuraliumChain;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainStateProvider : IChainStateEntryFields, IChainProvider, IDisposableExtended {
		public bool IsChainLikelySynchronized { get; }
		public bool IsChainSynced { get; }
		public bool IsChainDesynced { get; }

		public Task InsertModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key);
		public Task UpdateModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key, bool keyChange);
		public Task UpdateModeratorExpectedNextKeyIndex(byte keyId, long keySequenceId, long keyIndex);
		public Task<ICryptographicKey> GetModeratorKey(byte keyId);
		public Task<(ICryptographicKey key, KeyUseIndexSet keyIndex)> GetModeratorKeyAndIndex(byte keyId);
		public Task<KeyUseIndexSet> GetModeratorKeyIndex(byte keyId);
		
		public Task<T> GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey;

		public Task<SafeArrayHandle> GetModeratorKeyBytes(byte keyId);

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

		string[] SetChainInceptionField(DateTime value);
		string[] SetLastBlockHashField(byte[] value);
		string[] SetLastBlockTimestampField(DateTime value);
		string[] SetLastBlockLifespanField(ushort value);
		string[] SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses value);
		string[] SetGenesisBlockHashField(byte[] value);
		string[] SetBlockHeightField(long value);
		string[] SetDiskBlockHeightField(long value);
		string[] SetDownloadBlockHeightField(long value);
		string[] SetPublicBlockHeightField(long value);
		string[] SetDigestHeightField(int value);
		string[] SetDigestBlockHeightField(long value);
		string[] SetLastDigestHashField(byte[] value);
		string[] SetLastDigestTimestampField(DateTime value);
		string[] SetPublicDigestHeightField(int value);
		string[] SetLastSyncField(DateTime value);
		string[] SetMaximumVersionAllowedField(string value);
		string[] SetMinimumWarningVersionAllowedField(string value);
		string[] SetMinimumVersionAllowedField(string value);
		string[] SetMaxBlockIntervalField(int value);
		string[] SetAllowGossipPresentationsField(bool value);
		string[] SetLastBlockXmssKeySignaturePathCacheField(byte[] value);

		Task UpdateFields(IEnumerable<Func<IChainStateProvider, string[]>> actions);
		Task UpdateFields(Func<IChainStateProvider, string[]> action);
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
	public abstract class ChainStateProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : ChainProvider, IChainStateProvider<CHAIN_STATE_DAL, CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_STATE_DAL : class, IChainStateDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_CONTEXT : class, IChainStateContext
		where CHAIN_STATE_SNAPSHOT : class, IChainStateEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new()
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new() {

		private const string BLOCKS_ID_FILE = "block.id";

		/// <summary>
		///     allow parallel readers, one writer
		/// </summary>
		private readonly RecursiveAsyncReaderWriterLock asyncLocker = new RecursiveAsyncReaderWriterLock();

		private readonly CENTRAL_COORDINATOR centralCoordinator;
		protected CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		private readonly string folderPath;

		private readonly object locker = new object();

		protected readonly ITimeService timeService;
		private CHAIN_STATE_DAL chainStateDal;

		protected (CHAIN_STATE_SNAPSHOT entry, bool full)? chainStateEntry;

		public ChainStateProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
		}

		protected bool IsMain => this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType == AppSettingsBase.SerializationTypes.Main;

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

		public override async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);
			
			string path = this.GetBlocksIdFilePath();

			if(GlobalSettings.ApplicationSettings.SerializationType == AppSettingsBase.SerializationTypes.Main) {
				FileExtensions.EnsureFileExists(path, this.centralCoordinator.FileSystem);
			} else {
				if(!File.Exists(path)) {
					throw new ApplicationException($"Blocks ID file did not exist at path: {path}");
				}
			}
		}
		
		public string GetBlocksIdFilePath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlocksFolderPath(), BLOCKS_ID_FILE);
		}

		/// <summary>
		///     make sure the chain state will be requeried.
		/// </summary>
		public void ResetChainState() {

			using(this.asyncLocker.WriterLock()) {
				this.chainStateEntry = null;
			}
		}

		public DateTime ChainInception {
			get { return this.GetField(entry => DateTime.SpecifyKind(entry.ChainInception, DateTimeKind.Utc)).WaitAndUnwrapException(); }
			set => this.SetChainInception(value).WaitAndUnwrapException();
		}

		public string[] SetChainInceptionField(DateTime value) {
			this.chainStateEntry.Value.entry.ChainInception = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.ChainInception)};
		}

		public Task SetChainInception(DateTime value) {
			return this.UpdateFields(prov => prov.SetChainInceptionField(value));
		}

		public byte[] LastBlockHash {
			get { return this.GetField(entry => entry.LastBlockHash).WaitAndUnwrapException(); }
			set => this.SetLastBlockHash(value).WaitAndUnwrapException();
		}

		public string[] SetLastBlockHashField(byte[] value) {
			this.chainStateEntry.Value.entry.LastBlockHash = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastBlockHash)};
		}

		public Task SetLastBlockHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetLastBlockHashField(value));
		}

		public DateTime LastBlockTimestamp {
			get { return this.GetField(entry => entry.LastBlockTimestamp).WaitAndUnwrapException(); }
			set => this.SetLastBlockTimestamp(value).WaitAndUnwrapException();
		}

		public string[] SetLastBlockTimestampField(DateTime value) {
			this.chainStateEntry.Value.entry.LastBlockTimestamp = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastBlockTimestamp)};
		}

		public Task SetLastBlockTimestamp(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastBlockTimestampField(value));
		}

		public ushort LastBlockLifespan {
			get { return this.GetField(entry => entry.LastBlockLifespan).WaitAndUnwrapException(); }
			set => this.SetLastBlockLifespan(value).WaitAndUnwrapException();
		}

		public string[] SetLastBlockLifespanField(ushort value) {
			this.chainStateEntry.Value.entry.LastBlockLifespan = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastBlockLifespan)};
		}

		public Task SetLastBlockLifespan(ushort value) {
			return this.UpdateFields(prov => prov.SetLastBlockLifespanField(value));
		}

		public ChainStateEntryFields.BlockInterpretationStatuses BlockInterpretationStatus {
			get { return this.GetField(entry => entry.BlockInterpretationStatus).WaitAndUnwrapException(); }
			set => this.SetBlockInterpretationStatus(value).WaitAndUnwrapException();
		}

		public string[] SetBlockInterpretationStatusField(ChainStateEntryFields.BlockInterpretationStatuses value) {
			this.chainStateEntry.Value.entry.BlockInterpretationStatus = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.BlockInterpretationStatus)};
		}

		public Task SetBlockInterpretationStatus(ChainStateEntryFields.BlockInterpretationStatuses value) {
			return this.UpdateFields(prov => prov.SetBlockInterpretationStatusField(value));
		}

		public byte[] GenesisBlockHash {
			get { return this.GetField(entry => entry.GenesisBlockHash).WaitAndUnwrapException(); }
			set => this.SetGenesisBlockHash(value).WaitAndUnwrapException();
		}

		public string[] SetGenesisBlockHashField(byte[] value) {
			(CHAIN_STATE_SNAPSHOT entry, bool full)? stateEntry = this.chainStateEntry;

			if(stateEntry != null) {
				stateEntry.Value.entry.GenesisBlockHash = value;
			}

			return new[] {nameof(this.chainStateEntry.Value.entry.GenesisBlockHash)};
		}

		public Task SetGenesisBlockHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetGenesisBlockHashField(value));
		}

		public long BlockHeight {
			get { return this.GetField(entry => entry.BlockHeight).WaitAndUnwrapException(); }
			set => this.SetBlockHeight(value).WaitAndUnwrapException();
		}

		public string[] SetBlockHeightField(long value) {
			CHAIN_STATE_SNAPSHOT entry = this.chainStateEntry.Value.entry;
			entry.BlockHeight = value;

			List<string> propertyNames = new List<string>();
			propertyNames.Add(nameof(entry.BlockHeight));

			//make sure it is always at least worth the block height
			if(value > entry.PublicBlockHeight) {
				propertyNames.AddRange(this.SetPublicBlockHeightField(value));
			}

			if(value > entry.DiskBlockHeight) {
				propertyNames.AddRange(this.SetDiskBlockHeightField(value));
			}

			if(value > entry.DownloadBlockHeight) {
				propertyNames.AddRange(this.SetDownloadBlockHeightField(value));
			}

			return propertyNames.ToArray();
		}

		public Task SetBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetBlockHeightField(value));
		}

		public long DiskBlockHeight {
			get { return this.GetField(entry => entry.DiskBlockHeight).WaitAndUnwrapException(); }
			set => this.SetDiskBlockHeight(value).WaitAndUnwrapException();
		}

		public string[] SetDiskBlockHeightField(long value) {
			CHAIN_STATE_SNAPSHOT entry = this.chainStateEntry.Value.entry;
			entry.DiskBlockHeight = value;

			List<string> propertyNames = new List<string>();
			propertyNames.Add(nameof(entry.DiskBlockHeight));

			//make sure it is always at least worth the block height
			if(value > entry.DownloadBlockHeight) {
				propertyNames.AddRange(this.SetDownloadBlockHeightField(value));
			}

			if(value > entry.PublicBlockHeight) {
				propertyNames.AddRange(this.SetPublicBlockHeightField(value));
			}

			// finally, if we are a master, we write the block id into the path
			if(this.IsMain && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.NodeShareType().HasBlocks) {

				try {
					//TODO: can this be made async?
					byte[] bytes = new byte[sizeof(long)];

					TypeSerializer.Serialize(value, in bytes);

					string path = this.GetBlocksIdFilePath();
					FileExtensions.WriteAllBytes(path, bytes, this.centralCoordinator.FileSystem);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to write blocks id file.");
				}
			}

			return propertyNames.ToArray();
		}

		public Task SetDiskBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetDiskBlockHeightField(value));
		}

		public long DownloadBlockHeight {
			get { return this.GetField(entry => entry.DownloadBlockHeight).WaitAndUnwrapException(); }
			set => this.SetDownloadBlockHeight(value).WaitAndUnwrapException();
		}

		public string[] SetDownloadBlockHeightField(long value) {

			CHAIN_STATE_SNAPSHOT entry = this.chainStateEntry.Value.entry;
			entry.DownloadBlockHeight = value;

			List<string> propertyNames = new List<string>();
			propertyNames.Add(nameof(entry.DownloadBlockHeight));

			if(value > entry.PublicBlockHeight) {
				propertyNames.AddRange(this.SetPublicBlockHeightField(value));
			}

			return propertyNames.ToArray();
		}

		public async Task SetDownloadBlockHeight(long value) {
			await this.UpdateFields(prov => prov.SetDownloadBlockHeightField(value)).ConfigureAwait(false);
		}

		public long PublicBlockHeight {
			get {
				long publicHeight = this.GetField(entry => entry.PublicBlockHeight).WaitAndUnwrapException();
				long blockHeight = this.DiskBlockHeight;

				//make sure it is always at least worth the block height
				if(publicHeight < blockHeight) {
					this.PublicBlockHeight = blockHeight;
					publicHeight = blockHeight;
				}

				return publicHeight;
			}
			set => this.SetPublicBlockHeight(value).WaitAndUnwrapException();
		}

		public string[] SetPublicBlockHeightField(long value) {

			CHAIN_STATE_SNAPSHOT entry = this.chainStateEntry.Value.entry;
			long publicHeight = value;

			// skip the lock, get the field directly
			long blockHeight = entry.DiskBlockHeight;

			if(publicHeight < blockHeight) {
				publicHeight = blockHeight;
			}

			entry.PublicBlockHeight = publicHeight;

			return new[] {nameof(entry.PublicBlockHeight)};
		}

		public Task SetPublicBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetPublicBlockHeightField(value));
		}

		public int DigestHeight {
			get { return this.GetField(entry => entry.DigestHeight).WaitAndUnwrapException(); }
			set => this.SetDigestHeight(value).WaitAndUnwrapException();
		}

		public string[] SetDigestHeightField(int value) {
			this.chainStateEntry.Value.entry.DigestHeight = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.DigestHeight)};
		}

		public Task SetDigestHeight(int value) {
			return this.UpdateFields(prov => prov.SetDigestHeightField(value));
		}

		public long DigestBlockHeight {
			get { return this.GetField(entry => entry.DigestBlockHeight).WaitAndUnwrapException(); }
			set => this.SetDigestBlockHeight(value).WaitAndUnwrapException();
		}

		public string[] SetDigestBlockHeightField(long value) {
			this.chainStateEntry.Value.entry.DigestBlockHeight = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.DigestBlockHeight)};
		}

		public Task SetDigestBlockHeight(long value) {
			return this.UpdateFields(prov => prov.SetDigestBlockHeightField(value));
		}

		public byte[] LastDigestHash {
			get { return this.GetField(entry => entry.LastDigestHash).WaitAndUnwrapException(); }
			set => this.SetLastDigestHash(value).WaitAndUnwrapException();
		}

		public string[] SetLastDigestHashField(byte[] value) {
			this.chainStateEntry.Value.entry.LastDigestHash = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastDigestHash)};
		}

		public Task SetLastDigestHash(byte[] value) {
			return this.UpdateFields(prov => prov.SetLastDigestHashField(value));
		}

		public DateTime LastDigestTimestamp {
			get { return this.GetField(entry => entry.LastDigestTimestamp).WaitAndUnwrapException(); }
			set => this.SetLastDigestTimestamp(value).WaitAndUnwrapException();
		}

		public string[] SetLastDigestTimestampField(DateTime value) {
			this.chainStateEntry.Value.entry.LastDigestTimestamp = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastDigestTimestamp)};
		}

		public Task SetLastDigestTimestamp(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastDigestTimestampField(value));
		}

		public int PublicDigestHeight {
			get { return this.GetField(entry => entry.PublicDigestHeight).WaitAndUnwrapException(); }
			set => this.SetPublicDigestHeight(value).WaitAndUnwrapException();
		}

		public string[] SetPublicDigestHeightField(int value) {
			this.chainStateEntry.Value.entry.PublicDigestHeight = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.PublicDigestHeight)};
		}

		public Task SetPublicDigestHeight(int value) {
			return this.UpdateFields(prov => prov.SetPublicDigestHeightField(value));
		}

		public DateTime LastSync {
			get { return this.GetField(entry => entry.LastSync).WaitAndUnwrapException(); }
			set => this.SetLastSync(value).WaitAndUnwrapException();
		}

		public string[] SetLastSyncField(DateTime value) {
			this.chainStateEntry.Value.entry.LastSync = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastSync)};
		}

		public Task SetLastSync(DateTime value) {
			return this.UpdateFields(prov => prov.SetLastSyncField(value));
		}

		public string MaximumVersionAllowed {
			get { return this.GetField(entry => entry.MaximumVersionAllowed).WaitAndUnwrapException(); }
			set => this.SetMaximumVersionAllowed(value).WaitAndUnwrapException();
		}

		public string[] SetMaximumVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MaximumVersionAllowed = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.MaximumVersionAllowed)};
		}

		public Task SetMaximumVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMaximumVersionAllowedField(value));
		}

		public string MinimumWarningVersionAllowed {
			get { return this.GetField(entry => entry.MinimumWarningVersionAllowed).WaitAndUnwrapException(); }
			set => this.SetMinimumWarningVersionAllowed(value).WaitAndUnwrapException();
		}

		public string[] SetMinimumWarningVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MinimumWarningVersionAllowed = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.MinimumWarningVersionAllowed)};
		}

		public Task SetMinimumWarningVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMinimumWarningVersionAllowedField(value));
		}

		public string MinimumVersionAllowed {
			get { return this.GetField(entry => entry.MinimumVersionAllowed).WaitAndUnwrapException(); }
			set => this.SetMinimumVersionAllowed(value).WaitAndUnwrapException();
		}

		public string[] SetMinimumVersionAllowedField(string value) {
			this.chainStateEntry.Value.entry.MinimumVersionAllowed = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.MinimumVersionAllowed)};
		}

		public Task SetMinimumVersionAllowed(string value) {
			return this.UpdateFields(prov => prov.SetMinimumVersionAllowedField(value));
		}

		public int MaxBlockInterval {
			get { return this.GetField(entry => entry.MaxBlockInterval).WaitAndUnwrapException(); }
			set => this.SetMaxBlockInterval(value).WaitAndUnwrapException();
		}

		public string[] SetMaxBlockIntervalField(int value) {
			this.chainStateEntry.Value.entry.MaxBlockInterval = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.MaxBlockInterval)};
		}

		public Task SetMaxBlockInterval(int value) {
			return this.UpdateFields(prov => prov.SetMaxBlockIntervalField(value));
		}

		public bool AllowGossipPresentations {
			get { return this.GetField(entry => entry.AllowGossipPresentations).WaitAndUnwrapException(); }
			set => this.SetAllowGossipPresentations(value).WaitAndUnwrapException();
		}

		public byte[] LastBlockXmssKeySignaturePathCache {
			get { return this.GetField(entry => entry.LastBlockXmssKeySignaturePathCache).WaitAndUnwrapException(); }
			set => this.SetLastBlockXmssKeySignaturePathCache(value).WaitAndUnwrapException();
		}

		public string[] SetLastBlockXmssKeySignaturePathCacheField(byte[] value) {
			this.chainStateEntry.Value.entry.LastBlockXmssKeySignaturePathCache = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.LastBlockXmssKeySignaturePathCache)};
		}

		public Task SetLastBlockXmssKeySignaturePathCache(byte[] value) {
			return this.UpdateFields(prov => prov.SetLastBlockXmssKeySignaturePathCacheField(value));
		}
		
		public string[] SetAllowGossipPresentationsField(bool value) {
			this.chainStateEntry.Value.entry.AllowGossipPresentations = value;

			return new[] {nameof(this.chainStateEntry.Value.entry.AllowGossipPresentations)};
		}

		public Task SetAllowGossipPresentations(bool value) {
			return this.UpdateFields(prov => prov.SetAllowGossipPresentationsField(value));
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
				int interval = this.MaxBlockInterval;
				maxBlockInterval = interval != 0 ? interval : (int) TimeSpan.FromMinutes(1).TotalSeconds;
			} else {
				maxBlockInterval = lifespan; // we give it the chance of 2 blocks since we may get it through gossip
			}
			
			// make sure we dont go faster than 15 seconds
			maxBlockInterval = Math.Max(maxBlockInterval, 15);

			// now lets do a play on time
			
			DateTime doubleSyncDeadline = lastSync.AddSeconds(maxBlockInterval * 2.3);

			DateTime now = DateTimeEx.CurrentTime;

			if(now > doubleSyncDeadline) {
				return Enums.ChainSyncState.Desynchronized;
			}

			DateTime syncDeadline = lastSync.AddSeconds(maxBlockInterval * 1.3);
			
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
		public Task<ICryptographicKey> GetModeratorKey(byte keyId) {
			return this.GetModeratorKey<ICryptographicKey>(keyId);
		}

		public async Task<SafeArrayHandle> GetModeratorKeyBytes(byte keyId) {
			MODERATOR_KEYS_SNAPSHOT chainKeyEntry = await GetJoinedField(entry => entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId), true).ConfigureAwait(false);

			return SafeArrayHandle.Wrap(chainKeyEntry?.PublicKey);
		}

		public async Task<T> GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey {

			using SafeArrayHandle bytes = await GetModeratorKeyBytes(keyId).ConfigureAwait(false);

			if(bytes == null && bytes.IsZero) {
				return null;
			}

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			return KeyFactory.RehydrateKey<T>(rehydrator);

		}

		public async Task<(ICryptographicKey key, KeyUseIndexSet keyIndex)> GetModeratorKeyAndIndex(byte keyId) {
			var key = await GetModeratorKey(keyId).ConfigureAwait(false);
			var index = await GetModeratorKeyIndex(keyId).ConfigureAwait(false);

			return (key, index);
		}

		public async Task<KeyUseIndexSet> GetModeratorKeyIndex(byte keyId) {
			MODERATOR_KEYS_SNAPSHOT chainKeyEntry = await GetJoinedField(entry => entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId), true).ConfigureAwait(false);

			return (chainKeyEntry.ExpectedNextKeySequenceId, chainKeyEntry.ExpectedNextKeyIndex);
		}
		
		/// <summary>
		///     insert a new moderator key in the chainstate for quick access
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public Task InsertModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key) {

			return this.UpdateJoinedFields(() => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry?.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry != null) {
					throw new ApplicationException($"Moderator key {keyId} is already defined in the chain state");
				}

				chainKeyEntry = new MODERATOR_KEYS_SNAPSHOT();

				chainKeyEntry.OrdinalId = keyId;
				chainKeyEntry.IsCurrent = true;
				chainKeyEntry.ExpectedNextKeySequenceId = 0;
				chainKeyEntry.ExpectedNextKeyIndex = 0;
				chainKeyEntry.PublicKey = key?.ToExactByteArrayCopy();
				chainKeyEntry.DeclarationTransactionId = transactionId.ToString();

				this.chainStateEntry?.entry.ModeratorKeys.Add(chainKeyEntry);
			});
		}

		/// <summary>
		///     update a moderator key
		/// </summary>
		/// <param name="transactionId"></param>
		/// <param name="keyId"></param>
		/// <param name="key"></param>
		/// <param name="keyChange"></param>
		/// <returns></returns>
		public Task UpdateModeratorKey(TransactionId transactionId, byte keyId, SafeArrayHandle key, bool keyChange) {
			return this.UpdateJoinedFields(() => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry?.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry == null) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} does not exist.");
				}

				if(key?.IsZero ?? false) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} is null and wont be saved.");
				}

				chainKeyEntry.PublicKey = key?.ToExactByteArrayCopy();
				chainKeyEntry.DeclarationTransactionId = transactionId.ToString();

				if(keyChange) {
					chainKeyEntry.ExpectedNextKeySequenceId += 1;
					chainKeyEntry.ExpectedNextKeyIndex = 0;
				}
			});
		}

		public Task UpdateModeratorExpectedNextKeyIndex(byte keyId, long keySequenceId, long keyIndex) {
			return this.UpdateJoinedFields(() => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry?.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry == null) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} does not exist.");
				}

				chainKeyEntry.ExpectedNextKeySequenceId = keySequenceId;
				chainKeyEntry.ExpectedNextKeyIndex = keyIndex;
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

		public async Task UpdateFields(IEnumerable<Func<IChainStateProvider, string[]>> actions) {

			using(await this.asyncLocker.WriterLockAsync().ConfigureAwait(false)) {
				await Repeater.RepeatAsync(() => {
					
					LockContext lockContext = null;
					return this.ChainStateDal.PerformOperationAsync(async (db, lc) => {

						if(this.chainStateEntry == null) {
							this.chainStateEntry = (await ChainStateDal.LoadSimpleState(db).ConfigureAwait(false), false);
						}

						EntityEntry<CHAIN_STATE_SNAPSHOT> dbEntry = db.Context.Entry(this.chainStateEntry.Value.entry);

						foreach(Func<IChainStateProvider, string[]> action in actions) {

							foreach(string propertyName in action(this)) {
								dbEntry.Property(propertyName).IsModified = true;
							}
						}

						await db.SaveChangesAsync().ConfigureAwait(false);
					}, lockContext);
				}).ConfigureAwait(false);
			}
		}

		public Task UpdateFields(Func<IChainStateProvider, string[]> action) {

			return this.UpdateFields(new[] {action});
		}

		/// <summary>
		///     feeders must check to see if a block has changed. if it did, we must update our state
		/// </summary>
		/// <returns></returns>
		protected bool BlockIdChanged() {
			if(this.IsMain) {
				// as a master, we dont bother with this
				return false;
			}

			if(this.chainStateEntry?.entry == null) {
				return true;
			}

			string filePath = this.GetBlocksIdFilePath();

			long fileBlockId = Repeater.Repeat(() => {
				
				SafeArrayHandle bytes = FileExtensions.ReadAllBytes(filePath, this.centralCoordinator.FileSystem);

				TypeSerializer.Deserialize(bytes.Span, out long fileBlockIdParsed);

				return fileBlockIdParsed;
			});
			

			return this.chainStateEntry?.entry.DiskBlockHeight != fileBlockId;
		}

		protected virtual CHAIN_STATE_SNAPSHOT CreateNewEntry() {
			CHAIN_STATE_SNAPSHOT chainStateEntry = new CHAIN_STATE_SNAPSHOT();

			chainStateEntry.ChainInception = DateTimeEx.MinValue;
			chainStateEntry.DownloadBlockHeight = 0;
			chainStateEntry.DiskBlockHeight = 0;
			chainStateEntry.BlockHeight = 0;
			chainStateEntry.PublicBlockHeight = 0;
			chainStateEntry.LastBlockTimestamp = DateTimeEx.MinValue;
			chainStateEntry.LastBlockLifespan = 0;
			chainStateEntry.BlockInterpretationStatus = ChainStateEntryFields.BlockInterpretationStatuses.Blank;

			chainStateEntry.DigestHeight = 0;
			chainStateEntry.LastDigestTimestamp = DateTimeEx.MinValue;
			chainStateEntry.MaxBlockInterval = 0;

			chainStateEntry.MaximumVersionAllowed = new SoftwareVersion(BlockchainConstants.BlockchainCompatibilityVersion).ToString();
			chainStateEntry.MinimumWarningVersionAllowed = new SoftwareVersion(BlockchainConstants.BlockchainCompatibilityVersion).ToString();
			chainStateEntry.MinimumVersionAllowed = new SoftwareVersion(BlockchainConstants.BlockchainCompatibilityVersion).ToString();

			return chainStateEntry;
		}

		protected async Task<T> GetField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function) {

			using(await asyncLocker.ReaderLockAsync().ConfigureAwait(false)) {
				if((this.chainStateEntry != null) && !this.BlockIdChanged()) {
					return function(this.chainStateEntry?.entry);
				}
			}

			using(await asyncLocker.WriterLockAsync().ConfigureAwait(false)) {
				if((this.chainStateEntry != null) && !this.BlockIdChanged()) {
					return function(this.chainStateEntry?.entry);
				}

				await Repeater.RepeatAsync(() => {
					LockContext lockContext = null;
					return ChainStateDal.PerformOperationAsync(async (db, lc) => {

                        chainStateEntry = (await ChainStateDal.LoadSimpleState(db).ConfigureAwait(false), false);
					}, lockContext);
				}).ConfigureAwait(false);

				if(this.chainStateEntry.HasValue && new SoftwareVersion(this.chainStateEntry.Value.entry.MinimumVersionAllowed) > new SoftwareVersion(BlockchainConstants.BlockchainCompatibilityVersion)) {
					NLog.Default.Warning($"The {nameof(MinimumVersionAllowed)} from the chain state is {this.chainStateEntry.Value.entry.MinimumVersionAllowed}, but our software version is {BlockchainConstants.BlockchainCompatibilityVersion}. Possible incompatibilities!");
				}
				return function(this.chainStateEntry?.entry);
			}
		}

		protected async Task UpdateJoinedFields(Action action) {

			using(await asyncLocker.WriterLockAsync().ConfigureAwait(false)) {

				await Repeater.RepeatAsync(() => {
					LockContext lockContext = null;
					return ChainStateDal.PerformOperationAsync(async (db, lc) => {

                        // always refresh the entry from the database
                        chainStateEntry = (await ChainStateDal.LoadFullState(db).ConfigureAwait(false), true);

						// since the entry is detached and not tracked, we attach it here
						db.Context.Attach(chainStateEntry.Value.entry).State = EntityState.Modified;

						action();

						await db.SaveChangesAsync().ConfigureAwait(false);
					}, lockContext);
				}).ConfigureAwait(false);
			}
		}

		protected async Task<T> GetJoinedField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function, bool force) {

			if(!force) {
				using(await asyncLocker.ReaderLockAsync().ConfigureAwait(false)) {
					if((this.chainStateEntry != null) && this.chainStateEntry.Value.full) {
						return function(this.chainStateEntry?.entry);
					}
				}
			}

			using(await asyncLocker.ReaderLockAsync().ConfigureAwait(false)) {
				if(!force && (this.chainStateEntry != null) && this.chainStateEntry.Value.full) {
					return function(this.chainStateEntry?.entry);
				}

				await Repeater.RepeatAsync(() => {
					LockContext lockContext = null;
					return ChainStateDal.PerformOperationAsync(async (db, lc) => {

                        chainStateEntry = (await ChainStateDal.LoadFullState(db).ConfigureAwait(false), true);
					}, lockContext);
				}).ConfigureAwait(false);

				return function(this.chainStateEntry?.entry);
			}
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