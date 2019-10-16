using System;
using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {
	public class BrotliCompression : Compression<BrotliCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {

#if (NETSTANDARD2_0)
throw new NotImplementedException();
#else
				using(BrotliStream compressor = new BrotliStream(output, this.ConvertCompression(level), true)) {
					compressor.Write(data.Bytes, data.Offset, data.Length);
				}
#endif
				return ByteArray.Create(output);
			}
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Default);
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

#if (NETSTANDARD2_0)
throw new NotImplementedException();
#else
			using(BrotliStream decompressor = new BrotliStream(input, CompressionMode.Decompress, true)) {

				decompressor.CopyTo(output);
			}
#endif
		}
	}
}