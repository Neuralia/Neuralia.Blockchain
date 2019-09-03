using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
#if (NETSTANDARD2_0)
using BrotliSharpLib;

#endif

namespace Neuralia.Blockchains.Core.Compression {
	public class BrotliCompression : Compression<BrotliCompression> {

		protected override IByteArray CompressData(IByteArray data, CompressionLevelByte level) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryAllocators.Instance.recyclableMemoryStreamManager.GetStream("compress")) {
#if (NETSTANDARD2_0)
				using(BrotliStream compressor = new BrotliStream(output, CompressionMode.Compress, true)) {
					compressor.SetQuality((int) this.ConvertCompression(level));
#elif (NETCOREAPP2_2)
				using(BrotliStream compressor = new BrotliStream(output, this.ConvertCompression(level), true)) {
#else
	throw new NotImplementedException();
#endif

					compressor.Write(data.Bytes, data.Offset, data.Length);

					compressor.Flush();

					return ByteArray.CreateFrom(output);
				}
			}
		}

		protected override IByteArray CompressData(IByteArray data) {
			return this.CompressData(data, CompressionLevelByte.Nine);
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
#if (NETSTANDARD2_0)
			using(BrotliStream decompressor = new BrotliStream(input, CompressionMode.Decompress)) {

#elif (NETCOREAPP2_2)
			using(BrotliStream decompressor = new BrotliStream(input, CompressionMode.Decompress)) {
#else
				throw new NotImplementedException();
#endif

				decompressor.CopyTo(output);

				decompressor.Flush();
			}
		}
	}
}