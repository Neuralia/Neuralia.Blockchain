using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {

	public interface IPresentationAccountSignature : IFirstAccountKey {
	}

	public class PresentationAccountSignature : AccountSignature, IPresentationAccountSignature {
		public IByteArray PublicKey { get; set; }
		public QTESLASecurityCategory.SecurityCategories SecurityCategory { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.PublicKey);
			nodelist.Add(this.SecurityCategory);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.WriteNonNullable(this.PublicKey);
			dehydrator.Write((byte) this.SecurityCategory);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.PublicKey = rehydrator.ReadNonNullableArray();
			this.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) rehydrator.ReadByte();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("PublicKey", this.PublicKey);
			jsonDeserializer.SetProperty("SecurityCategory", this.SecurityCategory);
		}
	}
}