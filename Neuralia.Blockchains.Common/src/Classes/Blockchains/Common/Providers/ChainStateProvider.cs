using System;
using System.IO;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainStateProvider : IChainStateEntryFields {
		bool IsChainSynced { get; }
		bool IsChainDesynced { get; }

		void InsertModeratorKey(TransactionId transactionId, byte keyId, IByteArray key);
		void UpdateModeratorKey(TransactionId transactionId, byte keyId, IByteArray key);
		ICryptographicKey GetModeratorKey(byte keyId);

		T GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey;

		IByteArray GetModeratorKeyBytes(byte keyId);

		Enums.ChainSyncState GetChainSyncState();

		bool BlockWithinDigest(long blockId);

		string GetBlocksIdFilePath();
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

		public DateTime ChainInception {
			get { return this.GetField(entry => DateTime.SpecifyKind(entry.ChainInception, DateTimeKind.Utc)); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.ChainInception = value;
				});
			}
		}

		public byte[] LastBlockHash {
			get { return this.GetField(entry => entry.LastBlockHash); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastBlockHash = value;
				});
			}
		}

		public DateTime LastBlockTimestamp {
			get { return this.GetField(entry => entry.LastBlockTimestamp); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastBlockTimestamp = value;
				});
			}
		}

		public ushort LastBlockLifespan {
			get { return this.GetField(entry => entry.LastBlockLifespan); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastBlockLifespan = value;
				});
			}
		}

		public ChainStateEntryFields.BlockInterpretationStatuses BlockInterpretationStatus {
			get { return this.GetField(entry => entry.BlockInterpretationStatus); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.BlockInterpretationStatus = value;
				});
			}
		}

		public byte[] GenesisBlockHash {
			get { return this.GetField(entry => entry.GenesisBlockHash); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.GenesisBlockHash = value;
				});
			}
		}

		public long BlockHeight {
			get { return this.GetField(entry => entry.BlockHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.BlockHeight = value;
				});

				//make sure it is always at least worth the block height
				if(value > this.PublicBlockHeight) {
					this.PublicBlockHeight = value;
				}

				if(value > this.DiskBlockHeight) {
					this.DiskBlockHeight = value;
				}

				if(value > this.DownloadBlockHeight) {
					this.DownloadBlockHeight = value;
				}
			}
		}

		public long DiskBlockHeight {
			get { return this.GetField(entry => entry.DiskBlockHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.DiskBlockHeight = value;
				});

				//make sure it is always at least worth the block height
				if(value > this.DownloadBlockHeight) {
					this.DownloadBlockHeight = value;
				}

				if(value > this.PublicBlockHeight) {
					this.PublicBlockHeight = value;
				}

				// finally, if we are a master, we write the block id into the path
				if(this.IsMaster && BlockchainUtilities.UsesBlocks(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockSavingMode)) {

					Span<byte> bytes = stackalloc byte[sizeof(long)];

					TypeSerializer.Serialize(value, bytes);

					string path = this.GetBlocksIdFilePath();
					FileExtensions.EnsureFileExists(path, this.centralCoordinator.FileSystem);
					FileExtensions.WriteAllBytes(path, bytes, this.centralCoordinator.FileSystem);
				}
			}
		}

		public long DownloadBlockHeight {
			get { return this.GetField(entry => entry.DownloadBlockHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.DownloadBlockHeight = value;
				});

				if(value > this.PublicBlockHeight) {
					this.PublicBlockHeight = value;
				}
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

				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.PublicBlockHeight = publicHeight;
				});
			}
		}

		public int DigestHeight {
			get { return this.GetField(entry => entry.DigestHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.DigestHeight = value;
				});
			}
		}

		public long DigestBlockHeight {
			get { return this.GetField(entry => entry.DigestBlockHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.DigestBlockHeight = value;
				});
			}
		}

		public byte[] LastDigestHash {
			get { return this.GetField(entry => entry.LastDigestHash); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastDigestHash = value;
				});
			}
		}

		public DateTime LastDigestTimestamp {
			get { return this.GetField(entry => entry.LastDigestTimestamp); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastDigestTimestamp = value;
				});
			}
		}

		public int PublicDigestHeight {
			get { return this.GetField(entry => entry.PublicDigestHeight); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.PublicDigestHeight = value;
				});
			}
		}

		public DateTime LastSync {
			get { return this.GetField(entry => entry.LastSync); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastSync = value;
				});
			}
		}

		public string MaximumVersionAllowed {
			get { return this.GetField(entry => entry.MaximumVersionAllowed); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.MaximumVersionAllowed = value;
				});
			}
		}

		public string MinimumWarningVersionAllowed {
			get { return this.GetField(entry => entry.MinimumWarningVersionAllowed); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.MinimumWarningVersionAllowed = value;
				});
			}
		}

		public string MinimumVersionAllowed {
			get { return this.GetField(entry => entry.MinimumVersionAllowed); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.MinimumVersionAllowed = value;
				});
			}
		}

		public int MaxBlockInterval {
			get { return this.GetField(entry => entry.MaxBlockInterval); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.MaxBlockInterval = value;
				});
			}
		}

		public long MiningPassword {
			get { return this.GetField(entry => entry.MiningPassword); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.MiningPassword = value;
				});
			}
		}

		public DateTime? LastMiningRegistrationUpdate {
			get { return this.GetField(entry => entry.LastMiningRegistrationUpdate); }
			set {
				this.UpdateFields(db => {
					this.chainStateEntry.Value.entry.LastMiningRegistrationUpdate = value;
				});
			}
		}

		public bool IsChainSynced => this.GetChainSyncState() == Enums.ChainSyncState.Synchronized;
		public bool IsChainDesynced => !this.IsChainSynced;

		/// <summary>
		///     Get the likely synchronization state of the chain
		/// </summary>
		/// <returns></returns>
		public Enums.ChainSyncState GetChainSyncState() {

			if(GlobalSettings.ApplicationSettings.GetChainConfiguration(this.centralCoordinator.ChainId).BlockSavingMode == AppSettingsBase.BlockSavingModes.None) {
				// we dont use block, hence we are always synced
				return Enums.ChainSyncState.Synchronized;
			}

			long blockHeight;

			lock(this.locker) {
				blockHeight = this.DiskBlockHeight;
			}

			// a 0 chain is essentially desynced
			if(blockHeight == 0) {
				return Enums.ChainSyncState.Desynchronized;
			}

			long publicBlockHeight;

			lock(this.locker) {
				publicBlockHeight = this.PublicBlockHeight;
			}

			// obviously, we are out of sync if this is true
			if(blockHeight < publicBlockHeight) {
				return Enums.ChainSyncState.Desynchronized;
			}

			DateTime lastSync;
			int maxBlockInterval;

			lock(this.locker) {
				lastSync = this.LastSync;

				ushort lifespan = this.LastBlockLifespan;

				if(lifespan == 0) {
					// infinite lifespan, lets use something else

					// take the block interval, otherwise a minute
					maxBlockInterval = this.MaxBlockInterval != 0 ? this.MaxBlockInterval : (int) TimeSpan.FromMinutes(1).TotalSeconds;
				} else {
					maxBlockInterval = lifespan * 2; // we give it the chance of 2 blocks since we may get it through gossip
				}

				// make sure we really dont wait more thn x minutes
				maxBlockInterval = Math.Min(maxBlockInterval, (int) TimeSpan.FromMinutes(3).TotalSeconds);

				// make sure we dont go faster than 15 seconds
				maxBlockInterval = Math.Max(maxBlockInterval, 15);
			}

			// now lets do a play on time
			DateTime syncDeadline = lastSync.AddSeconds(maxBlockInterval);
			DateTime doubleSyncDeadline = lastSync.AddSeconds(maxBlockInterval * 2);

			DateTime now = DateTime.Now;

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

		public IByteArray GetModeratorKeyBytes(byte keyId) {
			MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.GetJoinedField(entry => entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId));

			if(chainKeyEntry == null) {
				return null;
			}

			return (ByteArray) chainKeyEntry.PublicKey;
		}

		public T GetModeratorKey<T>(byte keyId)
			where T : class, ICryptographicKey {

			IByteArray bytes = this.GetModeratorKeyBytes(keyId);

			if(bytes == null) {
				return null;
			}

			return KeyFactory.RehydrateKey<T>(DataSerializationFactory.CreateRehydrator(bytes));
		}

		/// <summary>
		///     insert a new moderator key in the chainstate for quick access
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public void InsertModeratorKey(TransactionId transactionId, byte keyId, IByteArray key) {

			this.UpdateJoinedFields(db => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry.Value.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry != null) {
					throw new ApplicationException($"Moderator key {keyId} is already defined in the chain state");
				}

				chainKeyEntry = new MODERATOR_KEYS_SNAPSHOT();

				chainKeyEntry.OrdinalId = keyId;
				chainKeyEntry.IsCurrent = true;
				chainKeyEntry.PublicKey = key?.ToExactByteArrayCopy();
				chainKeyEntry.DeclarationTransactionId = transactionId.ToString();

				this.chainStateEntry.Value.entry.ModeratorKeys.Add(chainKeyEntry);
			});
		}

		/// <summary>
		///     update a moderator key
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public void UpdateModeratorKey(TransactionId transactionId, byte keyId, IByteArray key) {
			this.UpdateJoinedFields(db => {
				MODERATOR_KEYS_SNAPSHOT chainKeyEntry = this.chainStateEntry.Value.entry.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == keyId);

				if(chainKeyEntry == null) {
					throw new ApplicationException($"Moderator key with ordinal {keyId} does not exist.");
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

			IByteArray bytes = FileExtensions.ReadAllBytes(filePath, this.centralCoordinator.FileSystem);

			TypeSerializer.Deserialize(bytes.Span, out long fileBlockId);

			return this.chainStateEntry.Value.entry.DiskBlockHeight != fileBlockId;
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

			chainStateEntry.MaximumVersionAllowed = new SoftwareVersion(0, 0, 1, 2).ToString();
			chainStateEntry.MinimumWarningVersionAllowed = new SoftwareVersion(0, 0, 1, 2).ToString();
			chainStateEntry.MinimumVersionAllowed = new SoftwareVersion(0, 0, 1, 2).ToString();

			return chainStateEntry;
		}

		protected void UpdateFields(Action<CHAIN_STATE_CONTEXT> action) {

			lock(this.locker) {

				this.ChainStateDal.PerformOperation(db => {

					// always refresh the entry from the database
					this.chainStateEntry = (this.ChainStateDal.LoadSimpleState(db), false);

					action(db);

					db.SaveChanges();
				});

			}
		}

		protected T GetField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function) {
			lock(this.locker) {

				// if we have no entry, we must update it from the DB
				if((this.chainStateEntry == null) || this.BlockIdChanged()) {
					this.ChainStateDal.PerformOperation(db => {

						this.chainStateEntry = (this.ChainStateDal.LoadSimpleState(db), false);
					});
				}

				return function(this.chainStateEntry.Value.entry);
			}
		}

		protected void UpdateJoinedFields(Action<CHAIN_STATE_CONTEXT> action) {

			lock(this.locker) {

				this.ChainStateDal.PerformOperation(db => {

					// always refresh the entry from the database
					this.chainStateEntry = (this.ChainStateDal.LoadFullState(db), true);

					action(db);

					db.SaveChanges();
				});

			}
		}

		protected T GetJoinedField<T>(Func<CHAIN_STATE_SNAPSHOT, T> function) {
			lock(this.locker) {

				// if we have no entry, we must update it from the DB
				if((this.chainStateEntry == null) || !this.chainStateEntry.Value.full) {
					this.ChainStateDal.PerformOperation(db => {

						this.chainStateEntry = (this.ChainStateDal.LoadFullState(db), true);
					});
				}

				return function(this.chainStateEntry.Value.entry);
			}
		}
	}
}