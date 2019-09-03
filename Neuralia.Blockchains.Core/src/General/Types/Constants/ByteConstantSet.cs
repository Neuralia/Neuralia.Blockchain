using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core.General.Types.Constants {

	public abstract class ByteConstantSet<T> : ConstantSet<T, byte>
		where T : ISimpleNumeric<T, byte>, new() {

		protected ByteConstantSet(T baseOffset) : base(baseOffset) {
		}
	}

	public abstract class ByteConstantSet : ByteConstantSet<SimpleByte> {

		protected ByteConstantSet(SimpleByte baseOffset) : base(baseOffset) {
		}
	}
}