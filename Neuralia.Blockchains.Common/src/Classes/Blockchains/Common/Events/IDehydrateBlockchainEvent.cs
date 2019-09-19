using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events {
	public interface IDehydrateBlockchainEvent : IBinaryByteSerializable, IBinarySerializable, ITreeHashable {
	}
}