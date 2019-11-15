using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionTimestampJsonConverter : JsonConverter<TransactionTimestamp> {

		public override TransactionTimestamp Read(ref Utf8JsonReader reader, 
		                              Type typeToConvert,
		                              JsonSerializerOptions options)
		{
			var name = reader.GetString();

			return new TransactionTimestamp((string) name);
		}

		public override void Write(Utf8JsonWriter writer,
		                           TransactionTimestamp value,
		                           JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
		
	}
}