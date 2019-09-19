using System;
using Neuralia.Blockchains.Tools.Data;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Core.Debugging {
	/// <summary>
	///     An extension for Newtonsoft json serialization to transform byte arrays and ByteArray into base58 when in
	///     json form.false
	///     used for debugging
	/// </summary>
	public class ByteConverter : JsonConverter {
		public override bool CanRead => false;

		public override bool CanWrite => true;

		public override bool CanConvert(Type objectType) {
			return (objectType == typeof(byte[])) || (objectType == typeof(ByteArray));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			if(value is byte[] bytes) {
				writer.WriteValue(ByteArray.Create(bytes).ToBase58());
			} else {
				if(value is ByteArray array) {
					writer.WriteValue(array.ToBase58());
				}
			}
		}
	}
}