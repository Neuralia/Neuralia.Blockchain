using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Contents {

	public class TransactionContentType : SimpleUShort<TransactionContentType> {

		public TransactionContentType() {
		}

		public TransactionContentType(ushort value) : base(value) {
		}

		public static implicit operator TransactionContentType(ushort d) {
			return new TransactionContentType(d);
		}
	}

	public sealed class TransactionContentTypes : UShortConstantSet<TransactionContentType> {

		public readonly TransactionContentType GENESIS;

		static TransactionContentTypes() {
		}

		private TransactionContentTypes() : base(1000) {
			this.GENESIS = this.CreateBaseConstant();

		}

		public static TransactionContentTypes Instance { get; } = new TransactionContentTypes();
	}
}