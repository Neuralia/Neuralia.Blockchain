using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionIdJsonConverter : JsonConverter<TransactionId> {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, TransactionId value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());
		}

		public override TransactionId ReadJson(JsonReader reader, Type objectType, TransactionId existingValue, bool hasExistingValue, JsonSerializer serializer) {

			TransactionId entry = new TransactionId();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = new TransactionId((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}