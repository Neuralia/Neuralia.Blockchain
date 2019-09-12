using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Compression {
	public class DeflateCompression : Compression<DeflateCompression> {

		protected override IByteArray CompressData(IByteArray data, CompressionLevelByte level) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryAllocators.Instance.recyclableMemoryStreamManager.GetStream("compress")) {
				using(DeflateStream dstream = new DeflateStream(output, this.ConvertCompression(level))) {
					dstream.Write(data.Bytes, data.Offset, data.Length);

					dstream.Flush();

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
			using(DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress)) {
				dstream.CopyTo(output);

				dstream.Flush();

			}
		}
	}
}