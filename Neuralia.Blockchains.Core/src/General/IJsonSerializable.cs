using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Core.General {
	public interface IJsonSerializable {
		void JsonDehydrate(JsonDeserializer jsonDeserializer);
	}
}