using System;
using System.IO;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays; //using Ionic.Zlib;

namespace Neuralia.Blockchains.Core.Compression {
	public class GzipCompression : Compression<GzipCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level, Action<Stream> preProcessOutput = null) {

			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {

				using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress", data.Bytes, data.Offset, data.Length)) {
					this.CompressData(input, output, level);

					return ByteArray.Create(output);
				}
			}
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Default);
		}

		protected override void CompressData(Stream input, Stream output, CompressionLevelByte level) {
			// using(GZipStream compressor = new GZipStream(output, CompressionMode.Compress, this.ConvertCompression2(level), true)) {
			// 	input.CopyTo(compressor);
			// }
			throw new NotImplementedException();

		}

		protected override SafeArrayHandle DecompressData(SafeArrayHandle data, Action<Stream> preProcessInput = null) {

			using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("decompress", data.Bytes, data.Offset, data.Length)) {

				return this.DecompressData(input);
			}
		}

		protected override SafeArrayHandle DecompressData(Stream input, Action<Stream> preProcessInput = null) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("output")) {

				this.DecompressData(input, output);

				return ByteArray.Create(output);
			}
		}

		protected override void DecompressData(Stream input, Stream output) {

			// using(GZipStream decompressor = new GZipStream(input, CompressionMode.Decompress, true)) {
			//
			// 	decompressor.CopyTo(output);
			// }
			throw new NotImplementedException();
		}
	}
}