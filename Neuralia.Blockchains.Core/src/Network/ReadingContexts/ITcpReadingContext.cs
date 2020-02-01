using System;

namespace Neuralia.Blockchains.Core.Network.ReadingContexts {
	public interface ITcpReadingContext {
		bool IsCanceled { get; }
		bool IsCompleted { get; }
		bool IsEmpty { get; }
		long Length { get; }

		byte this[int i] { get; }
		void DataRead(int amount);

		void CopyTo(in Span<byte> dest, int srcOffset, int destOffset, int length);
	}
}