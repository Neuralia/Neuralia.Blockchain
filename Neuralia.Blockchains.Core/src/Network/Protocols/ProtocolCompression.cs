using Neuralia.Blockchains.Core.Compression;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public struct ProtocolCompression {

		public enum CompressionAlgorithm : byte {
			None = 0,
			Deflate = 1,
			Gzip = 2
		}

		public CompressionAlgorithm Type { get; }
		public CompressionLevelByte Level { get; }

		public ProtocolCompression(CompressionAlgorithm type, CompressionLevelByte level) {
			this.Type = type;
			this.Level = level;
		}
	}
}