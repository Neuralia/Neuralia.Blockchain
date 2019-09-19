using System.IO;
using System.IO.Compression;
using System.Text;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {

	public interface ICompression {
		SafeArrayHandle Compress(string text, CompressionLevelByte level);
		SafeArrayHandle Compress(string text);
		SafeArrayHandle Compress(SafeArrayHandle data, CompressionLevelByte level);
		SafeArrayHandle Compress(SafeArrayHandle data);
		SafeArrayHandle Decompress(SafeArrayHandle data);
		SafeArrayHandle Decompress(Stream input);
		void Decompress(Stream input, Stream output);
	}

	public abstract class Compression<T> : ICompression
		where T : ICompression, new() {

		private readonly object locker = new object();

		static Compression() {
		}

		public static T Instance { get; } = new T();

		public SafeArrayHandle Compress(string text, CompressionLevelByte level) {
			SafeArrayHandle data = (ByteArray) Encoding.UTF8.GetBytes(text);
			SafeArrayHandle result = null;

			lock(this.locker) {
				result = this.CompressData(data, level);
			}

			data.Return();

			return result;
		}

		public SafeArrayHandle Compress(string text) {
			SafeArrayHandle data = (ByteArray) Encoding.UTF8.GetBytes(text);
			SafeArrayHandle result = null;

			lock(this.locker) {
				result = this.CompressData(data);
			}

			data.Return();

			return result;
		}

		public SafeArrayHandle Compress(SafeArrayHandle data, CompressionLevelByte level) {
			lock(this.locker) {
				return this.CompressData(data, level);
			}
		}

		public SafeArrayHandle Compress(SafeArrayHandle data) {
			lock(this.locker) {
				return this.CompressData(data);
			}
		}

		public SafeArrayHandle Decompress(SafeArrayHandle data) {
			lock(this.locker) {
				return this.DecompressData(data);
			}
		}

		public SafeArrayHandle Decompress(Stream input) {
			lock(this.locker) {
				return this.DecompressData(input);
			}
		}

		public void Decompress(Stream input, Stream output) {
			lock(this.locker) {
				this.DecompressData(input, output);
			}
		}

		public string DecompressText(SafeArrayHandle data) {
			SafeArrayHandle bytes = this.Decompress(data);
			string result = Encoding.UTF8.GetString(bytes.ToExactByteArray());
			bytes.Return();

			return result;
		}

		public string DecompressText(Stream input) {
			SafeArrayHandle bytes = this.Decompress(input);
			string result = Encoding.UTF8.GetString(bytes.ToExactByteArray());
			bytes.Return();

			return result;
		}

		protected abstract SafeArrayHandle CompressData(SafeArrayHandle data, CompressionLevelByte level);
		protected abstract SafeArrayHandle CompressData(SafeArrayHandle data);

		protected abstract SafeArrayHandle DecompressData(SafeArrayHandle data);
		protected abstract SafeArrayHandle DecompressData(Stream input);
		protected abstract void DecompressData(Stream input, Stream output);

		protected CompressionLevelByte ConvertCompression(CompressionLevel level) {
			switch(level) {
				case CompressionLevel.Optimal:

					return CompressionLevelByte.Optimal;

				case CompressionLevel.Fastest:

					return CompressionLevelByte.Fastest;

				default:

					return CompressionLevelByte.NoCompression;
			}
		}

		protected CompressionLevel ConvertCompression(CompressionLevelByte level) {
			switch(level) {
				case CompressionLevelByte.Maximum:

					return (CompressionLevel) 11;

				case CompressionLevelByte.Nine:

					return (CompressionLevel) 9;

				case CompressionLevelByte.Optimal:

					return CompressionLevel.Optimal;

				case CompressionLevelByte.Fastest:

					return CompressionLevel.Fastest;

				default:

					return CompressionLevel.NoCompression;
			}
		}
	}
}