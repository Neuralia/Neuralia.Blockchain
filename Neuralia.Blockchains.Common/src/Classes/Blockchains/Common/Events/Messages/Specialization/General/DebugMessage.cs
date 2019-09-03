using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General {

	public interface IDebugMessage : IBlockchainMessage {
		string Message { get; set; }
	}

	public abstract class DebugMessage : BlockchainMessage, IDebugMessage {

		public string Message { get; set; }

		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.DEBUG, 1, 0);
		}

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			this.Message = rehydrator.ReadString();
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Message);
		}
	}
}