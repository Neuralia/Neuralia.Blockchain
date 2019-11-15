using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.General.Json.Converters {

	public class BlockIdJsonConverter : JsonConverter<BlockId> {

		public override BlockId Read(ref Utf8JsonReader reader, 
		                              Type typeToConvert,
		                              JsonSerializerOptions options)
		{
			var name = reader.GetString();

			return new BlockId((string) name);
		}

		public override void Write(Utf8JsonWriter writer,
		                           BlockId value,
		                           JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
		
		
	}
}