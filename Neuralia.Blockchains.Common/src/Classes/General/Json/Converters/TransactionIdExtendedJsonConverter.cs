using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class TransactionIdExtendedJsonConverter : JsonConverter<TransactionIdExtended> {

		public override TransactionIdExtended Read(ref Utf8JsonReader reader, 
		                              Type typeToConvert,
		                              JsonSerializerOptions options)
		{
			var name = reader.GetString();

			return new TransactionIdExtended((string) name);
		}

		public override void Write(Utf8JsonWriter writer,
		                           TransactionIdExtended value,
		                           JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToExtendedString());
		}
	}
}