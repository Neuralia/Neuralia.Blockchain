using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionIdJsonConverter : JsonConverter<TransactionId> {

		public override TransactionId Read(ref Utf8JsonReader reader, 
		                              Type typeToConvert,
		                              JsonSerializerOptions options)
		{
			var name = reader.GetString();

			return new TransactionId((string) name);
		}

		public override void Write(Utf8JsonWriter writer,
		                           TransactionId value,
		                           JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}