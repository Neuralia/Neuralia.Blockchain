using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {

	public class TransactionSelectionMethodType : SimpleUShort<TransactionSelectionMethodType> {

		public TransactionSelectionMethodType() {
		}

		public TransactionSelectionMethodType(byte value) : base(value) {
		}

		public static implicit operator TransactionSelectionMethodType(byte d) {
			return new TransactionSelectionMethodType(d);
		}

		public static bool operator ==(TransactionSelectionMethodType a, TransactionSelectionMethodType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(TransactionSelectionMethodType a, TransactionSelectionMethodType b) {
			return !(a == b);
		}
	}

	public class TransactionSelectionMethodTypes : UShortConstantSet<TransactionSelectionMethodType> {
		public readonly TransactionSelectionMethodType Automatic;

		public readonly TransactionSelectionMethodType CreationTime;
		public readonly TransactionSelectionMethodType Random;
		public readonly TransactionSelectionMethodType Size;
		public readonly TransactionSelectionMethodType TransationTypes;

		static TransactionSelectionMethodTypes() {
		}

		public TransactionSelectionMethodTypes() : base(100) {
			this.CreationTime = this.CreateBaseConstant();
			this.TransationTypes = this.CreateBaseConstant();
			this.Size = this.CreateBaseConstant();
			this.Random = this.CreateBaseConstant();
			this.Automatic = this.CreateBaseConstant();
		}

		public static TransactionSelectionMethodTypes Instance { get; } = new TransactionSelectionMethodTypes();
	}

}