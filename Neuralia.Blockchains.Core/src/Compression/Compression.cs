using System.IO;
using System.IO.Compression;
using System.Text;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Compression {

	public interface ICompression {
		IByteArray Compress(string text, CompressionLevelByte level);
		IByteArray Compress(string text);
		IByteArray Compress(IByteArray data, CompressionLevelByte level);
		IByteArray Compress(IByteArray data);
		IByteArray Decompress(IByteArray data);
		IByteArray Decompress(Stream input);
		void Decompress(Stream input, Stream output);
	}

	public abstract class Compression<T> : ICompression
		where T : ICompression, new() {

		private readonly object locker = new object();

		static Compression() {
		}

		public static T Instance { get; } = new T();

		public IByteArray Compress(string text, CompressionLevelByte level) {
			IByteArray data = (ByteArray) Encoding.UTF8.GetBytes(text);
			IByteArray result = null;

			lock(this.locker) {
				result = this.CompressData(data, level);
			}

			data.Return();

			return result;
		}

		public IByteArray Compress(string text) {
			IByteArray data = (ByteArray) Encoding.UTF8.GetBytes(text);
			IByteArray result = null;

			lock(this.locker) {
				result = this.CompressData(data);
			}

			data.Return();

			return result;
		}

		public IByteArray Compress(IByteArray data, CompressionLevelByte level) {
			lock(this.locker) {
				return this.CompressData(data, level);
			}
		}

		public IByteArray Compress(IByteArray data) {
			lock(this.locker) {
				return this.CompressData(data);
			}
		}

		public IByteArray Decompress(IByteArray data) {
			lock(this.locker) {
				return this.DecompressData(data);
			}
		}

		public IByteArray Decompress(Stream input) {
			lock(this.locker) {
				return this.DecompressData(input);
			}
		}

		public void Decompress(Stream input, Stream output) {
			lock(this.locker) {
				this.DecompressData(input, output);
			}
		}

		public string DecompressText(IByteArray data) {
			IByteArray bytes = this.Decompress(data);
			string result = Encoding.UTF8.GetString(bytes.ToExactByteArray());
			bytes.Return();

			return result;
		}

		public string DecompressText(Stream input) {
			IByteArray bytes = this.Decompress(input);
			string result = Encoding.UTF8.GetString(bytes.ToExactByteArray());
			bytes.Return();

			return result;
		}

		protected abstract IByteArray CompressData(IByteArray data, CompressionLevelByte level);
		protected abstract IByteArray CompressData(IByteArray data);

		protected abstract IByteArray DecompressData(IByteArray data);
		protected abstract IByteArray DecompressData(Stream input);
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