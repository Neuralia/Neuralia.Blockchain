using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages {

	public class BlockchainMessageType : SimpleUShort<BlockchainMessageType> {

		public BlockchainMessageType() {
		}

		public BlockchainMessageType(ushort value) : base(value) {
		}

		public static implicit operator BlockchainMessageType(ushort d) {
			return new BlockchainMessageType(d);
		}
	}

	public sealed class BlockchainMessageTypes : UShortConstantSet<BlockchainMessageType> {

		public readonly BlockchainMessageType ACTIVE_ELECTION_CANDIDACY;
		public readonly BlockchainMessageType DEBUG;
		public readonly BlockchainMessageType ELECTIONS_REGISTRATION;
		public readonly BlockchainMessageType PASSIVE_ELECTION_CANDIDACY;

		static BlockchainMessageTypes() {
		}

		private BlockchainMessageTypes() : base(50) {
			this.DEBUG = this.CreateBaseConstant();
			this.ELECTIONS_REGISTRATION = this.CreateBaseConstant();
			this.PASSIVE_ELECTION_CANDIDACY = this.CreateBaseConstant();
			this.ACTIVE_ELECTION_CANDIDACY = this.CreateBaseConstant();
		}

		public static BlockchainMessageTypes Instance { get; } = new BlockchainMessageTypes();
	}
}