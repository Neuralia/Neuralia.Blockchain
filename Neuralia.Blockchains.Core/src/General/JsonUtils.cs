using System;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Core.General {
	public static class JsonUtils {

		public static JsonSerializer CreateSerializer() {
			return JsonSerializer.Create(CreateBlockSerializerSettings());
		}

		public static JsonSerializerSettings CreateSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerSettings settings = new JsonSerializerSettings();
			settings.Formatting = Formatting.None;
			settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
			settings.TypeNameHandling = TypeNameHandling.Objects;
			settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
			settings.NullValueHandling = NullValueHandling.Include;
			settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
			settings.MissingMemberHandling = MissingMemberHandling.Ignore;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
			settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

			settings.Converters.Add(new ByteArrayBaseConverter(mode));
			settings.Converters.Add(new DecimalConverter());

			return settings;
		}

		public static JsonSerializerSettings CreateCompactSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerSettings settings = CreateSerializerSettings(mode);

			settings.Formatting = Formatting.None;

			return settings;
		}

		public static JsonSerializerSettings CreateNoNamesSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerSettings settings = CreateCompactSerializerSettings(mode);

			settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.NullValueHandling = NullValueHandling.Ignore;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			return settings;
		}

		public static JsonSerializerSettings CreatePrettySerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerSettings settings = CreateSerializerSettings(mode);

			settings.Formatting = Formatting.Indented;

			return settings;
		}

		public static JsonSerializerSettings CreateBlockSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerSettings settings = CreateNoNamesSerializerSettings(mode);

			settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

			return settings;
		}

		public static string SerializeJsonSerializable(IJsonSerializable jsonSerializable) {

			JsonDeserializer deserializer = new JsonDeserializer();

			jsonSerializable.JsonDehydrate(deserializer);

			return deserializer.Serialize();
		}
	}

	public class ByteArrayBaseConverter : JsonConverter {
		public enum BaseModes {
			Base58,
			Base64
		}

		private readonly BaseModes mode;

		public ByteArrayBaseConverter(BaseModes mode = BaseModes.Base58) {
			this.mode = mode;

		}

		public override bool CanRead => false;

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			if(value is SafeArrayHandle arrayWrapper) {

				if(arrayWrapper.IsEmpty) {
					new JValue("").WriteTo(writer);
				} else {
					if(this.mode == BaseModes.Base58) {
						new JValue(arrayWrapper.Entry.ToBase58()).WriteTo(writer);
					} else if(this.mode == BaseModes.Base64) {
						new JValue(arrayWrapper.Entry.ToBase64()).WriteTo(writer);
					}
				}
			}
			else if(value is ByteArray byteArray) {

				if(this.mode == BaseModes.Base58) {
					new JValue(byteArray.ToBase58()).WriteTo(writer);
				} else if(this.mode == BaseModes.Base64) {
					new JValue(byteArray.ToBase64()).WriteTo(writer);
				}
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {

			throw new NotImplementedException();

			// if(reader.Value == null) {
			// 	return null;
			// }
			//
			// string basevalue = reader.Value.ToString();
			//
			// if(this.mode == BaseModes.Base58) {
			// 	return ByteArray.FromBase58(basevalue);
			// }
			//
			// if(this.mode == BaseModes.Base64) {
			// 	return ByteArray.FromBase64(basevalue);
			// }
			//
			// return null;
		}

		public override bool CanConvert(Type objectType) {
			return typeof(SafeArrayHandle).IsAssignableFrom(objectType);
		}
	}

	internal class DecimalConverter : JsonConverter {
		public override bool CanConvert(Type objectType) {
			return (objectType == typeof(decimal)) || (objectType == typeof(decimal?));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			JToken neuralium = JToken.Load(reader);

			if((neuralium.Type == JTokenType.Float) || (neuralium.Type == JTokenType.Integer)) {
				return neuralium.ToObject<decimal>();
			}

			if(neuralium.Type == JTokenType.String) {
				// customize this to suit your needs
				return decimal.Parse(neuralium.ToString());
			}

			if((neuralium.Type == JTokenType.Null) && (objectType == typeof(decimal?))) {
				return null;
			}

			throw new JsonSerializationException("Unexpected neuralium type: " + neuralium.Type);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			writer.WriteValue(value.ToString());

		}
	}
}