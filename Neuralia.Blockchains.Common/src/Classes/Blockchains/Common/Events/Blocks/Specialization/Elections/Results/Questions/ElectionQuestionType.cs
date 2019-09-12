using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions {
	public class ElectionQuestionType : SimpleUShort<ElectionQuestionType> {

		public ElectionQuestionType() {
		}

		public ElectionQuestionType(byte value) : base(value) {
		}

		public static implicit operator ElectionQuestionType(byte d) {
			return new ElectionQuestionType(d);
		}

		public static bool operator ==(ElectionQuestionType a, ElectionQuestionType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(ElectionQuestionType a, ElectionQuestionType b) {
			return !(a == b);
		}
	}

	public sealed class ElectionQuestionTypes : UShortConstantSet<ElectionQuestionType> {

		public readonly ElectionQuestionType BlockTransactionIndex;

		static ElectionQuestionTypes() {
		}

		private ElectionQuestionTypes() : base(100) {
			this.BlockTransactionIndex = this.CreateBaseConstant();
		}

		public static ElectionQuestionTypes Instance { get; } = new ElectionQuestionTypes();
	}

}