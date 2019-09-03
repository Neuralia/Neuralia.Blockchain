using System;
using Neuralia.Blockchains.Core.General.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Core.General.Json.Converters {

	public class AccountIdJsonConverter : JsonConverter<AccountId> {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, AccountId value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());
		}

		public override AccountId ReadJson(JsonReader reader, Type objectType, AccountId existingValue, bool hasExistingValue, JsonSerializer serializer) {

			AccountId entry = new AccountId();

			if(reader.TokenType != JsonToken.Null) {
				JValue jValue = new JValue(reader.Value);

				switch(reader.TokenType) {
					case JsonToken.String:
						entry = AccountId.FromString((string) jValue);

						break;
				}
			}

			return entry;
		}
	}
}