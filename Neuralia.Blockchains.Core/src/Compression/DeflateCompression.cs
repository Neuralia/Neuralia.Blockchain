using System;
using System.IO;
using System.IO.Compression;
using Microsoft.IO;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Compression {
	public class DeflateCompression : Compression<DeflateCompression> {

		protected override SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level, Action<Stream> preProcessOutput = null) {
			using(RecyclableMemoryStream output = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress")) {
				using(RecyclableMemoryStream input = (RecyclableMemoryStream) MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("compress", data.Bytes, data.Offset, data.Length)) {
					this.CompressData(input, output, level);
					return ByteArray.Create(output);
				}
			}
		}

		protected override SafeArrayHandle CompressData(SafeArrayHandle data) {
			return this.CompressData(data, CompressionLevelByte.Fastest);
		}

		protected override void CompressData(Stream input, Stream output, CompressionLevelByte level) {
			using(DeflateStream dstream = new DeflateStream(output, this.ConvertCompression(level), true)) {
				input.CopyTo(dstream);
			}
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
			using(DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress, true)) {
				dstream.CopyTo(output);
			}
		}
	}
}