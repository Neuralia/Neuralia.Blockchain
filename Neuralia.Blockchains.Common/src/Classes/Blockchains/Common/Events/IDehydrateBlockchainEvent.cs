using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events {
	public interface IDehydrateBlockchainEvent : IBinaryByteSerializable, IBinarySerializable, ITreeHashable {
		void Clear();
	}
	
	public interface IDehydrateBlockchainEvent<TYPE> : IDehydrateBlockchainEvent 
		where TYPE : IBlockchainEvent{
		
		TYPE RehydratedEvent { get; set; }
		
		void Dehydrate(ChannelsEntries<IDataDehydrator> channelDehydrators);

		TYPE Rehydrate(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}
}