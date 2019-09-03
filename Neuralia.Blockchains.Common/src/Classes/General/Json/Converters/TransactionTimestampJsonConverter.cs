using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionTimestampJsonConverter : JsonConverter<TransactionTimestamp> {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, TransactionTimestamp value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());
		}

		public override TransactionTimestamp ReadJson(JsonReader reader, Type objectType, TransactionTimestamp existingValue, bool hasExistingValue, JsonSerializer serializer) {

			TransactionTimestamp entry = new TransactionTimestamp();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = new TransactionTimestamp((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}