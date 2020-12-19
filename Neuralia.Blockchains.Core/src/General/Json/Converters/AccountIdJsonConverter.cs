using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Core.General.Types;
namespace Neuralia.Blockchains.Core.General.Json.Converters {

	public class AccountIdJsonConverter : JsonConverter<AccountId> {

		public override AccountId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return AccountId.FromString(name);
		}

		public override void Write(Utf8JsonWriter writer, AccountId value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}
	}
}