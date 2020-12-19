using System;
using System.IO;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {
	/// <summary>
	///     A do nothing compressor. straight passthrough
	/// </summary>
	public class NullCompression : Compression<NullCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level, Action<Stream> preProcessOutput = null) {
			return data.Clone();
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override void CompressData(Stream input, Stream output, CompressionLevelByte level) {
		}

		protected override SafeArrayHandle DecompressData(SafeArrayHandle data, Action<Stream> preProcessInput = null) {

			return data.Clone();
		}

		protected override SafeArrayHandle DecompressData(Stream stream, Action<Stream> preProcessInput = null) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {
				stream.CopyTo(output);

				return SafeArrayHandle.Create(output);
			}
		}

		protected override void DecompressData(Stream intput, Stream output) {

		}
	}
}