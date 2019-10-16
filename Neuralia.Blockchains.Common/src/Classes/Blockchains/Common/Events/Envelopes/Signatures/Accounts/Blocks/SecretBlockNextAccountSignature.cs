using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface ISecretBlockNextAccountSignature : IBlockNextAccountSignature {
		SafeArrayHandle NextKeyHashSha2 { get;  }
		SafeArrayHandle NextKeyHashSha3 { get;  }
		int NonceHash { get; set; }

		QTESLASecurityCategory.SecurityCategories NextSecondSecurityCategory { get; set; }
		SafeArrayHandle NextSecondPublicKey { get;  }
	}

	public class SecretBlockNextAccountSignature : BlockNextAccountSignature, ISecretBlockNextAccountSignature {

		public SafeArrayHandle NextKeyHashSha2 { get;  } = SafeArrayHandle.Create();
		public SafeArrayHandle NextKeyHashSha3 { get;  } = SafeArrayHandle.Create();
		public int NonceHash { get; set; }

		public QTESLASecurityCategory.SecurityCategories NextSecondSecurityCategory { get; set; }
		public SafeArrayHandle NextSecondPublicKey { get;  } = SafeArrayHandle.Create();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.NextKeyHashSha2);
			nodelist.Add(this.NextKeyHashSha3);
			nodelist.Add(this.NonceHash);

			nodelist.Add((byte) this.NextSecondSecurityCategory);
			nodelist.Add(this.NextSecondPublicKey);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.NextKeyHashSha2);
			dehydrator.WriteNonNullable(this.NextKeyHashSha3);
			dehydrator.Write(this.NonceHash);

			dehydrator.Write((byte) this.NextSecondSecurityCategory);
			dehydrator.WriteNonNullable(this.NextSecondPublicKey);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.NextKeyHashSha2.Entry = rehydrator.ReadNonNullableArray();
			this.NextKeyHashSha3.Entry = rehydrator.ReadNonNullableArray();
			this.NonceHash = rehydrator.ReadInt();

			this.NextSecondSecurityCategory = (QTESLASecurityCategory.SecurityCategories) rehydrator.ReadByte();
			this.NextSecondPublicKey.Entry = rehydrator.ReadNonNullableArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("NextKeyHashSha2", this.NextKeyHashSha2);
			jsonDeserializer.SetProperty("NextKeyHashSha3", this.NextKeyHashSha3);
			jsonDeserializer.SetProperty("NonceHash", this.NonceHash);

			jsonDeserializer.SetProperty("NextSecondSecurityCategory", this.NextSecondSecurityCategory);
			jsonDeserializer.SetProperty("NextSecondPublicKey", this.NextSecondPublicKey);

		}
	}
}