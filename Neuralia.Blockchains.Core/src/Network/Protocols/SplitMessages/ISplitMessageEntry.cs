using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.SplitMessages {
	public interface ISplitMessageEntry : IMessageEntry {

		long Hash { get; }
		int CompleteMessageLength { get; }

		SafeArrayHandle CreateNextSliceRequestMessage();
		SafeArrayHandle CreateSliceResponseMessage(ISliceRequestMessageEntry requestSliceMessageEntry);
		void SetSliceData(ISliceResponseMessageEntry responseSliceMessageEntry);
		SafeArrayHandle AssembleCompleteMessage();
	}

	public interface ISplitMessageEntry<HEADER_TYPE> : ISplitMessageEntry, IMessageEntry<HEADER_TYPE>
		where HEADER_TYPE : IMessageHeader {
	}

}