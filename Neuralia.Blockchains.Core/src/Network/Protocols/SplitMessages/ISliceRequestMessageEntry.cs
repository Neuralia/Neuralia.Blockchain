namespace Neuralia.Blockchains.Core.Network.Protocols.SplitMessages {
	public interface ISliceRequestMessageEntry {
		long LargeMessageHash { get; }

		int Index { get; }
		long SliceHash { get; }
	}
}