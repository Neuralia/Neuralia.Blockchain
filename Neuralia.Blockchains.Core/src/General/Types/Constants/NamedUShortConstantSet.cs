using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core.General.Types.Constants {

	public abstract class NamedUShortConstantSet<T> : UShortConstantSet<T>
		where T : NamedSimpleUShort<T>, new() {

		protected NamedUShortConstantSet(T baseOffset) : base(baseOffset) {
		}

		protected NamedUShortConstantSet(ushort baseOffset) : base(baseOffset) {
		}
		
		protected void CreateBaseConstant(ref T code, string name, T offset = default) {

			code = this.CreateBaseConstant();
			code.ErrorName = name;
		}
	}
}