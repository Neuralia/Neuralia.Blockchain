using System;
using System.IO;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {
	/// <summary>
	///     A do nothing compressor. straight passthrough
	/// </summary>
	public class NullCompression : Compression<NullCompression> {

		protected override IByteArray CompressData(IByteArray data, CompressionLevelByte level) {
			return ByteArray.CreateFrom(data);
		}

		protected override IByteArray CompressData(IByteArray data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override IByteArray DecompressData(IByteArray data) {

			return ByteArray.CreateFrom(data);
		}

		protected override IByteArray DecompressData(Stream stream) {
			throw new NotImplementedException();
		}

		protected override void DecompressData(Stream intput, Stream output) {

		}
	}
}