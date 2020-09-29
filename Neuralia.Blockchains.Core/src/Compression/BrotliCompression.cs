using System;
using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Compression {
	public class BrotliCompression : Compression<BrotliCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level, Action<Stream> preProcessOutput = null) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {

				using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress", data.Bytes, data.Offset, data.Length)) {

					if(preProcessOutput != null) {
						preProcessOutput(output);
					}

					this.CompressData(input, output, level);

					return SafeArrayHandle.Create(output);
				}
			}
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Default);
		}

		protected override void CompressData(Stream input, Stream output, CompressionLevelByte level) {

			using(BrotliStream compressor = new BrotliStream(output, this.ConvertCompression(level), true)) {
				input.CopyTo(compressor);
			}

		}

		protected override SafeArrayHandle DecompressData(SafeArrayHandle data, Action<Stream> preProcessInput = null) {

			using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("decompress", data.Bytes, data.Offset, data.Length)) {

				return this.DecompressData(input, preProcessInput);
			}
		}

		protected override SafeArrayHandle DecompressData(Stream input, Action<Stream> preProcessInput = null) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("output")) {
				if(preProcessInput != null) {
					preProcessInput(input);
				}

				this.DecompressData(input, output);

				return SafeArrayHandle.Create(output);
			}
		}

		protected override void DecompressData(Stream input, Stream output) {

			using(BrotliStream decompressor = new BrotliStream(input, CompressionMode.Decompress, true)) {

				decompressor.CopyTo(output);
			}
		}
	}
}