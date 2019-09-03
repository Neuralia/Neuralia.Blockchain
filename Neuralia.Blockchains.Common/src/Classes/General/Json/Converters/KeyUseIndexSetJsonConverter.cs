using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class KeyUseIndexSetJsonConverter : JsonConverter<KeyUseIndexSet> {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, KeyUseIndexSet value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());
		}

		public override KeyUseIndexSet ReadJson(JsonReader reader, Type objectType, KeyUseIndexSet existingValue, bool hasExistingValue, JsonSerializer serializer) {

			KeyUseIndexSet entry = new KeyUseIndexSet();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = new KeyUseIndexSet((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}