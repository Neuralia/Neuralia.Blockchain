namespace Neuralia.Blockchains.Core.Network.Protocols.SplitMessages {
	public interface ISliceResponseMessageEntry {
		long LargeMessageHash { get; }

		int Index { get; }
		long SliceHash { get; }
	}
}