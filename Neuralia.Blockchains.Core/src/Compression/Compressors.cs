namespace Neuralia.Blockchains.Core.Compression {
	public static class Compressors {

		public static ICompression WalletCompressor => GzipCompression.Instance; //NullCompression.Instance ;
		public static ICompression DigestCompressor => GzipCompression.Instance;
		public static ICompression GeneralPurposeCompressor => GzipCompression.Instance;
	}
}