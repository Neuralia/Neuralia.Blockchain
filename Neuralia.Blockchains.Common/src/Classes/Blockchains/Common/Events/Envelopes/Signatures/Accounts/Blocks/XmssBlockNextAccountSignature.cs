using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface IXmssBlockNextAccountSignature : IBlockNextAccountSignature {
		
		bool KeyChange { get; set; }

		Enums.KeyHashBits HashBits { get; set; }
		byte TreeHeight { get; set; }
		byte TreeLayers { get; set; }
		SafeArrayHandle PublicKey { get;  }
	}

	public class XmssBlockNextAccountSignature : BlockNextAccountSignature, IXmssBlockNextAccountSignature {
		
		public bool KeyChange { get; set; }

		public Enums.KeyHashBits HashBits { get; set; } = Enums.KeyHashBits.SHA3_256;
		public byte TreeHeight { get; set; }
		public byte TreeLayers { get; set; }
		public SafeArrayHandle PublicKey { get;  } = SafeArrayHandle.Create();
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.KeyChange);

			if(this.KeyChange) {
				nodelist.Add((byte)this.HashBits);
				nodelist.Add(this.TreeHeight);
				nodelist.Add(this.TreeLayers);
				nodelist.Add(this.PublicKey);
			}

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.Write(this.KeyChange);

			if(this.KeyChange) {
				dehydrator.Write((byte) this.HashBits);
				dehydrator.Write(this.TreeHeight);
				dehydrator.Write(this.TreeLayers);
				dehydrator.Write(this.PublicKey);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.KeyChange = rehydrator.ReadBool();
			
			if(this.KeyChange) {
				this.HashBits = (Enums.KeyHashBits)rehydrator.ReadByte();
				this.TreeHeight = rehydrator.ReadByte();
				this.TreeLayers = rehydrator.ReadByte();
				this.PublicKey.Entry = rehydrator.ReadArray();
			}
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("KeyChange", this.KeyChange);
			
			if(this.KeyChange) {
				jsonDeserializer.SetProperty("HashBits", this.HashBits.ToString());
				jsonDeserializer.SetProperty("TreeHeight", this.TreeHeight);
				jsonDeserializer.SetProperty("TreeLayers", this.TreeLayers);
				jsonDeserializer.SetProperty("PublicKey", this.PublicKey);
			}
		}
	}
}