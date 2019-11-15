using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Services;
using System.Text.Json;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Serialization {
	public class JsonDeserializer  {

		private readonly Utf8JsonWriter writer;
		
		public JsonDeserializer(Utf8JsonWriter writer) {
			this.writer = writer;
		}

		
		public static string Serialize(IJsonSerializable jsonSerializable) {
			
			var options = new JsonWriterOptions
			{
				Indented = false
			};
			
			using (var stream = MemoryUtils.Instance.recyclableMemoryStreamManager.GetStream("json"))
			{
				using (var writer = new Utf8JsonWriter(stream, options))
				{
					writer.WriteStartObject();
					
					JsonDeserializer deserializer = new JsonDeserializer(writer);
					jsonSerializable.JsonDehydrate(deserializer);
					
					writer.WriteEndObject();
				}

				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}

		private void DehydrateSerializable(string name, IJsonSerializable value) {
			this.writer.WriteStartObject(name);
			value?.JsonDehydrate(this);
			this.writer.WriteEndObject();
		}

		public void WriteObject(Action<JsonDeserializer> action) {
			this.writer.WriteStartObject();
			action(this);
			this.writer.WriteEndObject();
		}
		
		private void DehydrateSerializable<T>(T value, Action<JsonDeserializer, T> transform) {
			
			if(value != null) {
				transform(this, value);
			}
		}
		
		public void SetProperty(string name, IJsonSerializable value) {
			
			this.DehydrateSerializable(name, value);
		}

		public void SetProperty(string name, DateTime value) {
			
			 this.writer.WritePropertyName(name);
			 this.writer.WriteStringValue( TimeService.FormatDateTimeStandardUtc(value));
		}
		
		public void SetProperty(string name, object value) {

			object entry = this.TranslateValue(value);

			if(entry == null) {
				this.writer.WriteNull(name);
			}

			else if(entry is byte b1) {
				this.writer.WriteNumber(name, b1);
			}

			else if(entry is short s1) {
				this.writer.WriteNumber(name, s1);
			}

			else if(entry is ushort @ushort) {
				this.writer.WriteNumber(name, @ushort);
			}

			else if(entry is int i) {
				this.writer.WriteNumber(name, i);
			}

			else if(entry is uint u) {
				this.writer.WriteNumber(name, u);
			}

			else if(entry is long @long) {
				this.writer.WriteNumber(name, @long);
			}

			else if(entry is ulong @ulong) {
				this.writer.WriteNumber(name, @ulong);
			}

			else if(entry is double d) {
				this.writer.WriteNumber(name, d);
			}

			else if(entry is decimal dec) {
				this.writer.WriteNumber(name, dec);
			}

			else if(entry is bool b) {
				this.writer.WriteBoolean(name, b);
			}

			else if(entry is Guid guid) {
				this.writer.WriteString(name, guid);
			}

			else if(entry is string s) {
				this.writer.WriteString(name, s);
			}

			else if(entry is DateTime time) {
				this.writer.WriteString(name, time);
			}

			else if(entry is Enum @enum) {
				this.writer.WriteString(name, @enum.ToString());
			}
			else if(value is IJsonSerializable serializable) {
				this.SetProperty(name, serializable);
			} else {
				this.writer.WriteString(name, entry?.ToString());
			}
			
		}

		public void SetProperty(string name, object[] array) {

			this.writer.WriteStartArray(name);
			
			 foreach(var value in array) {
				this.BuildToken(value);
			 }
			 
			this.writer.WriteEndArray();
		}
		
		public void SetProperty<T>(string name, T value, Action<JsonDeserializer, T> transform) {
			
			this.writer.WriteStartObject(name);
			
			this.DehydrateSerializable(value, transform);
			
			this.writer.WriteEndObject();
		}

		public void SetArray(string name, object[] array) {

			this.writer.WriteStartArray(name);
			
			foreach(var value in array) {
				this.BuildToken(value);
			}
			 
			this.writer.WriteEndArray();
		}
		
		public void SetArray(string name, IEnumerable<object> values) {

			this.SetArray(name, values.ToArray());
		}

		public void SetArray(string name, IEnumerable values) {

			this.SetArray(name, values.Cast<object>());
		}

		public void SetArray<T>(string name, IEnumerable<T> values, Action<JsonDeserializer, T> transform) {

			this.writer.WriteStartArray(name);
			
			foreach(var value in values) {
				transform(this, value);
			}
			 
			this.writer.WriteEndArray();
		}

		
		private object TranslateValue(object value) {

			if(value == null) {
				return null;
			}

			if(value is Enum enumEntry) {
				return Enum.GetName(value.GetType(), value);
			}

			if(value is SafeArrayHandle handle) {
				value = handle.Entry;
			}

			if(value is byte[] array) {
				value = ByteArray.Wrap(array);
			}
			
			if(value is ByteArray bytearray) {
				return bytearray?.ToBase58();
			}

			return value;
		}

		private void BuildToken(object value) {

			object entry = this.TranslateValue(value);

			if(entry == null) {
				this.writer.WriteNullValue();
			}

			else if(entry is byte b1) {
				this.writer.WriteNumberValue(b1);
			}

			else if(entry is short s1) {
				this.writer.WriteNumberValue(s1);
			}

			else if(entry is ushort @ushort) {
				this.writer.WriteNumberValue(@ushort);
			}

			else if(entry is int i) {
				this.writer.WriteNumberValue(i);
			}

			else if(entry is uint u) {
				this.writer.WriteNumberValue(u);
			}

			else if(entry is long @long) {
				this.writer.WriteNumberValue(@long);
			}

			else if(entry is ulong @ulong) {
				this.writer.WriteNumberValue(@ulong);
			}

			else if(entry is double d) {
				this.writer.WriteNumberValue(d);
			}

			else if(entry is decimal dec) {
				this.writer.WriteNumberValue(dec);
			}

			else if(entry is bool b) {
				this.writer.WriteBooleanValue(b);
			}

			else if(entry is Guid guid) {
				this.writer.WriteStringValue(guid);
			}

			else if(entry is string s) {
				this.writer.WriteStringValue(s);
			}

			else if(entry is DateTime time) {
				this.writer.WriteStringValue(time);
			}

			else if(entry is Enum @enum) {
				this.writer.WriteStringValue(@enum.ToString());
			}
			else if(value is IJsonSerializable serializable) {
				this.WriteObject((s) => {
					serializable.JsonDehydrate(this);
				});
				
			} else {
				this.writer.WriteStringValue(entry?.ToString());
			}
		}
	}
}