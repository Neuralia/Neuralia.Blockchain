using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;

namespace Neuralia.Blockchains.Components.Converters.old {

	public class TransactionIdJsonConverter : JsonConverter<TransactionId> {

		public override TransactionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return new TransactionId(name);
		}

		public override void Write(Utf8JsonWriter writer, TransactionId value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}
	}
}