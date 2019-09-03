using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionIdExtendedJsonConverter : JsonConverter<TransactionIdExtended> {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, TransactionIdExtended value, JsonSerializer serializer) {
			writer.WriteValue(value.ToExtendedString());
		}

		public override TransactionIdExtended ReadJson(JsonReader reader, Type objectType, TransactionIdExtended existingValue, bool hasExistingValue, JsonSerializer serializer) {

			TransactionIdExtended entry = new TransactionIdExtended();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = new TransactionIdExtended((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}