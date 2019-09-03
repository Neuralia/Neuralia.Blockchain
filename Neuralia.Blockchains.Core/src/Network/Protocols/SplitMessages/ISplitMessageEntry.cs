using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols.SplitMessages {
	public interface ISplitMessageEntry : IMessageEntry, IDisposable2 {

		long Hash { get; }
		int CompleteMessageLength { get; }

		IByteArray CreateNextSliceRequestMessage();
		IByteArray CreateSliceResponseMessage(ISliceRequestMessageEntry requestSliceMessageEntry);
		void SetSliceData(ISliceResponseMessageEntry responseSliceMessageEntry);
		IByteArray AssembleCompleteMessage();
	}

	public interface ISplitMessageEntry<HEADER_TYPE> : ISplitMessageEntry, IMessageEntry<HEADER_TYPE>
		where HEADER_TYPE : IMessageHeader {
	}

}