using System;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Core.General {
	public static class JsonUtilsOld {

		public static string Serialize(object value) {
			JsonSerializerSettings settings = CreateNoNamesSerializerSettings();

			return JsonConvert.SerializeObject(value, settings);
		}

		public static T Deserialize<T>(string value) {
			JsonSerializerSettings settings = CreateNoNamesSerializerSettings();

			return JsonConvert.DeserializeObject<T>(value, settings);
		}

		public static string SerializeManifest(object value, JsonConverter[] converters) {
			JsonSerializerSettings settings = CreateSerializerSettings();
			settings.Formatting = Formatting.Indented;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			foreach(JsonConverter cvt in converters) {
				settings.Converters.Add(cvt);
			}

			return JsonConvert.SerializeObject(value, settings);
		}

		public static T DeserializeManifest<T>(string value, JsonConverter[] converters) {
			JsonSerializerSettings settings = CreateSerializerSettings();
			settings.Formatting = Formatting.Indented;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			foreach(JsonConverter cvt in converters) {
				settings.Converters.Add(cvt);
			}

			return JsonConvert.DeserializeObject<T>(value, settings);
		}

		public static JsonSerializer CreateSerializer() {
			return JsonSerializer.Create(CreateBlockSerializerSettings());
		}

		public static JsonSerializerSettings CreateSerializerSettings() {
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

			settings.Converters.Add(new ByteArrayBaseConverterOld());
			settings.Converters.Add(new DecimalConverterOld());
			settings.Converters.Add(new ComponentVersionConverterOld());

			return settings;
		}

		public static JsonSerializerSettings CreateDigestChannelSerializerSettings() {
			JsonSerializerSettings settings = CreatePrettySerializerSettings();

			settings.TypeNameHandling = TypeNameHandling.None;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			return settings;
		}

		public static JsonSerializerSettings CreateCompactSerializerSettings() {
			JsonSerializerSettings settings = CreateSerializerSettings();

			settings.Formatting = Formatting.None;

			return settings;
		}

		public static JsonSerializerSettings CreateNoNamesSerializerSettings() {
			JsonSerializerSettings settings = CreateCompactSerializerSettings();

			settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
			settings.TypeNameHandling = TypeNameHandling.None;
			settings.NullValueHandling = NullValueHandling.Ignore;
			settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

			return settings;
		}

		public static JsonSerializerSettings CreatePrettySerializerSettings() {
			JsonSerializerSettings settings = CreateSerializerSettings();

			settings.Formatting = Formatting.Indented;

			return settings;
		}

		public static JsonSerializerSettings CreateBlockSerializerSettings() {
			JsonSerializerSettings settings = CreateNoNamesSerializerSettings();

			settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

			return settings;
		}

		public static string SerializeJsonSerializable(IJsonSerializable jsonSerializable) {

			return JsonDeserializer.Serialize(jsonSerializable);
		}
	}

	public class ComponentVersionConverterOld : JsonConverter {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			if(value is ComponentVersion cversion) {

				new JValue(cversion.ToString()).WriteTo(writer);
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {

			if(reader.Value == null) {
				return null;
			}

			string basevalue = reader.Value.ToString();

			if(typeof(ComponentVersion).IsAssignableFrom(objectType)) {
				return new ComponentVersion(basevalue);
			}

			return null;
		}

		public override bool CanConvert(Type objectType) {
			return typeof(ComponentVersion).IsAssignableFrom(objectType);
		}
	}

	public class ComponentVersionTypedConverterOld<T> : JsonConverter
		where T : SimpleUShort<T>, new() {

		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			if(value is ComponentVersion<T> cversion) {

				new JValue(cversion.ToString()).WriteTo(writer);
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {

			if(reader.Value == null) {
				return null;
			}

			string basevalue = reader.Value.ToString();

			if(typeof(ComponentVersion<T>).IsAssignableFrom(objectType)) {
				return new ComponentVersion(basevalue);
			}

			return null;
		}

		public override bool CanConvert(Type objectType) {
			return typeof(ComponentVersion<T>).IsAssignableFrom(objectType);
		}
	}

	public class ByteArrayBaseConverterOld : JsonConverter {
		public enum BaseModes {
			Base58,
			Base64
		}

		private readonly BaseModes mode;

		public ByteArrayBaseConverterOld(BaseModes mode = BaseModes.Base58) {
			this.mode = mode;

		}

		public override bool CanRead => true;

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
			} else if(value is ByteArray byteArray) {

				if(this.mode == BaseModes.Base58) {
					new JValue(byteArray.ToBase58()).WriteTo(writer);
				} else if(this.mode == BaseModes.Base64) {
					new JValue(byteArray.ToBase64()).WriteTo(writer);
				}
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {

			if(reader.Value == null) {
				return null;
			}

			string basevalue = reader.Value.ToString();

			SafeArrayHandle array = null;

			if(this.mode == BaseModes.Base58) {
				array = ByteArray.FromBase58(basevalue);
			}

			if(this.mode == BaseModes.Base64) {
				array = ByteArray.FromBase64(basevalue);
			}

			if(typeof(SafeArrayHandle).IsAssignableFrom(objectType)) {
				return array;
			}

			if(typeof(ByteArray).IsAssignableFrom(objectType)) {
				return array.Entry;
			}

			return null;
		}

		public override bool CanConvert(Type objectType) {
			return typeof(SafeArrayHandle).IsAssignableFrom(objectType) || typeof(ByteArray).IsAssignableFrom(objectType);
		}
	}

	internal class DecimalConverterOld : JsonConverter {
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