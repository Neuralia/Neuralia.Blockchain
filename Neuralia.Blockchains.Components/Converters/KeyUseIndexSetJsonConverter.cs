using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;

namespace Neuralia.Blockchains.Components.Converters {

	public class KeyUseIndexSetJsonConverter : JsonConverter<KeyUseIndexSet> {

		public override KeyUseIndexSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return new KeyUseIndexSet(name);
		}

		public override void Write(Utf8JsonWriter writer, KeyUseIndexSet value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}
	}
}