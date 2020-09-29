using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface IXmssBlockNextAccountSignature : IBlockNextAccountSignature {

		bool KeyChange { get; set; }

		Enums.KeyHashType HashType { get; set; }
		Enums.KeyHashType BackupHashType { get; set; }
		byte TreeHeight { get; set; }
		SafeArrayHandle PublicKey { get; }
	}

	public class XmssBlockNextAccountSignature : BlockNextAccountSignature, IXmssBlockNextAccountSignature {

		public bool KeyChange { get; set; }

		public Enums.KeyHashType HashType { get; set; } = Enums.KeyHashType.SHA3_256;
		public Enums.KeyHashType BackupHashType { get; set; } = Enums.KeyHashType.SHA2_256;
		
		public byte TreeHeight { get; set; }
		public SafeArrayHandle PublicKey { get; } = SafeArrayHandle.Create();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.KeyChange);

			if(this.KeyChange) {
				nodelist.Add((byte) this.HashType);
				nodelist.Add((byte) this.BackupHashType);
				nodelist.Add(this.TreeHeight);
				nodelist.Add(this.PublicKey);
			}

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			dehydrator.Write(this.KeyChange);

			if(this.KeyChange) {
				dehydrator.Write((byte) this.HashType);
				dehydrator.Write((byte) this.BackupHashType);
				dehydrator.Write(this.TreeHeight);
				dehydrator.Write(this.PublicKey);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.KeyChange = rehydrator.ReadBool();

			if(this.KeyChange) {
				this.HashType = (Enums.KeyHashType) rehydrator.ReadByte();
				this.BackupHashType = (Enums.KeyHashType) rehydrator.ReadByte();
				this.TreeHeight = rehydrator.ReadByte();
				this.PublicKey.Entry = rehydrator.ReadArray();
			}
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("KeyChange", this.KeyChange);
			jsonDeserializer.SetProperty("HashType", this.HashType.ToString());
			jsonDeserializer.SetProperty("BackupHashType", this.BackupHashType.ToString());
			jsonDeserializer.SetProperty("TreeHeight", this.TreeHeight);
			jsonDeserializer.SetProperty("PublicKey", this.PublicKey);
		}
	}
}