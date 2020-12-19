using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Utils {

	public class IdKeyUseIndexSetJsonConverter : JsonConverter<IdKeyUseIndexSet> {

		public override IdKeyUseIndexSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return new IdKeyUseIndexSet(name);
		}

		public override void Write(Utf8JsonWriter writer, IdKeyUseIndexSet value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}
	}
}