namespace Neuralia.Blockchains.Core.Workflows {
	public static class WorkflowIDs {
		public const short NO_WORKFLOW = short.MinValue;
		public const short HANDSHAKE = 1;
		public const short VALIDATE_MINER_IP_HANDSHAKE = 2;
		public const short PEER_LIST_REQUEST = 3;

		public const short MESSAGE_GROUP_MANIFEST = 4;

		public const short CHAIN_SYNC = 5;

		public const short DEBUG = 999;
	}
}