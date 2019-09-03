using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General {
	public interface ISerializableCombo : IBinarySerializable, ITreeHashable, IJsonSerializable {
	}
}