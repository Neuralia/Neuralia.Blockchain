using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Core.Configuration {
	/// <summary>
	/// Base Interface For Application settings.
	/// </summary>
	public interface IAppSettingsBase {
		/// <summary>
		/// Get or sets the chain configuration based on the chain type.
		/// </summary>
		/// <param name="chainType"></param>
		/// <returns><value>ChainConfigurations</value></returns>
		ChainConfigurations GetChainConfiguration(BlockchainType chainType);
	}
	/// <summary>
	/// This class contains the base default configurations used to run the Neuralium Blockchain Application at startup to control the behaviour of different aspects of the application.
	/// </summary>
	public abstract class AppSettingsBase : IAppSettingsBase {

		public AppSettingsBase() {
			
		}
		
		/// <summary>
		///  Describes how the Blockchain logging will be scoped.
		/// </summary>
		public enum ChainScopedLoggingTypes {
			/// <summary>
			/// Never perform scoped logging.
			/// </summary>
			Never,
			/// <summary>
			/// Only perform scoped logging when more than one chains are available ...TODO 
			/// </summary>
			WhenMany,
			/// <summary>
			/// Always perform scoped logging.
			/// </summary>
			Always
		}

		/// <summary>
		/// How are you going to use the socket to send and receive data?
		/// </summary>
		public enum SocketTypes {
			/// <summary>
			/// Full Duplex socket connection. Data can be both transmitted and received through the socket.
			/// </summary>
			Duplex,
			/// <summary>
			/// Connection-oriented sockets, which use Transmission Control Protocol (TCP),
			/// A stream socket provides a sequenced and unique flow of error-free data without record boundaries,
			/// with well-defined mechanisms for creating and destroying connections and reporting errors.
			/// A stream socket transmits data reliably, in order, and with out-of-band capabilities
			/// </summary>
			Stream
		}
		
		/// <summary>
		/// Describes how Serialization is to be performed.
		/// </summary>
		public enum SerializationTypes {
			/// <summary>
			/// Perform serialization of the full blockchain files and the database.
			/// </summary>
			Main,
			/// <summary>
			/// Only observe the files and databases that are updated by master.
			/// </summary>
			Secondary
		}

		/// <summary>
		/// Describes how the blocks from the chain will be saved to save saved to disk. 
		/// </summary>
		public enum BlockSavingModes : byte {
			/// <summary>
			/// No blocks are saved.
			/// </summary>
			None = 0,
			/// <summary>
			/// No blocks are saved during a blockchain sync.
			/// </summary>
			NoneBySync = 1,
			/// <summary>
			/// Only blocks are saved
			/// </summary>
			BlockOnly = 2,
			/// <summary>
			/// First the Digest and then blocks are saved from the chain.
			/// </summary>
			/// <remarks> If the block data is contained in the digest then block is not explicitly saved</remarks>
			DigestThenBlocks = 3,
			/// <summary>
			/// Both Digest and Blocks are saved from the chain.
			/// </summary>
			DigestAndBlocks = 4

		}

		/// <summary>
		/// Describes how the blockchain nodes in the peer-to-peer network are going to communicate.
		/// </summary>
		[Flags]
		public enum ContactMethods : byte {
			/// <summary>
			/// Use Web Registration(WebReg). Initially created to accommodate the more limited mobile devices, it
			/// has been expanded to the entire network as an option to transaction dispatch. WebReg consists
			/// in a series of HTTPS web services that are available to post transactions directly to the pool,
			/// bypassing the gossip protocol entirely. 
			/// </summary>
			Web = 1,
			/// <summary>
			/// Use a custom Gossip protocol as the backbone of the p2p network to propagate message between nodes.
			/// It ensures that messages are propagated to all participating nodes in a viral manner. The protocol
			/// is built in such a way that it will batch messages together for optimization and provide an
			/// opportunity for a node to determine which messages it wants to receive, and which ones it
			/// wants to ignore. By maintaining a message registry, a node is in measure to accept or reject
			/// messages efficiently which results in immediate echo attenuation.
			/// </summary>
			Gossip = 1 << 1,
			/// <summary>
			/// Use first Web registration first and the if possible Gossip.
			/// </summary>
			WebOrGossip = Web | Gossip
		}

		/// <summary>
		/// Describes how Digest sync is performed.
		/// </summary>
		/// <remarks>
		/// When we save a digest, do we save the entire digest or only our own digest snapshots?
		/// </remarks>
		public enum DigestSyncModes {
			/// <summary>
			/// Save the whole digest
			/// </summary>
			Whole, 
			/// <summary>
			/// Save only own digest snapshots
			/// </summary>
			OwnOnly
		}

		/// <summary>
		/// Describes the mode by which messages are saved.
		/// </summary>
		public enum MessageSavingModes {
			/// <summary>
			/// Blockchain message saving is disabled
			/// </summary>
			Disabled,
			
			/// <summary>
			/// Blockchain message saving is enabled
			/// </summary>
			Enabled
		}

		/// <summary>
		/// What method to use for querying the passphrase?
		/// </summary>
		public enum PassphraseQueryMethod {
			/// <summary>
			/// Use console method. TODO: describe
			/// </summary>
			Console,
			/// <summary>
			/// Use Event method. TODO: describe
			/// </summary>
			Event,
			/// <summary>
			/// Use Other method. TODO: describe
			/// </summary>
			Other
		}
		
		/// <summary>
		/// Describes the method by which Remote Procedure calls (RPC) are made.
		/// </summary>
		[Flags]
		public enum RpcModes : byte {
			/// <summary>
			/// RPC is not used
			/// </summary>
			None,
			/// <summary>
			/// Using SignalIR to resolve RPC calls.
			/// </summary>
			Signal = 1 << 0,
			/// <summary>
			/// Using REST API to resolve RPC calls.
			/// </summary>
			Rest = 1 << 1,
			/// <summary>
			/// Using both SignalIR and Rest to resolve RPC calls.
			/// </summary>
			Both = Signal | Rest
		}
		
		/// <summary>
		/// Describes the way Remote Procedure calls(RPC) transport is done.
		/// </summary>
		public enum RpcTransports {
			/// <summary>
			/// No extra security aded.
			/// </summary>
			Unsecured,
			/// <summary>
			/// Using X509Certificate2 rpc certificate and http redirection.
			/// </summary>
			Secured
		}

		/// <summary>
		/// 
		/// </summary>
		public enum RpcBindModes {
			/// <summary>
			/// 
			/// </summary>
			Localhost,
			/// <summary>
			/// 
			/// </summary>
			Any
		}

		/// <summary>
		/// 
		/// </summary>
		public enum RpcLoggingLevels {
			Information,
			Verbose
		}

		public enum SnapshotIndexTypes {
			None,
			List,
			All
		}
		
		/// <summary>
		/// 
		/// </summary>
		public enum MemoryCheckModes {
			Disabled,
			Virtual,
			CGroup
		}

		/// <summary>
		/// How much CPU to dedicate to XMSS operations.\n
		/// Default: Half of the available processor cores ::Neuralia::Blockchains::Core::Enums::ThreadMode.
		/// </summary>
		public Enums.ThreadMode XmssThreadMode { get; set; } = Enums.ThreadMode.Half;

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
		/// Describes how handle wallet transaction deletion.
		///     
		/// </summary>
		public enum WalletTransactionDeletionModes {
			/// <summary>
			/// Perform safe wallet transaction deletion. This mode emphasis on serious memory clearing. It is slower.
			/// </summary>
			Safe,
			/// <summary>
			/// Perform fast transaction deletion. In this mode regular deletes are performed, therefore faster.
			/// </summary>
			Fast
		}

		/// <summary>
		/// When performing a THS, do we use ram, or harddisk (slower)
		/// </summary>
		public Enums.THSMemoryTypes THSMemoryType { get; set; } = Enums.THSMemoryTypes.RAM;

		/// <summary>
		/// how many thraeds to use for the THS
		/// </summary>
		public int THSThreadCount { get; set; } = 1;
		
		/// <summary>
		/// which protocol should we try to use
		/// </summary>
		public IPMode IPProtocol { get; set; } = IPMode.Both;

		/// <summary>
		///     What type of tcp socket to use
		/// </summary>
		public SocketTypes SocketType { get; set; } = SocketTypes.Duplex;

		/// <summary>
		///     If true, we will use faster but larger memory buffers from the array pool. if false, we will use the regular exact
		///     sized buffer. slower but less ram
		/// </summary>
		public bool UseArrayPools { get; set; } = true;

		public string SystemFilesPath { get; set; }

		/// <summary>
		///     Turn on special behaviors for mobiles.
		/// </summary>
		/// <value>Default is false</value>
		public bool MobileMode { get; set; } = false;

		/// <summary>
		///     Turn on special behaviors for syncless.
		/// </summary>
		/// <value>Default is false</value>
		public bool SynclessMode { get; set; } = false;

		/// <summary>
		/// 
		/// </summary>
		public int LogLevel { get; set; }

		/// <summary>
		/// Which port are we arunning the server.
		/// </summary>
		public int Port { get; set; } = GlobalsService.DEFAULT_PORT;
		
		/// <summary>
		/// Gets or sets the port on which the validator will listen on. 
		/// </summary>
		/// <returns>Returns the port number to use. Default value: <see cref="GlobalsService.DEFAULT_VALIDATOR_PORT"/></returns>
		public int ValidatorPort { get; set; } = GlobalsService.DEFAULT_VALIDATOR_PORT;
		
		/// <summary>
		/// 
		/// </summary>
		public int ValidatorHttpPort { get; set; } = GlobalsService.DEFAULT_VALIDATOR_HTTP_PORT;
		
		/// <summary>
		/// 
		/// </summary>
		public int RpcPort { get; set; } = GlobalsService.DEFAULT_RPC_PORT;

		/// <summary>
		/// if true, the port 80 HTTP backup protocol will be started and used
		/// </summary>
		public bool EnableAppointmentValidatorBackupProtocol { get; set; } = true;

		/// <summary>
		/// should the IPMarshall be enabled during appointments. some may wish to disable it due to proxies 
		/// </summary>
		public bool EnableAppointmentValidatorIPMarshall { get; set; } = true;

		/// <summary>
		/// 
		/// </summary>
		public bool UseUPnP { get; set; } = true;
		/// <summary>
		/// 
		/// </summary>
		public bool UsePmP { get; set; } = false;
	
		/// <summary>
		/// 
		/// </summary>
		public List<FullNode> Nodes { get; set; } = new ();

		/// <summary>
		/// Connections to local nodes will be maintained aggressively, and won't count in totals, meant to be used with each-others under a LAN
		/// </summary>
		public List<FullNode> LocalNodes { get; set; } = new ();

		/// <summary>
		/// 
		/// </summary>
		public List<WhitelistedNode> Whitelist { get; set; } = new ();
		
		/// <summary>
		/// 
		/// </summary>
		public List<Node> Blacklist { get; set; } = new ();

		/// <summary>
		/// Gets of sets the logger types to be enabled in the application.
		/// </summary>
		/// <value>Returns the type of logger NLog.LoggerTypes. Empty means all.</value>
		public List<NLog.LoggerTypes> EnabledLoggers { get; set; } = new List<NLog.LoggerTypes>{NLog.LoggerTypes.Standard}; 
		
		/// <summary>
		///     how does it serialize?  if main, it will have its full blockchain files, and database. if feeder, it simply
		///     observes the files and databases that are updated by a master
		/// </summary>
		public SerializationTypes SerializationType { get; set; } = SerializationTypes.Main;

		/// <summary>
		///     Use the rest webapi to register transactions & messages. its sipler, faster and bypasses p2p transaction limits, so
		///     its preferable to use.
		/// </summary>
		public ContactMethods HubContactMethod { get; set; } = ContactMethods.WebOrGossip;

		/// <summary>
		/// Gets or sets the way that scoped logging is done for the blockchains.
		/// </summary>
		/// <value>For possible values see <see cref="ChainScopedLogging"/>. Default value is <see cref="ChainScopedLoggingTypes.WhenMany"./></value>
		/// <returns>The type of blockchain scoped logging. For possible types see: <see cref="ChainScopedLoggingTypes"/></returns>
		public ChainScopedLoggingTypes ChainScopedLogging { get; set; } = ChainScopedLoggingTypes.WhenMany;
		

#if TESTNET
		public string HubsGossipDNS { get; set; } = "test-hubs.neuralium.com";
public string PortTestDns { get; set; } = "test-port-test.neuralium.com";
		public string HubsWebAddress { get; set; } = "https://test-web-hubs.neuralium.com";
#elif DEVNET
		public string HubsGossipDNS { get; set; } = "dev-hubs.neuralium.com";
		public string PortTestDns { get; set; } = "dev-port-test.neuralium.com";
		public string HubsWebAddress { get; set; } = "http://dev-web-hubs.neuralium.com";
#else
	    public string HubsGossipDNS { get; set; } = "hubs.neuralium.com";
		public string PortTestDns { get; set; } = "port-test.neuralium.com";
		public string HubsWebAddress { get; set; } = "https://web-hubs.neuralium.com";
#endif
		
		/// <summary>
		/// if set, it will override the DNS check
		/// </summary>
		public string PortTestIpOverride { get; set; }

		/// <summary>
		///     the maximum amount of IPs to keep in our cache
		/// </summary>
		public int MaximumIpCacheCount { get; set; } = 1000;

		/// <summary>
		/// 
		/// </summary>
		public int MaxPeerCount { get; set; } = 10;
		
		/// <summary>
		/// 
		/// </summary>
		public int MaxNonConnectablePeerCount { get; set; } = 3;

		/// <summary>
		/// 
		/// </summary>
		public int AveragePeerCount { get; set; } = 5;
		
		/// <summary>
		/// 
		/// </summary>
		public int MaxMobilePeerCount { get; set; } = 30;
		
		/// <summary>
		/// 
		/// </summary>
		public double HubIPsRequestPeriod { get; set; } = 1800;
		
		/// <summary>
		/// 
		/// </summary>
		public double PeerIPsRequestPeriod { get; set; } = 600;
		
		/// <summary>
		/// 
		/// </summary>
		public double PeerReconnectionPeriod { get; set; } = 60;

		public double IPCrawlerStartupDelay { get; set; } = 5.0;

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
		public bool AllowConnectionsFromLocalCidrRange { get; set; } = true;

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

		public bool UseStunServer { get; set; } = false;

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
		/// gets or sets which RPC mode to enable.
		/// <returns>see: <see cref="RpcModes"/>. Default value: <see cref="RpcModes.Signal"/></returns>
		/// </summary>
		public RpcModes RpcMode { get; set; } = RpcModes.Signal;

		/// <summary>
		/// Gets or sets the type of remote Procedure call transport.
		/// </summary>
		/// <remarks> Enable Transport Layer Security (TLS) secure communication or not.</remarks>
		/// <returns>return <see cref="RpcTransport"/>. Default value is <see cref="RpcTransports.Unsecured"/>.</returns>
		public RpcTransports RpcTransport { get; set; } = RpcTransports.Unsecured;

		/// <summary>
		/// Gets or sets the RPC bidning mode.
		///     Do we allow the Rpc to listen only to localhost, or any address
		/// </summary>
		public RpcBindModes RpcBindMode { get; set; } = RpcBindModes.Localhost;

		/// <summary>
		///     What level should we use for Rpc message logging
		/// </summary>
		public RpcLoggingLevels RpcLoggingLevel { get; set; } = RpcLoggingLevels.Information;

		/// <summary>
		///     The TLS certificate to use. can be a path too, otherwise app root.  if null, a dynamic certificate will be
		///     generated.
		/// </summary>
		public string TlsCertificate { get; set; } = "neuralium.com.rpc.crt";

		/// <summary>
		/// should we use memory limits
		/// </summary>
		/// <remarks>CGroup this is very OS specific and best suited for containers, may not be set on the OS and not all methods may be implemented. see latest documentation</remarks>
		public MemoryCheckModes MemoryLimitCheckMode { get; set; } = MemoryCheckModes.Disabled;
		
		/// <summary>
		/// if 0, we use virtual memory. If set, we use this limit in BYTES
		/// </summary>
		public long TotalUsableMemory { get; set; } = 0;
		
		/// <summary>
		/// start warning that we are using a lot of memory
		/// </summary>
		public double MemoryLimitWarning { get; set; } = 0.7;
		
		/// <summary>
		/// stop the app, if we reach this and limits are enabled
		/// </summary>
		public double MemoryLimit { get; set; } = 0.9;
		
		/// <summary>
		/// How many requesters do we expect to get in any appointment validation
		/// </summary>
		public int TargetAppointmentRequesterCount { get; set; } = 30;
		
		/// <summary>
		/// 
		/// </summary>
		public abstract ChainConfigurations GetChainConfiguration(BlockchainType chainType);

		/// <summary>
		///  This class represents a basic p2p network node.
		/// </summary>
		public class Node {
			/// <summary>
			/// Gets or sets the IP of the node.
			/// </summary>
			/// <returns>The IP address of the node.</returns>
			public string Ip { get; set; }
		}

		/// <summary>
		/// This class represents a peer-to-peer Node with a connectable port.
		/// </summary>
		public class FullNode : Node {
			/// <summary>
			/// Gets or sets the port of the node.
			/// <see cref="GlobalsService.DEFAULT_PORT"/>
			/// </summary>
			/// <returns>The <see cref="int"/> value of the node port. Default value: <see cref="GlobalsService.DEFAULT_PORT"/></returns>
			public int Port { get; set; } = GlobalsService.DEFAULT_PORT;
		}

		/// <summary>
		/// This class represents a peer-to-peer node that has already been white listed and is trustable. 
		/// </summary>
		public class WhitelistedNode : Node {

			/// <summary>
			/// Describes the the way be which we accept other p2p white listed nodes when they are contacting the node.
			/// </summary>
			public enum AcceptanceTypes {
				/// <summary>
				/// The white listed node is only accepted if there is still room in the allowed peer list
				/// </summary>
				WithRemainingSlots,

				/// <summary>
				///  Always accept the incoming connection node, no matter what, even if a saturated peer connection is set.
				/// </summary>
				Always
			}

			/// <summary>
			///  Gets or 
			/// </summary>
			public AcceptanceTypes AcceptanceType { get; set; } = AcceptanceTypes.WithRemainingSlots;

			public bool CIDR { get; set; } = false;
		}

	#region options

		public bool DisableTimeServer { get; set; } = false;
		public bool DisableP2P { get; set; } = false;
		public bool P2PEnabled => !this.DisableP2P;

	#endregion

	}

	/// <summary>
	/// This class takes care of configurations related with the Blockchain
	/// </summary>
	public abstract class ChainConfigurations {

		/// <summary>
		/// 
		/// </summary>
		[Flags]
		public enum KeyDictionaryTypes {
			Transactions = 1 << 0,
			Messages = 1 << 1,
			All = Transactions | Messages
		}

		/// <summary>
		/// 
		/// </summary>
		public enum HashTypes {
			/// <summary>
			///  Using Sha2 hashing
			/// </summary>
			Sha2,
			/// <summary>
			/// Using Sha3 hashing
			/// </summary>
			Sha3
		}
		
		/// <summary>
		/// 
		/// </summary>
		[Flags]
		public enum MiningStatisticsModes {
			None,
			Total = 1 << 0,
			Session = 1 << 1,
			Both = Session | Total
			
		}

		/// <summary>
		///     how long to store a passphrase before it is forgotten. null or -1 is infinite.
		/// </summary>
		public int? PassphraseTimeout { get; set; } = null;

		/// <summary>
		/// 
		/// </summary>
		public EncryptorParameters.SymetricCiphers WalletEncryptionFormat { get; set; } = EncryptorParameters.SymetricCiphers.XCHACHA_20_POLY_1305;

		/// <summary>
		/// 
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// should we enable automatic transaction retry?
		/// </summary>
		public bool EnableAutomaticRetry{ get; set; } = true;
		
		/// <summary>
		///     Gets or sets the way serialization will be performed.
		/// </summary>
		/// <remarks><para>If master is set the full blockchain files, and database will be serialized.</para>
		/// If feeder is set simply observes the files and databases that are updated by a master</remarks>
		/// <returns>The type of serialization to be performed. <see cref="AppSettingsBase.SerializationTypes"/></returns>
		public AppSettingsBase.SerializationTypes SerializationType { get; set; } = AppSettingsBase.SerializationTypes.Main;

		/// <summary>
		///     The http url of the mining registration API
		/// </summary>
#if TESTNET
		
		/// <summary>
		///     The http url of the hash server
		/// </summary>
		public string HashUrl { get; set; } = "https://test-hash.neuralium.com";
		public string WebElectionsRegistrationUrl { get; set; } = "https://test-election-registration.neuralium.com";
		public string WebElectionsRecordsUrl { get; set; } = "https://test-election-records.neuralium.com";
		public string WebElectionsStatusUrl { get; set; } = "https://test-election-status.neuralium.com";
		public string WebPresentationRegistrationUrl { get; set; } = "https://test-presentation-registration.neuralium.com";
		public string WebTransactionRegistrationUrl { get; set; } = "https://test-transaction-registration.neuralium.com";
		public string WebMessageRegistrationUrl { get; set; } = "https://test-message-registration.neuralium.com";
		public string WebAppointmentsRegistrationUrl { get; set; } = "https://test-appointments-registration.neuralium.com";
		public string WebTransactionPoolUrl { get; set; } = "https://test-transaction-pool.neuralium.com";
		public string WebSyncUrl { get; set; } = "https://test-sync.neuralium.com";
#elif DEVNET
		public string HashUrl { get; set; } = "http://dev-hash.neuralium.com";
		public string WebElectionsRegistrationUrl { get; set; } = "http://dev-election-registration.neuralium.com";
		public string WebElectionsRecordsUrl { get; set; } = "http://dev-election-records.neuralium.com";
		public string WebElectionsStatusUrl { get; set; } = "http://dev-election-status.neuralium.com";
		public string WebPresentationRegistrationUrl { get; set; } = "http://dev-presentation-registration.neuralium.com";
		public string WebTransactionRegistrationUrl { get; set; } = "http://dev-transaction-registration.neuralium.com";
		public string WebMessageRegistrationUrl { get; set; } = "http://dev-message-registration.neuralium.com";
		public string WebAppointmentsRegistrationUrl { get; set; } = "http://dev-appointments-registration.neuralium.com";
		public string WebTransactionPoolUrl { get; set; } = "http://dev-transaction-pool.neuralium.com";
		public string WebSyncUrl { get; set; } = "http://dev-sync.neuralium.com";
#else
		public string HashUrl { get; set; } = "https://hash.neuralium.com";
	    public string WebElectionsRegistrationUrl { get; set; } = "https://election-registration.neuralium.com";
		public string WebElectionsRecordsUrl { get; set; } = "https://election-records.neuralium.com";
		public string WebElectionsStatusUrl { get; set; } = "https://election-status.neuralium.com";
		public string WebPresentationRegistrationUrl { get; set; } = "https://presentation-registration.neuralium.com";
		public string WebTransactionRegistrationUrl { get; set; } = "https://transaction-registration.neuralium.com";
		public string WebMessageRegistrationUrl { get; set; } = "https://message-registration.neuralium.com";
		public string WebAppointmentsRegistrationUrl { get; set; } = "https://appointments-registration.neuralium.com";
		public string WebTransactionPoolUrl { get; set; } = "https://transaction-pool.neuralium.com";
		public string WebSyncUrl { get; set; } = "https://sync.neuralium.com";
#endif
		

		/// <summary>
		/// if true, we will contact the web transaction pools to get the webreg ones
		/// </summary>
		public bool UseWebTransactionPool  { get; set; } = true;
		
		/// <summary>
		///     Gets or sets weather If true, during the wallet sync, the public block height will be updated, causing a creaping sync target. at false,
		///     it will sync with the height it had when it started,
		///     even if it changes along the way.
		/// </summary>
		public bool AllowWalletSyncDynamicGrowth { get; set; } = false;

		/// <summary>
		///     How do we want to capture the passphrases.
		/// </summary>
		public AppSettingsBase.PassphraseQueryMethod PassphraseCaptureMethod { get; set; } = AppSettingsBase.PassphraseQueryMethod.Event;

		/// <summary>
		///     if true, mining can be enabled even chain is not synced. mining wlil start when fully synced
		/// </summary>
		public bool EnableMiningPreload { get; set; } = false;

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
		/// if we auto create wallet, what type of account to create
		/// </summary>
		public Enums.AccountTypes AccountType { get; set; } = Enums.AccountTypes.User;
		
		/// <summary>
		///     Should we encrypt the wallet keys when creating a new wallet
		/// </summary>
		public bool EncryptWallet { get; set; } = false;
	
		/// <summary>
		/// Gets to sets a value indicator weather the wallet should be compressed upon creation.
		/// </summary>
		/// <value>Default value is true.</value>
		public bool CompressWallet { get; set; } = true;

		/// <summary>
		///     Should we encrypt the wallet keys when creating a new wallet
		/// </summary>
		public bool EncryptWalletKeys { get; set; } = false;

		/// <summary>
		///     Should each key have its own passphrase, or share the same
		/// </summary>
		/*!
		 @see EncryptWalletKeysSeparate
		 */
		public bool EncryptWalletKeysSeparate { get; set; } = false;

		/// <summary>
		///     if enabled, the noe will use the mining pool facilities to check if correctly registered for mining
		/// </summary>
		public bool EnableMiningStatusChecks { get; set; } = true;

		/// <summary>
		///     The minimum amount of peers required to sync. 1 peer is VERY risky. 2 is a bit better but not by much. A proper
		///     minimum is 3 peers.
		/// </summary>

		//TODO: should this be 3 for prod??
		public int MinimumSyncPeerCount { get; set; } = 1;

		public int MinimumDispatchPeerCount { get; set; } = 1;

		/// <summary>
		///     How we determine the max size of a block group file. <br/> <br/> <br/>
		///		If we have a block count mode, then its the maximum number of blocks.<br/>
		///     If we are in file size mode, then its the maximum number of bytes.<br/>
		/// </summary>
		public int? BlockFileGroupSize { get; set; } = null;

		/// <summary>
		///     At which interval will we insert a new L1 entry.
		/// </summary>
		public int BlockCacheL1Interval { get; set; } = 100;

		/// <summary>
		///     At which interval will we insert a new L2 entry.A higher number takes less space, but will require reading more
		///     data from disk. It's a balancing act.
		/// </summary>
		public int BlockCacheL2Interval { get; set; } = 10;

		/// <summary>
		///     How are we determining the max size of a block group file?
		///		If we have a block count mode, then its the maximum number of blocks.
		///     If we are in file size mode, then its the maximum number of bytes.
		/// </summary>
		public int? MessageFileGroupSize { get; set; } = null;

		public AppSettingsBase.MessageSavingModes MessageSavingMode { get; set; } = AppSettingsBase.MessageSavingModes.Disabled;

		/// <summary>
		///     Should we publish our key indices inside transactions for key logging? (recommended)
		/// </summary>
		public bool PublishKeyUseIndices { get; set; } = true;
		
		/// <summary>
		///     Should we publish the limit at which a previous key index will not be usable? (highly recommended)
		/// </summary>
		public bool PublishKeyUseLocks { get; set; } = true;
		

		/// <summary>
		///     The keylog is an important security feature. If necessary, it can be disabled, but it is not advised AT ALL.
		///		This is part of the wallet block sync, and keylog will be disabled if wallet block sync is disabled also.
		/// </summary>
		public bool UseKeyLog { get; set; } = true;

		/// <summary>
		///     Do we want to disable the block sync with other peers?
		/// </summary>
		public bool DisableSync { get; set; } = false;
		
		/// <summary>
		///  Should we use key gates to check key valid ranges in blocks?\n
		///  It is highly recommended to do so but takes more space.
		/// </summary>
		public bool EnableKeyGates { get; set; } = true;
		
		/// <summary>
		/// Do we enable the user presentation transaction time hard signature?
		
		/// <i>false</i>: Enables the user presentation transaction time hard signature.
		/// <i>true</i>: Enables the user presentation transaction time hard signature.
		/// Default value set to true.
		/// </summary>
		public bool DisableWebRegUserPresentationTHS { get; set; } = true;
		
		/// <summary>
		/// Gets or set if web registration appointment initiation request time hard signature is enabled.
		/// </summary>
		/// <returns><i>false</i>: Enables the appointment initiation request time hard signature. <br/>
		/// <i>true</i>: Enables the appointment initiation request time hard signature.<br/>
		/// Default: <i>false</i> </returns>
		public bool DisableWebRegAppointmentInitiationTHS { get; set; } = true;
		
		/// <summary>
		///  <para>Gets or sets if the appointment puzzle time hard signature is enabled.</para>
		/// </summary>
		/// 
		/// <returns><i>false</i>: Enables the appointment initiation request time hard signature.<br/>
		/// <i>true</i>: Enables the appointment initiation request time hard signature.
		/// <value>Default value set to true.</value></returns>
		public bool DisableAppointmentPuzzleTHS { get; set; } = true;
		
		/// <summary>
		/// Should we store mining statistics in the wallet
		/// Default: <i>MiningStatisticsModes.Both</i>
		/// </summary>
		public MiningStatisticsModes MiningStatistics { get; set; } = MiningStatisticsModes.Both;
		
		/// <summary>
		///     do we want to disable the wallet block sync?
		/// </summary>
		public bool DisableWalletSync { get; set; } = false;

		public KeySecurityConfigurations KeySecurityConfigurations { get; set; } = new KeySecurityConfigurations();

		public AppSettingsBase.SnapshotIndexTypes AccountSnapshotTrackingMethod { get; set; } = AppSettingsBase.SnapshotIndexTypes.None;

		/// <summary>
		///     Which accounts snapshots do we wish to track?
		/// </summary>
		public List<string> TrackedSnapshotAccountsList { get; set; } = new List<string>();

		/// <summary>
		///     how do we save the events on chain
		/// </summary>
		public AppSettingsBase.BlockSavingModes BlockSavingMode { get; set; } = AppSettingsBase.BlockSavingModes.BlockOnly;

		/// <summary>
		///     should we use a Key dictionary index? takes more disk space, but makes verification much faster
		///     by keeping fast access to the General and Message keys
		/// </summary>
		public bool EnableKeyDictionaryIndex { get; set; } = true;

		public KeyDictionaryTypes EnabledKeyDictionaryTypes { get; set; } = KeyDictionaryTypes.All;

		/// <summary>
		///     How many parallel workflow threads can we have at a maximum in this chain
		/// </summary>
		public int? MaxWorkflowParallelCount { get; set; } = null;

		/// <summary>
		///     If we receive a gossip block message and it is a blockID with this distance from our blockheight, then we cache it
		///     to reuse later.
		/// </summary>
		public int BlockGossipCacheProximityLevel { get; set; } = 1000;

		/// <summary>
		/// 
		/// </summary>
		public bool SkipDigestHashVerification { get; set; } = false;

		/// <summary>
		/// 
		/// </summary>
		public bool SkipGenesisHashVerification { get; set; } = true;

		/// <summary>
		/// 
		/// </summary>
		public bool SkipPeriodicBlockHashVerification { get; set; } = true;

		/// <summary>
		///     Use the rest webapi to register for mining. its simpler and faster, so its preferable to use. it is also required
		///     if the peer can not open it's default port through the firewall
		/// </summary>
		public AppSettingsBase.ContactMethods ElectionsRegistrationMethod { get; set; } = AppSettingsBase.ContactMethods.WebOrGossip;

		/// <summary>
		///     Use the rest webapi to register transactions & messages. its simpler, faster and bypasses p2p transaction limits, so
		///     its preferable to use.
		/// </summary>
		public AppSettingsBase.ContactMethods RegistrationMethod { get; set; } = AppSettingsBase.ContactMethods.WebOrGossip;
		
		/// <summary>
		/// Gets or sets the blockchain sync method. 
		/// </summary>
		/// <value>return the Contact method do use for blockchain syncing. <br>
		///Default value: <see cref="AppSettingsBase.ContactMethods.Gossip"/>. Also <seealso cref="AppSettingsBase.ContactMethods"/>
		/// </value>
		public AppSettingsBase.ContactMethods ChainSyncMethod { get; set; } = AppSettingsBase.ContactMethods.Gossip;
		

		/// <summary>
		///     force a specific mining tier (if possible)
		/// </summary>
		public Enums.MiningTiers? MiningTier { get; set; } = null;

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
		public byte TransactionXmssKeyHeight { get; set; } = 11;

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
		public HashTypes TransactionXmssKeyBackupHashType { get; set; } = HashTypes.Sha2;

		/// <summary>
		/// The seed size for the transaction key
		/// </summary>
		public int? TransactionXmssKeySeedSize { get; set; } = 1500;

		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte MessageXmssKeyHeight { get; set; } = 13;

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
		public HashTypes MessageXmssKeyBackupHashType { get; set; } = HashTypes.Sha3;
		
		/// <summary>
		/// The seed size for the key
		/// </summary>
		public int? MessageXmssKeySeedSize { get; set; } = 1000;
		
		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte ChangeXmssKeyHeight { get; set; } = 9;

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
		public HashTypes ChangeXmssKeyHashType { get; set; } = HashTypes.Sha2;
		
		/// <summary>
		/// 
		/// </summary>
		public HashTypes ChangeXmssKeyBackupHashType { get; set; } = HashTypes.Sha3;
		
		/// <summary>
		/// The seed size for the key
		/// </summary>
		public int? ChangeXmssKeySeedSize { get; set; } = 2000;
		
		/// <summary>
		///     what kind of strength do we want for our xmss^MT super key
		/// </summary>
		public byte SuperXmssKeyHeight { get; set; } = 9;
		
		/// <summary>
		///     Percentage level where we warn of a key Super comming
		/// </summary>
		public float SuperXmssKeyWarningLevel { get; set; } = 0.7F;

		/// <summary>
		///     Percentage level where we must begin the key Super process
		/// </summary>
		public float SuperXmssKeyChangeLevel { get; set; } = 0.9F;

		/// <summary>
		///     the hashing algorithm to use for the keys. Sha3 is currently slower than sha2
		/// </summary>
		public HashTypes SuperXmssKeyHashType { get; set; } = HashTypes.Sha3;
		public HashTypes SuperXmssKeyBackupHashType { get; set; } = HashTypes.Sha2;

		/// <summary>
		/// The seed size for the key
		/// </summary>
		public int? SuperXmssKeySeedSize { get; set; } = 3000;
		
		/// <summary>
		///     what kind of strength do we want for our xmss main key
		/// </summary>
		public byte ValidatorSignatureXmssKeyHeight { get; set; } = 13;

		/// <summary>
		///     Percentage level where we warn of a key change comming
		/// </summary>
		public float ValidatorSignatureXmssKeyWarningLevel { get; set; } = 0.7F;

		/// <summary>
		///     Percentage level where we must begin the key change process
		/// </summary>
		public float ValidatorSignatureXmssKeyChangeLevel { get; set; } = 0.9F;

		/// <summary>
		///     the hashing algorithm to use for the keys. Sha3 is currently slower than sha2
		/// </summary>
		public HashTypes ValidatorSignatureXmssKeyHashType { get; set; } = HashTypes.Sha3;
		
		/// <summary>
		/// 
		/// </summary>
		public HashTypes ValidatorSignatureXmssKeyBackupHashType { get; set; } = HashTypes.Sha2;
		
		/// <summary>
		/// The seed size for the key
		/// </summary>
		public int? ValidatorSignatureXmssKeySeedSize { get; set; } = 2000;
		
		/// <summary>
		///     if true, we will allow gossip presentations even if not allowed otherwise
		/// </summary>
		public bool AllowGossipPresentations { get; set; } = false;
	}

	/// <summary>
	/// This class contains the configurations possible 
	/// </summary>
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
	
	/// <summary>
	/// 
	/// </summary>
	public class UndocumentedDebugConfigurations {
		/// <summary>
		///     should we disable the mining registration process?
		/// </summary>
		public bool DisableMiningRegistration { get; set; } = false;
		
		/// <summary>
		/// 
		/// </summary>
		public bool DebugNetworkMode { get; set; } = false;

		// if true, we expect to operate in locahost only and wont expect a network interface to be present
		public bool LocalhostOnly { get; set; } = false;
		
		/// <summary>
		/// if true, we will allow localhost IPs
		/// </summary>
		public bool AllowLocalhost { get; set; } = false;

		/// <summary>
		///     a debug option to skip if a peer is a hub.. useful to test the hubs, but otherwise not healthy for peers
		/// </summary>
		public bool SkipHubCheck { get; set; } = false;

		public string WhiteListNodesRegex { get; set; } = "";

	}

	/// <summary>
	///  This class represents the proxy server settings.
	/// </summary>
	public class ProxySettings {
		/// <summary>
		/// The host name of the proxy.
		/// </summary>
		public string Host { get; set; }
		/// <summary>
		///  Gets or sets the connection port for the proxy.
		/// </summary>
		public int Port { get; set; }
		/// <summary>
		/// Gets or sets the username to connect to the proxy.
		/// </summary>
		public string User { get; set; }
		/// <summary>
		/// Gets or sets the password to connect to the proxy.
		/// </summary>
		public string Password { get; set; }
	}
}