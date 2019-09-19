using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {
	public class GzipCompression : Compression<GzipCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {

				using(GZipStream compressor = new GZipStream(output, this.ConvertCompression(level), true)) {
					compressor.Write(data.Bytes, data.Offset, data.Length);
				}
				return ByteArray.Create(output);
			}
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override SafeArrayHandle DecompressData(SafeArrayHandle data) {

			using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("decompress", data.Bytes, data.Offset, data.Length)) {

				return this.DecompressData(input);
			}
		}

		protected override SafeArrayHandle DecompressData(Stream input) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("output")) {

				this.DecompressData(input, output);

				return ByteArray.Create(output);
			}
		}

		protected override void DecompressData(Stream input, Stream output) {

			using(GZipStream decompressor = new GZipStream(input, CompressionMode.Decompress, true)) {

				decompressor.CopyTo(output);
			}
		}
	}
}