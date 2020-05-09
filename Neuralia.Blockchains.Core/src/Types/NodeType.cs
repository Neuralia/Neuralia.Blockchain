namespace Neuralia.Blockchains.Core.Types {

	public class NodeType {

		private const uint PEER_TYPE_MASK = 0x7;

		private const uint BLOCK_SAVING_MASK = 0x7;
		private const int BLOCK_SAVING_OFFSET = 3;

		public static readonly NodeType Unknown = new NodeType(Enums.PeerTypes.Unknown, BlockchainTypes.Instance.None, Enums.ChainSharingTypes.None);
		public static readonly NodeType Full = new NodeType(Enums.PeerTypes.FullNode, BlockchainTypes.Instance.All, Enums.ChainSharingTypes.Full);
		public static readonly NodeType Hub = new NodeType(Enums.PeerTypes.Hub, BlockchainTypes.Instance.All, Enums.ChainSharingTypes.None);

		public NodeType(byte data) {
			this.Data = data;
		}

		public NodeType(NodeType other) {
			this.Data = other.Data;
			this.BlockchainType = other.BlockchainType;
		}

		public NodeType(Enums.PeerTypes peerType, BlockchainType blockchainType, Enums.ChainSharingTypes blockSavingMode) : this(peerType, blockchainType, new NodeShareType(blockSavingMode)) {

		}

		public NodeType(Enums.PeerTypes peerType, BlockchainType blockchainType, NodeShareType nodeShareType) {

			this.PeerType = peerType;
			this.BlockchainType = blockchainType;
			this.ShareType = nodeShareType;
		}

		public byte Data { get; private set; }
		public BlockchainType BlockchainType { get; }

		public Enums.PeerTypes PeerType {
			get => (Enums.PeerTypes) (this.Data & PEER_TYPE_MASK);
			set => this.Data = (byte) ((this.Data & ~PEER_TYPE_MASK) | (byte) value);
		}

		public NodeShareType ShareType {
			get => new NodeShareType((Enums.ChainSharingTypes) ((this.Data >> BLOCK_SAVING_OFFSET) & BLOCK_SAVING_MASK));
			set => this.Data = (byte) ((this.Data & ~(BLOCK_SAVING_MASK << BLOCK_SAVING_OFFSET)) | ((byte) value.SharingType << BLOCK_SAVING_OFFSET));
		}

		public static implicit operator NodeType((Enums.PeerTypes peerType, BlockchainType blockchainType, NodeShareType nodeShareType) entry) {
			return new NodeType(entry.peerType, entry.blockchainType, entry.nodeShareType);
		}

		public static implicit operator NodeShareType(NodeType entry) {
			return entry.ShareType;
		}

		public static implicit operator BlockchainType(NodeType entry) {
			return entry.BlockchainType;
		}

		public static implicit operator Enums.PeerTypes(NodeType entry) {
			return entry.PeerType;
		}

		public override string ToString() {
			return $"({this.PeerType}, {this.BlockchainType}, {this.ShareType})";
		}

		public bool Equals(NodeType other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return (this.Data == other.Data) && (this.BlockchainType == other.BlockchainType);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((NodeType) obj);
		}

		public static bool operator ==(NodeType a, NodeType b) {
			return a.Equals(b);
		}

		public static bool operator !=(NodeType a, NodeType b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			int hc = this.Data.GetHashCode();
			hc *= 37;
			hc ^= this.BlockchainType.GetHashCode();

			return hc;
		}
	}
}