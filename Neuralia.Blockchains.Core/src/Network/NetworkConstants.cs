namespace Neuralia.Blockchains.Core.Network {
	public static class NetworkConstants {
		public const int MAIN_NETWORK_ID = 0x13A21052;
		public const int TEST_NETWORK_ID = 0x12B73334;
		public const int DEV_NETWORK_ID = 0x11A61251;


		public static int CURRENT_NETWORK_ID {
			get {
#if TESTNET
				return TEST_NETWORK_ID;
#elif DEVNET
			return DEV_NETWORK_ID;
#else
			return MAIN_NETWORK_ID;
#endif
			}
		}
	}
}