using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.General;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Core.Serialization {
	public class JsonDeserializer {

		private readonly JsonSerializer jsonSerializer = JsonUtils.CreateSerializer();

		public JsonDeserializer() {

		}

		private JsonDeserializer(JsonSerializer jsonSerializer) {

		}

		public JToken RootBase { get; private set; } = new JObject();

		public JObject Root => (JObject) this.RootBase;

		public string Serialize() {
			return this.Root.ToString(Formatting.None);
		}

		private JToken DehydrateSerializable(IJsonSerializable value) {
			JsonDeserializer subSerializer = new JsonDeserializer(this.jsonSerializer);
			value.JsonDehydrate(subSerializer);

			return subSerializer.RootBase;
		}

		private JToken DehydrateSerializable<T>(T value, Action<JsonDeserializer, T> transform) {
			JsonDeserializer subSerializer = new JsonDeserializer(this.jsonSerializer);
			transform(subSerializer, value);

			return subSerializer.RootBase;
		}

		public void SetValue(IJsonSerializable value) {

			this.RootBase = this.DehydrateSerializable(value);
		}

		public void SetValue(object value) {
			this.RootBase = this.BuildToken(value);
		}

		public void SetProperty(string name, IJsonSerializable value) {

			this.Root.Add(new JProperty(name, this.DehydrateSerializable(value)));
		}

		public void SetProperty(string name, object value) {

			this.Root.Add(new JProperty(name, this.BuildToken(value)));
		}

		public void SetProperty(string name, JArray array) {

			this.Root.Add(new JProperty(name, array));
		}

		public void SetProperty<T>(string name, T value, Action<JsonDeserializer, T> transform) {
			this.Root.Add(new JProperty(name, this.DehydrateSerializable(value, transform)));
		}

		public void SetArray<T>(string name, IEnumerable<T> values, Action<JsonDeserializer, T> transform) {

			this.SetProperty(name, new JArray(values.Select(e => this.DehydrateSerializable(e, transform))));
		}

		public void SetArray(string name, IJsonSerializable[] values) {

			this.SetProperty(name, new JArray(values.Select(this.DehydrateSerializable)));
		}

		public void SetArray(string name, IEnumerable<object> values) {

			this.SetProperty(name, new JArray(values.Select(o => {

				if(o is IJsonSerializable serializable) {
					return this.DehydrateSerializable(serializable);
				}

				return this.BuildToken(o);
			})));
		}

		public void SetArray(string name, IEnumerable values) {

			this.SetArray(name, values.Cast<object>());
		}

		private object TranslateValue(object value) {

			if(value == null) {
				return null;
			}

			if(value is Enum enumEntry) {
				return Enum.GetName(value.GetType(), value);
			}

			return value;
		}

		private JToken BuildToken(object value) {
			JToken token = null;

			if(value != null) {
				token = JToken.FromObject(this.TranslateValue(value), this.jsonSerializer);
			}

			return token;
		}
	}
}