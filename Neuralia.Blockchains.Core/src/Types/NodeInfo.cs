using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Types {

	/// <summary>
	///     a stru cture to hold and report various information about the node
	/// </summary>
	public class NodeInfo : ISerializableCombo {

		private const uint GOSSIP_TYPE_MASK = 0x3;

		private const uint PEER_TYPE_MASK = 0x7;
		private const int PEER_TYPE_OFFSET = 2;

		public static readonly NodeInfo Full = new NodeInfo(Enums.GossipSupportTypes.Full, Enums.PeerTypes.FullNode);
		public static readonly NodeInfo Hub = new NodeInfo(Enums.GossipSupportTypes.None, Enums.PeerTypes.Hub);
		public static readonly NodeInfo Unknown = new NodeInfo(Enums.GossipSupportTypes.None, Enums.PeerTypes.Unknown);

		public NodeInfo() {

		}

		public NodeInfo(NodeInfo other) {
			this.Data = other.Data;
		}

		public NodeInfo(Enums.GossipSupportTypes gossipSupportType, Enums.PeerTypes peerType, Dictionary<BlockchainType, ChainSettings> chainSettings = null) {
			this.GossipSupportType = gossipSupportType;
			this.PeerType = peerType;

			if(chainSettings != null) {
				this.ChainSettings = chainSettings;
			}
		}

		public uint Data { get; private set; }

		/// <summary>
		///     Here we store the calculated consensus between all peers and the data they sent us. hopefully they all agree!
		/// </summary>
		private Dictionary<BlockchainType, ChainSettings> ChainSettings { get; set; } = new Dictionary<BlockchainType, ChainSettings>();

		public Enums.GossipSupportTypes GossipSupportType {
			get => (Enums.GossipSupportTypes) (this.Data & GOSSIP_TYPE_MASK);
			private set => this.Data = (this.Data & ~GOSSIP_TYPE_MASK) | (byte) value;
		}

		public Enums.PeerTypes PeerType {
			get => (Enums.PeerTypes) ((this.Data >> PEER_TYPE_OFFSET) & PEER_TYPE_MASK);
			private set => this.Data = (uint) ((this.Data & ~(PEER_TYPE_MASK << PEER_TYPE_OFFSET)) | ((byte) value << PEER_TYPE_OFFSET));
		}

		public bool GossipAccepted => this.GossipSupportType != Enums.GossipSupportTypes.None;

		public bool IsUnknown => this.PeerType == Enums.PeerTypes.Unknown;
		public bool IsKnown => !this.IsUnknown;

		public void Rehydrate(IDataRehydrator rehydrator) {
			AdaptiveInteger1_4 entry = new AdaptiveInteger1_4();
			entry.Rehydrate(rehydrator);

			this.Data = entry.Value;

			this.ChainSettings.Clear();
			int chainSettingCount = rehydrator.ReadInt();

			for(int j = 0; j < chainSettingCount; j++) {
				BlockchainType chainId = rehydrator.ReadUShort();

				ChainSettings chainSetting = new ChainSettings();
				chainSetting.Rehydrate(rehydrator);

				this.ChainSettings.Add(chainId, chainSetting);
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			AdaptiveInteger1_4 entry = new AdaptiveInteger1_4();
			entry.Value = this.Data;

			entry.Dehydrate(dehydrator);

			// now the chain optionsBase
			dehydrator.Write(this.ChainSettings.Count);

			foreach(KeyValuePair<BlockchainType, ChainSettings> chainSetting in this.ChainSettings) {
				dehydrator.Write(chainSetting.Key.Value);

				chainSetting.Value.Dehydrate(dehydrator);
			}
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Data);

			hashNodeList.Add(this.ChainSettings.Count);

			foreach(KeyValuePair<BlockchainType, ChainSettings> chainSetting in this.ChainSettings) {
				hashNodeList.Add(chainSetting.Key.Value);
				hashNodeList.Add(chainSetting.Value);
			}

			return hashNodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty(nameof(this.GossipSupportType), this.GossipSupportType.ToString());
			jsonDeserializer.SetProperty(nameof(this.PeerType), this.PeerType.ToString());
		}

		public static bool DoesPeerTypeSupport(NodeInfo node, Enums.GossipSupportTypes minimumRequiredType) {
			return (byte) node.GossipSupportType >= (byte) minimumRequiredType;
		}

		public void SetChainSettings(Dictionary<BlockchainType, ChainSettings> chainSettings) {

			this.ChainSettings = chainSettings;
		}

		public void AddChainSettings(BlockchainType blockchainType, ChainSettings chainSettings) {

			if(this.ChainSettings.ContainsKey(blockchainType)) {
				this.ChainSettings.Remove(blockchainType);
			}

			this.ChainSettings.Add(blockchainType, chainSettings);
		}

		/// <summary>
		///     Get the node type info for the selected blockchain
		/// </summary>
		/// <param name="blockchainType"></param>
		/// <returns></returns>
		public NodeType GetNodeShareType(BlockchainType blockchainType) {

			if(!this.ChainSettings.ContainsKey(blockchainType)) {
				return null;
			}

			return new NodeType(this.PeerType, blockchainType, this.ChainSettings[blockchainType].ShareType);
		}

		public Dictionary<BlockchainType, ChainSettings> GetChainSettings() {

			return this.ChainSettings.ToDictionary();
		}

		public Dictionary<BlockchainType, NodeType> GetNodeShareTypes() {

			return this.ChainSettings.ToDictionary(s => s.Key, s => new NodeType(this.PeerType, s.Key, s.Value.ShareType));
		}

		public List<BlockchainType> GetSupportedBlockchains() {
			return this.ChainSettings.Select(s => s.Key).ToList();

		}

		public override string ToString() {
			return $"({this.GossipSupportType}, {this.PeerType})";
		}

		public bool Equals(NodeInfo other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.PeerType == other.PeerType;
		}

		public bool Equals(Enums.PeerTypes other) {

			return this.PeerType == other;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((NodeInfo) obj);
		}

		public static bool operator ==(NodeInfo a, NodeInfo b) {
			return a.Equals(b);
		}

		public static bool operator !=(NodeInfo a, NodeInfo b) {
			return !(a == b);
		}

		public static bool operator ==(NodeInfo a, Enums.PeerTypes b) {

			if(ReferenceEquals(null, a)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(NodeInfo a, Enums.PeerTypes b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			return this.PeerType.GetHashCode();
		}
	}
}