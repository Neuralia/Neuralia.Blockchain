using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface ISecretBlockNextAccountSignature : IBlockNextAccountSignature {
		IByteArray NextKeyHashSha2 { get; set; }
		IByteArray NextKeyHashSha3 { get; set; }
		int NonceHash { get; set; }

		QTESLASecurityCategory.SecurityCategories NextSecondSecurityCategory { get; set; }
		IByteArray NextSecondPublicKey { get; set; }
	}

	public class SecretBlockNextAccountSignature : BlockNextAccountSignature, ISecretBlockNextAccountSignature {

		public IByteArray NextKeyHashSha2 { get; set; }
		public IByteArray NextKeyHashSha3 { get; set; }
		public int NonceHash { get; set; }

		public QTESLASecurityCategory.SecurityCategories NextSecondSecurityCategory { get; set; }
		public IByteArray NextSecondPublicKey { get; set; }

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

			this.NextKeyHashSha2 = rehydrator.ReadNonNullableArray();
			this.NextKeyHashSha3 = rehydrator.ReadNonNullableArray();
			this.NonceHash = rehydrator.ReadInt();

			this.NextSecondSecurityCategory = (QTESLASecurityCategory.SecurityCategories) rehydrator.ReadByte();
			this.NextSecondPublicKey = rehydrator.ReadNonNullableArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PromisedNonceCombo", this.NextKeyHashSha2);
			jsonDeserializer.SetProperty("PromisedNonceSha2", this.NextKeyHashSha3);
			jsonDeserializer.SetProperty("NonceHash", this.NonceHash);

			jsonDeserializer.SetProperty("SecondSecurityCategory", this.NextSecondSecurityCategory);
			jsonDeserializer.SetProperty("SecondPublicKey", this.NextSecondPublicKey);

		}
	}
}