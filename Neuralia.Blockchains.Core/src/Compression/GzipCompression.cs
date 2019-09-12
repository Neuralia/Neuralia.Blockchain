using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Compression {
	public class GzipCompression : Compression<GzipCompression> {

		protected override IByteArray CompressData(IByteArray data, CompressionLevelByte level) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryAllocators.Instance.recyclableMemoryStreamManager.GetStream("compress")) {

				using(GZipStream compressor = new GZipStream(output, this.ConvertCompression(level), true)) {
					compressor.Write(data.Bytes, data.Offset, data.Length);

					compressor.Flush();

					return ByteArray.CreateFrom(output);
				}
			}
		}

		protected override IByteArray CompressData(IByteArray data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override IByteArray DecompressData(IByteArray data) {

			using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryAllocators.Instance.recyclableMemoryStreamManager.GetStream("decompress", data.Bytes, data.Offset, data.Length)) {

				return this.DecompressData(input);
			}
		}

		protected override IByteArray DecompressData(Stream input) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryAllocators.Instance.recyclableMemoryStreamManager.GetStream("output")) {

				this.DecompressData(input, output);

				return ByteArray.CreateFrom(output);
			}
		}

		protected override void DecompressData(Stream input, Stream output) {

			using(GZipStream decompressor = new GZipStream(input, CompressionMode.Decompress)) {

				decompressor.CopyTo(output);

				decompressor.Flush();
			}
		}
	}
}