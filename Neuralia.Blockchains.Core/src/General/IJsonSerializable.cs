using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Core.General {
	public interface IJsonSerializable {
		void JsonDehydrate(JsonDeserializer jsonDeserializer);
	}

	public interface IJsonValue {
		void JsonWriteValue(string name, JsonDeserializer jsonDeserializer);
	}
}