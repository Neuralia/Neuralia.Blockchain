using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Core.Configuration {
	public interface IAppSettingsBase {
		ChainConfigurations GetChainConfiguration(BlockchainType chaintype);
	}

	public abstract class AppSettingsBase : IAppSettingsBase {

		public enum SerializationTypes {
			Master,
			Feeder
		}

		public enum BlockSavingModes : byte {
			None = 0,
			NoneBySync = 1,
			DigestsThenBlocks = 2,
			DigestAndBlocks = 3,
			BlockOnly = 4
		}

		/// <summary>
		///     when we save a digest, to we save the entire digest or only our own snapshots?
		/// </summary>
		public enum DigestSyncModes {
			Whole,
			OwnOnly
		}

		/// <summary>
		///     Should we save blockchain messages?
		/// </summary>
		public enum MessageSavingModes {
			Disabled,
			Enabled
		}

		public enum PassphraseQueryMethod {
			Console,
			Event
		}

		[Flags]
		public enum RpcModes : byte {
			None,
			Signal = 1 << 0,
			Rest = 1 << 1,
			Both = Signal | Rest
		}

		public enum RpcTransports {
			Unsecured,
			Secured
		}

		public enum RpcBindModes {
			Localhost,
			Any
		}

		public enum SnapshotIndexTypes {
			None,
			List,
			All
		}

		/// <summary>
		///     How we handle the transaction pool. Metadata is only the
		///     transaction id connection. full, we also save the full envelope.
		/// </summary>
		public enum TransactionPoolHandling {
			Disabled,
			MiningMetadata,
			MiningFull,
			AlwaysMetadata,
			AlwaysFull
		}

		/// <summary>
		///     In safe mode, it is slower, but we seriously clear the memory. In fast mode we do a regular delete
		/// </summary>
		public enum WalletTransactionDeletionModes {
			Safe,
			Fast
		}

		/// <summary>
		/// If true, we will use faster but larger memory buffers from the array pool. if false, we will use the regular exact sized buffer. slower but less ram
		/// </summary>
		public bool UseArrayPools { get; set; } = true;
		
		public string MothershipUrl { get; set; } = "https://www.neuralium.com";

#if TESTNET
		public string HubsAddress { get; set; } = "test-hubs.neuralium.com";
#elif DEVNET
		public string HubsAddress { get; set; } = "dev-hubs.neuralium.com";
#else
	    public string HubsAddress { get; set; } = "hubs.neuralium.com";
#endif

		public string SystemFilesPath { get; set; }

		/// <summary>
		///     Turn on special behaviors for mobiles
		/// </summary>
		public bool MobileMode { get; set; } = false;

		public int logLevel { get; set; }

		public int acceptableTimeRange { get; set; } = 5; // in minutes

		public int port { get; set; } = GlobalsService.DEFAULT_PORT;
		public int rpcPort { get; set; } = GlobalsService.DEFAULT_RPC_PORT;

		public List<FullNode> Nodes { get; set; } = new List<FullNode>();

		public List<WhitelistedNode> Whitelist { get; set; } = new List<WhitelistedNode>();
		public List<Node> Blacklist { get; set; } = new List<Node>();

		/// <summary>
		///     how does it serialize?  if master, it will have its full blockchain files, and database. if feeder, it simply
		///     observes the files and databases that are updated by a master
		/// </summary>
		public SerializationTypes SerializationType { get; set; } = SerializationTypes.Master;

		/// <summary>
		///     The http url of the mining registration API
		/// </summary>
#if TESTNET
		public string WebElectionsRegistrationUrl { get; set; } = "http://test-election-registration.neuralium.com";
		public string WebRegistrationUrl { get; set; } = "http://test-registration.neuralium.com";
#elif DEVNET
		public string WebElectionsRegistrationUrl { get; set; } = "http://dev-election-registration.neuralium.com";
		public string WebRegistrationUrl { get; set; } = "http://dev-registration.neuralium.com";
#else
	    public string WebElectionsRegistrationUrl { get; set; } = "http://election-registration.neuralium.com"; 
		public string WebRegistrationUrl { get; set; } = "http://registration.neuralium.com";
#endif

		
		/// <summary>
		///     the maximum amount of IPs to keep in our cache
		/// </summary>
		public int MaximumIpCacheCount { get; set; } = 1000;

		public int maxPeerCount { get; set; } = 10;

		public int averagePeerCount { get; set; } = 5;

		/// <summary>
		///     how do we delete the files when doing wallet transctions? safe is slower but clears data much better
		/// </summary>
		public WalletTransactionDeletionModes WalletTransactionDeletionMode { get; set; } = WalletTransactionDeletionModes.Fast;

		/// <summary>
		///     If true, the same IP can connect multiple times with different ports. if false, we allow only one connection by IP
		/// </summary>
		public bool AllowMultipleConnectionsFromSameIp { get; set; } = true;

		/// <summary>
		///     here we can reject IPs from the same CIDR range as ours.
		/// </summary>
		public bool AllowConnectionsFromLocalCIDRRange { get; set; } = true;

		/// <summary>
		///     how long to store a passphrase before it is forgotten. null or -1 is infinite.
		/// </summary>
		public int? PassphraseTimeout { get; set; } = null;

		public byte WalletEncryptionFormat { get; set; } = (byte) EncryptorParameters.SymetricCiphers.XCHACHA_40;

		public ProxySettings ProxySettings { get; set; } = null;

		/// <summary>
		///     Should we contact the hubs if we need to get more peers?
		/// </summary>
		public bool EnableHubs { get; set; } = true;

		/// <summary>
		///     How do we handle the transaction pool? by default, we store only metadata if we are
		///     mining
		/// </summary>
		public TransactionPoolHandling TransactionPoolHandlingMode { get; set; } = TransactionPoolHandling.MiningMetadata;

		/// <summary>
		///     Various configurations only useful for debugging. we dont document them as regular uses should have no use for
		///     them.
		/// </summary>
		public UndocumentedDebugConfigurations UndocumentedDebugConfigurations { get; set; } = new UndocumentedDebugConfigurations();

		public bool UseSTUNServer { get; set; } = false;

		//TODO: set to proper value
		/// <summary>
		///     The amount of time in seconds before we attempt to sync again
		/// </summary>
		public int SyncDelay { get; set; } = 60;

		/// <summary>
		///     The amount of time in seconds before we attempt to sync again
		/// </summary>
		public int WalletSyncDelay { get; set; } = 60;

		/// <summary>
		///     do we delete blocks saved after X many days? its not very nice, so by default, we store them all.
		/// </summary>
		public int? DeleteBlocksAfterDays { get; set; } = null;

		/// <summary>
		///     which RPC modes to enable
		/// </summary>
		public RpcModes RpcMode { get; set; } = RpcModes.Signal;

		/// <summary>
		///     Enable TLS secure communication or not
		/// </summary>
		public RpcTransports RpcTransport { get; set; } = RpcTransports.Unsecured;

		/// <summary>
		///     Do we allow the Rpc to listen only to localhost, or any address
		/// </summary>
		public RpcBindModes RpcBindMode { get; set; } = RpcBindModes.Localhost;

		/// <summary>
		///     The TLS certificate to use. can be a path too, otherwise app root.  if null, a dynamic certificate will be
		///     generated.
		/// </summary>
		public string TlsCertificate { get; set; } = "neuralium.com.rpc.crt";

		public abstract ChainConfigurations GetChainConfiguration(BlockchainType chaintype);

		public class Node {
			public string ip { get; set; }
		}

		public class FullNode : Node {
			public int port { get; set; } = 33888;
		}

		public class WhitelistedNode : Node {

			/// <summary>
			///     If a whitelisted node contacts us, how do we accept them?
			/// </summary>
			public enum AcceptanceTypes {
				/// <summary>
				///     We accept the node if we still have room in our allowed peer list
				/// </summary>
				WithRemainingSlots,

				/// <summary>
				///     We accept the node no matter what, even if we have a saturated peer connection set.
				/// </summary>
				Always
			}

			public AcceptanceTypes AcceptanceType { get; set; } = AcceptanceTypes.WithRemainingSlots;

			public bool CIDR { get; set; } = false;
		}

	#region options

		public bool DisableTimeServer { get; set; } = false;
		public bool DisableP2p { get; set; } = false;
		public bool P2pEnabled => !this.DisableP2p;

	#endregion

	}

	public abstract class ChainConfigurations {

		[Flags]
		public enum FastKeyTypes {
			Transactions = 1 << 0,
			Messages = 1 << 1,
			All = Transactions | Messages
		}

		[Flags]
		public enum RegistrationMethods : byte {
			Web = 1 << 0,
			Gossip = 1 << 1,
			WebOrGossip = Web | Gossip
		}
		
		public enum HashTypes {
			Sha2,
			Sha3
		}

		public bool Enabled { get; set; } = true;

		/// <summary>
		///     how does it serialize?  if master, it will have its full blockchain files, and database. if feeder, it simply
		///     observes the files and databases that are updated by a master
		/// </summary>
		public AppSettingsBase.SerializationTypes SerializationType { get; set; } = AppSettingsBase.SerializationTypes.Master;

		/// <summary>
		///     The http url of the mining registration API
		/// </summary>
#if TESTNET
		public string WebElectionsRegistrationUrl { get; set; } = "http://test-election-registration.neuralium.com";
		public string WebRegistrationUrl { get; set; } = "http://test-registration.neuralium.com";
#elif DEVNET
		public string WebElectionsRegistrationUrl { get; set; } = "http://dev-election-registration.neuralium.com";
		public string WebRegistrationUrl { get; set; } = "http://dev-registration.neuralium.com";
#else
	    public string WebElectionsRegistrationUrl { get; set; } = "http://election-registration.neuralium.com"; 
		public string WebRegistrationUrl { get; set; } = "http://registration.neuralium.com";
#endif
		
		/// <summary>
		///     If true, during the wallet sync, the public block height will be updated, causing a creaping sync target. at false,
		///     it will sync with the height it had when it started,
		///     even if it changes along the way.
		/// </summary>
		public bool AllowWalletSyncDynamicGrowth { get; set; } = false;

		/// <summary>
		///     How do we want to capture the passphrases.
		/// </summary>
		public AppSettingsBase.PassphraseQueryMethod PassphraseCaptureMethod { get; set; } = AppSettingsBase.PassphraseQueryMethod.Event;

		/// <summary>
		///     if true, the wallet will be loaded at chain start automatically. Otherwise, only on demand if transactions are
		///     created.
		/// </summary>
		public bool LoadWalletOnStart { get; set; } = false;

		/// <summary>
		///     if true, we will create a new wallet if it is missing. otherwise, we will continue without a wallet
		/// </summary>
		public bool CreateMissingWallet { get; set; } = false;

		/// <summary>
		///     Should we encrypt the wallet keys when creating a new wallet
		/// </summary>
		public bool EncryptWallet { get; set; } = false;

		/// <summary>
		///     Should we encrypt the wallet keys when creating a new wallet
		/// </summary>
		public bool EncryptWalletKeys { get; set; } = false;

		/// <summary>
		///     Should each key have its own passphrase, or share the same
		/// </summary>
		public bool EncryptWalletKeysSeparate { get; set; } = false;

		/// <summary>
		/// if enabled, the noe will use the mining pool facilities to check if correctly registered for mining
		/// </summary>
		public bool EnableMiningStatusChecks { get; set; } = true;

		/// <summary>
		///     The minimum amount of peers required to sync. 1 peer is VERY risky. 2 is a bit better but not by much. A proper
		///     minimum is 3 peers.
		/// </summary>

		//TODO: should this be 3 for prod??
		public int MinimumSyncPeerCount { get; set; } = 2;

		public int MinimumDispatchPeerCount { get; set; } = 2;

		/// <summary>
		///     How we determine the max size of a block group file. If we have a block count mode, then its the maximum number of
		///     blocks.
		///     if we are in file size mode, then its the maximum number of bytes.
		/// </summary>
		public int? BlockFileGroupSize { get; set; } = null;

		/// <summary>
		///     At which interval will we insert a new L1 entry.
		/// </summary>
		public int BlockCacheL1Interval { get; set; } = 100;

		/// <summary>
		///     At which interval will we insert a new L2 entry.A higher number takes less space, but will require reading more
		///     data from disk. its a balancing act.
		/// </summary>
		public int BlockCacheL2Interval { get; set; } = 10;

		/// <summary>
		///     How we determine the max size of a block group file. If we have a block count mode, then its the maximum number of
		///     blocks.
		///     if we are in file size mode, then its the maximum number of bytes.
		/// </summary>
		public int? MessageFileGroupSize { get; set; } = null;

		public AppSettingsBase.MessageSavingModes MessageSavingMode { get; set; } = AppSettingsBase.MessageSavingModes.Disabled;

		/// <summary>
		///     The keylog is a security feature. it can be disabled if necessary, but it is not advised. This is part of the
		///     wallet block sync, and keylog will be disabled if wallet block sync is disabled also
		/// </summary>
		public bool UseKeyLog { get; set; } = true;

		/// <summary>
		///     do we want to disable the block sync with other peers?
		/// </summary>
		public bool DisableSync { get; set; } = false;

		/// <summary>
		///     do we want to disable the wallet block sync?
		/// </summary>
		public bool DisableWalletSync { get; set; } = false;

		public KeySecurityConfigurations KeySecurityConfigurations { get; set; } = new KeySecurityConfigurations();

		public AppSettingsBase.SnapshotIndexTypes AccountSnapshotTrackingMethod { get; set; } = AppSettingsBase.SnapshotIndexTypes.None;

		/// <summary>
		///     Which accounts snapshots do we wish to track?
		/// </summary>
		public List<(long accountId, byte accountType)> TrackedSnapshotAccountsList { get; set; } = new List<(long accountId, byte accountType)>();

		/// <summary>
		///     how do we save the events on chain
		/// </summary>
		public AppSettingsBase.BlockSavingModes BlockSavingMode { get; set; } = AppSettingsBase.BlockSavingModes.DigestsThenBlocks;

		/// <summary>
		///     should we use a fast key index? takes more disk space, but makes verification much faster
		///     by keeping fast access to the General and Message keys
		/// </summary>
		public bool EnableFastKeyIndex { get; set; } = true;

		public FastKeyTypes EnabledFastKeyTypes { get; set; } = FastKeyTypes.All;

		/// <summary>
		///     How many parallel validation threads can we have
		/// </summary>
		public int MaxValidationParallelCount { get; set; } = 3;

		/// <summary>
		///     How many parallel workflow threads can we have at a maximum in this chain
		/// </summary>
		public int? MaxWorkflowParallelCount { get; set; } = null;

		/// <summary>
		///     If we receive a gossip block message and it is a blockID with this distance from our blockheight, then we cache it
		///     to reuse later.
		/// </summary>
		public int BlockGossipCacheProximityLevel { get; set; } = 1000;

		public bool SkipDigestHashVerification { get; set; } = false;

		public bool SkipGenesisHashVerification { get; set; } = true;

		public bool SkipPeriodicBlockHashVerification { get; set; } = true;

		/// <summary>
		///     Use the rest webapi to register for mining. its simpler and faster, so its preferable to use. it is also required
		///     if the peer can not open it's default port through the firewall
		/// </summary>
		public RegistrationMethods ElectionsRegistrationMethod { get; set; } = RegistrationMethods.Gossip;
		
		/// <summary>
		///     Use the rest webapi to register transactions & messages. its sipler, faster and bypasses p2p transaction limits, so its preferable to use. 
		/// </summary>
		public RegistrationMethods RegistrationMethod { get; set; } = RegistrationMethods.Gossip;
		
		/// <summary>
		///     Time in minutes to store a wallet passphrase in memory before wiping it out
		/// </summary>
		public int? DefaultWalletPassphraseTimeout { get; set; } = null;

		/// <summary>
		///     Time in minutes to store a key's passphrase in memory before wiping it out
		/// </summary>
		public int? DefaultKeyPassphraseTimeout { get; set; } = null;

		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte TransactionXmssKeyTreeHeight { get; set; } = 9;

		/// <summary>
		///     Percentage level where we warn of a key change comming
		/// </summary>
		public float TransactionXmssKeyWarningLevel { get; set; } = 0.7F;

		/// <summary>
		///     Percentage level where we must begin the key change process
		/// </summary>
		public float TransactionXmssKeyChangeLevel { get; set; } = 0.9F;

		/// <summary>
		///     the hashing algorithm to use for the keys. Sha3 is currently slower than sha2
		/// </summary>
		public HashTypes TransactionXmssKeyHashType { get; set; } = HashTypes.Sha3;

		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte MessageXmssKeyTreeHeight { get; set; } = 13;

		/// <summary>
		///     Percentage level where we warn of a key change comming
		/// </summary>
		public float MessageXmssKeyWarningLevel { get; set; } = 0.7F;

		/// <summary>
		///     Percentage level where we must begin the key change process
		/// </summary>
		public float MessageXmssKeyChangeLevel { get; set; } = 0.9F;

		/// <summary>
		///     the hashing algorithm to use for the keys. Sha3 is currently slower than sha2
		/// </summary>
		public HashTypes MessageXmssKeyHashType { get; set; } = HashTypes.Sha2;

		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte ChangeXmssKeyTreeHeight { get; set; } = 7;

		/// <summary>
		///     Percentage level where we warn of a key change comming
		/// </summary>
		public float ChangeXmssKeyWarningLevel { get; set; } = 0.7F;

		/// <summary>
		///     Percentage level where we must begin the key change process
		/// </summary>
		public float ChangeXmssKeyChangeLevel { get; set; } = 0.9F;

		/// <summary>
		///     the hashing algorithm to use for the keys. Sha3 is currently slower than sha2
		/// </summary>
		public HashTypes ChangeXmssKeyHashType { get; set; } = HashTypes.Sha3;
	}

	public class KeySecurityConfigurations {

		/// <summary>
		///     If enabled, the chain will ensure to keep track of keys height in the chainstate relative to the transactions
		///     confirmed in blocks. This will ensure that copied
		///     wallets will not reuse a key height if it was already used once.
		/// </summary>
		public bool EnableKeyHeightChecks { get; set; } = true;

		/// <summary>
		///     Enable key height checking for the general key
		/// </summary>
		public bool CheckTransactionKeyHeight { get; set; } = true;

		/// <summary>
		///     Enable key height checking for the backup key
		/// </summary>
		public bool CheckSuperKeyHeight { get; set; } = false;
	}

	public class UndocumentedDebugConfigurations {
		/// <summary>
		///     should we disable the mining registration process?
		/// </summary>
		public bool DisableMiningRegistration { get; set; } = false;

		public bool DebugNetworkMode { get; set; } = false;

		// if true, we expect to operate in locahost only and wont expect a network interface to be present
		public bool localhostOnly { get; set; } = false;

		/// <summary>
		///     a debug option to skip if a peer is a hub.. useful to test the hubs, but otherwise not healthy for peers
		/// </summary>
		public bool SkipHubCheck { get; set; } = false;
	}

	public class ProxySettings {
		public string Host { get; set; }
		public int Port { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
	}
}