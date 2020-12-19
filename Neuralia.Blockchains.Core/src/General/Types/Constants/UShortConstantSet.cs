using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core.General.Types.Constants {

	public abstract class UShortConstantSet<T> : ConstantSet<T, ushort>
		where T : ISimpleNumeric<T, ushort>, new() {

		protected UShortConstantSet(T baseOffset) : base(baseOffset) {
		}

		protected UShortConstantSet(ushort baseOffset) : base(baseOffset) {
		}
	}

	public abstract class UShortConstantSet : UShortConstantSet<SimpleUShort> {

		protected UShortConstantSet(SimpleUShort baseOffset) : base(baseOffset) {
		}
	}
}