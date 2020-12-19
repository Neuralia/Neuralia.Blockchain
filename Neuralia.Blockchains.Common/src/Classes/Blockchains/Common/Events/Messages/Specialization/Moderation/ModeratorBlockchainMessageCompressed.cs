namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation {

	public interface IModeratorBlockchainMessageCompressed : IBlockchainMessageCompressed, IModeratorBlockchainMessage {
		
	}
	
	public abstract class ModeratorBlockchainMessageCompressed : BlockchainMessageCompressed, IModeratorBlockchainMessageCompressed {
		
	}
}