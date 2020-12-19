using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	public struct XMSSNodeId {
		public XMSSNodeId(long index, byte height) {
			this.Index = index;
			this.Height = height;
		}

		public override string ToString() {
			return $"(height: {this.Height}, index: {this.Index})";
		}

		public bool Equals(XMSSNodeId other) {
			return (this.Index == other.Index) && (this.Height == other.Height);
		}

		public override bool Equals(object obj) {
			return obj is XMSSNodeId other && this.Equals(other);
		}

		public static bool operator ==(XMSSNodeId a, XMSSNodeId b) {
			return a.Equals(b);
		}

		public static bool operator !=(XMSSNodeId a, XMSSNodeId b) {
			return !(a == b);
		}

		public long Index { get; }
		public byte Height { get; }

		public static implicit operator XMSSNodeId((long index, byte height) d) {
			(long index, byte height) = d;

			return new XMSSNodeId(index, height);
		}

		public class XmssNodeIdConverterOld : JsonConverter<XMSSNodeId> {

			public class NodeIdWrapper {
				public long Index { get; set; }
				public byte Height { get; set;}
			}
			public override void WriteJson(JsonWriter writer, XMSSNodeId value, JsonSerializer serializer) {
				
				writer.WriteValue(Newtonsoft.Json.JsonConvert.SerializeObject(new NodeIdWrapper{Index = value.Index, Height = value.Height}));
			}

			public override XMSSNodeId ReadJson(JsonReader reader, Type objectType, XMSSNodeId existingValue, bool hasExistingValue, JsonSerializer serializer) {
				JToken token = JToken.Load(reader);
				
				if(token.Type == JTokenType.String) {
					// customize this to suit your needs

					var wrapper = Newtonsoft.Json.JsonConvert.DeserializeObject<NodeIdWrapper>(token.ToString());

					return new XMSSNodeId(wrapper.Index, wrapper.Height);
				}
				
				throw new JsonSerializationException("Unexpected token type: " + token.Type);
			}
		}
	}
}