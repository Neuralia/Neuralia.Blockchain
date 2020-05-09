using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Components.Blocks;

namespace Neuralia.Blockchains.Components.Converters {

	public class BlockIdJsonConverter : JsonConverter<BlockId> {

		public override BlockId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return new BlockId(name);
		}

		public override void Write(Utf8JsonWriter writer, BlockId value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}
	}
}