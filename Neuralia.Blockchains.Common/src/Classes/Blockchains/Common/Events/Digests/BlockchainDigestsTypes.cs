using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {

	public class BlockchainDigestsType : SimpleUShort<BlockchainDigestsType> {

		public BlockchainDigestsType() {
		}

		public BlockchainDigestsType(ushort value) : base(value) {
		}

		public static implicit operator BlockchainDigestsType(ushort d) {
			return new BlockchainDigestsType(d);
		}
	}

	public sealed class BlockchainDigestsTypes : UShortConstantSet<BlockchainDigestsType> {

		public readonly BlockchainDigestsType Basic;

		static BlockchainDigestsTypes() {
		}

		private BlockchainDigestsTypes() : base(1000) {
			this.Basic = this.CreateBaseConstant();

		}

		public static BlockchainDigestsTypes Instance { get; } = new BlockchainDigestsTypes();
	}
}