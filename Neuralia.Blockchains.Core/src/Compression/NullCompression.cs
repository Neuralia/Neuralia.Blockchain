using System;
using System.IO;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {
	/// <summary>
	///     A do nothing compressor. straight passthrough
	/// </summary>
	public class NullCompression : Compression<NullCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level) {
			return data.Branch();
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override SafeArrayHandle DecompressData(SafeArrayHandle data) {

			return data.Branch();
		}

		protected override SafeArrayHandle DecompressData(Stream stream) {
			throw new NotImplementedException();
		}

		protected override void DecompressData(Stream intput, Stream output) {

		}
	}
}