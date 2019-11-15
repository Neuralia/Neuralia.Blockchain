using System;
using Neuralia.Blockchains.Core.General.Types;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Core.General.Json.Converters {

	public class AccountIdJsonConverter : JsonConverter<AccountId> {

		public override AccountId Read(ref Utf8JsonReader reader, 
		                              Type typeToConvert,
		                              JsonSerializerOptions options)
		{
			var name = reader.GetString();

			return AccountId.FromString((string) name);
		}

		public override void Write(Utf8JsonWriter writer,
		                           AccountId value,
		                           JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}