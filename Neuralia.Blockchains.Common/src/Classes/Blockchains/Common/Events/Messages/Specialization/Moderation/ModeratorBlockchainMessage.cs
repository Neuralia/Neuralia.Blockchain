namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation {
	public interface IModeratorBlockchainMessage : IBlockchainMessage {
		
	}
	
	public abstract class ModeratorBlockchainMessage : BlockchainMessage, IModeratorBlockchainMessage {
		
	}
}