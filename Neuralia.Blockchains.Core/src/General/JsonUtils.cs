using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.General {
	public static class JsonUtils {

		public static string Serialize(object entry) {
			return Serialize(entry, CreateBlockSerializerSettings());
		}

		public static string Serialize(object entry, JsonSerializerOptions serializerOptions) {
			return JsonSerializer.Serialize(entry, serializerOptions);
		}

		public static JsonSerializerOptions CreateSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerOptions settings = new JsonSerializerOptions();
			settings.WriteIndented = false;
			settings.IgnoreNullValues = false;
			settings.PropertyNameCaseInsensitive = false;
			settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

			settings.Converters.Add(new ByteArrayBaseConverter(mode));
			settings.Converters.Add(new DecimalConverter());

			return settings;
		}

		public static JsonSerializerOptions CreateCompactSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerOptions settings = CreateSerializerSettings(mode);

			settings.WriteIndented = false;

			return settings;
		}

		public static JsonSerializerOptions CreateNoNamesSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerOptions settings = CreateCompactSerializerSettings(mode);

			settings.IgnoreNullValues = true;
			settings.IgnoreNullValues = true;

			return settings;
		}

		public static JsonSerializerOptions CreatePrettySerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerOptions settings = CreateSerializerSettings(mode);

			settings.WriteIndented = true;

			return settings;
		}

		public static JsonSerializerOptions CreateBlockSerializerSettings(ByteArrayBaseConverter.BaseModes mode = ByteArrayBaseConverter.BaseModes.Base58) {
			JsonSerializerOptions settings = CreateNoNamesSerializerSettings(mode);

			return settings;
		}

		public static string SerializeJsonSerializable(IJsonSerializable jsonSerializable) {

			return JsonDeserializer.Serialize(jsonSerializable);
		}
	}

	public class SafeArrayHandleConverter : JsonConverter<SafeArrayHandle> {
		public enum BaseModes {
			Base58,
			Base64
		}

		private readonly BaseModes mode;

		public SafeArrayHandleConverter(BaseModes mode = BaseModes.Base58) {
			this.mode = mode;

		}

		public override SafeArrayHandle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			throw new NotImplementedException();
		}

		public override void Write(Utf8JsonWriter writer, SafeArrayHandle value, JsonSerializerOptions options) {

			if(value.IsEmpty) {
				writer.WriteStringValue("");
			} else {
				if(this.mode == BaseModes.Base58) {
					writer.WriteStringValue(value.Entry.ToBase58());
				} else if(this.mode == BaseModes.Base64) {
					writer.WriteStringValue(value.Entry.ToBase64());
				}
			}
		}

		public override bool CanConvert(Type typeToConvert) {
			return typeof(SafeArrayHandle).IsAssignableFrom(typeToConvert) || typeof(ByteArray).IsAssignableFrom(typeToConvert);
		}
	}

	public class ByteArrayBaseConverter : JsonConverter<ByteArray> {
		public enum BaseModes {
			Base58,
			Base64
		}

		private readonly BaseModes mode;

		public ByteArrayBaseConverter(BaseModes mode = BaseModes.Base58) {
			this.mode = mode;

		}

		public override ByteArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			throw new NotImplementedException();
		}

		public override void Write(Utf8JsonWriter writer, ByteArray value, JsonSerializerOptions options) {

			if(value.IsEmpty) {
				writer.WriteStringValue("");
			} else {
				if(this.mode == BaseModes.Base58) {
					writer.WriteStringValue(value.ToBase58());
				} else if(this.mode == BaseModes.Base64) {
					writer.WriteStringValue(value.ToBase64());
				}
			}

		}

		public override bool CanConvert(Type typeToConvert) {
			return typeof(ByteArray).IsAssignableFrom(typeToConvert);
		}
	}

	internal class DecimalConverter : JsonConverter<decimal> {
		public override bool CanConvert(Type objectType) {
			return (objectType == typeof(decimal)) || (objectType == typeof(decimal?));
		}

		public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string name = reader.GetString();

			return decimal.Parse(name);
		}

		public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
		}
	}
}