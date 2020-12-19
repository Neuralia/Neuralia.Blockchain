using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks {

	public class BlockType : SimpleUShort<BlockType> {

		public BlockType() {
		}

		public BlockType(ushort value) : base(value) {
		}

		public static implicit operator BlockType(ushort d) {
			return new BlockType(d);
		}
	}

	public sealed class BlockTypes : UShortConstantSet<BlockType> {

		public readonly BlockType Election;
		public readonly BlockType Genesis;
		public readonly BlockType Simple;

		static BlockTypes() {
		}

		private BlockTypes() : base(1000) {
			this.Genesis = this.CreateBaseConstant();
			this.Simple = this.CreateBaseConstant();
			this.Election = this.CreateBaseConstant();
		}

		public static BlockTypes Instance { get; } = new BlockTypes();
	}
}