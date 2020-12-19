using System;
using Neuralia.Blockchains.Components.Blocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Components.Converters.old {
	public class BlockIdJsonConverterOld : JsonConverter<BlockId> {
		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, BlockId value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());
		}

		public override BlockId ReadJson(JsonReader reader, Type objectType, BlockId existingValue, bool hasExistingValue, JsonSerializer serializer) {

			BlockId entry = new BlockId();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = new BlockId((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}