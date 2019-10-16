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

					return CompressionLevelByte.Level9;

				case CompressionLevel.Fastest:

					return CompressionLevelByte.Fastest;

				default:

					return CompressionLevelByte.Default;
			}
		}

		protected CompressionLevel ConvertCompression(CompressionLevelByte level) {
			return (CompressionLevel) (int)level;
		}
		
		protected Ionic.Zlib.CompressionLevel ConvertCompression2(CompressionLevelByte level) {
			
			switch(level) {
				case CompressionLevelByte.None:
					return Ionic.Zlib.CompressionLevel.Level0;
				case CompressionLevelByte.Level1:
					return Ionic.Zlib.CompressionLevel.Level1;
				case CompressionLevelByte.Level2:
					return Ionic.Zlib.CompressionLevel.Level2;
				case CompressionLevelByte.Level3:
					return Ionic.Zlib.CompressionLevel.Level3;
				case CompressionLevelByte.Level4:
					return Ionic.Zlib.CompressionLevel.Level4;
				case CompressionLevelByte.Level5:
					return Ionic.Zlib.CompressionLevel.Level5;
				case CompressionLevelByte.Level6:
					return Ionic.Zlib.CompressionLevel.Level6;
				case CompressionLevelByte.Level7:
					return Ionic.Zlib.CompressionLevel.Level7;
				case CompressionLevelByte.Level8:
					return Ionic.Zlib.CompressionLevel.Level8;
				case CompressionLevelByte.Level9:
					return Ionic.Zlib.CompressionLevel.Level9;
				case CompressionLevelByte.Level10:
					return Ionic.Zlib.CompressionLevel.Level9;
				case CompressionLevelByte.Level11:
					return Ionic.Zlib.CompressionLevel.Level9;
			}
			
			
			return ( Ionic.Zlib.CompressionLevel) (int)level;
		}
	}
}