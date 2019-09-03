using System.IO;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class StreamExtensions {
		/// <summary>
		///     this method will wipe a stream with 0s.
		/// </summary>
		/// <param name="stream"></param>
		public static void ClearStream(this Stream stream) {

			long length = stream.Position;
			stream.Position = 0;

			for(int i = 0; i < length; i++) {
				stream.WriteByte(0);
			}
		}
	}
}