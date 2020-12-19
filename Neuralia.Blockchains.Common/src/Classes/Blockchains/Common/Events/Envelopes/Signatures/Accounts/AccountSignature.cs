using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {

	public interface IAccountSignature : IAccountSignatureBase {

		SafeArrayHandle Autograph { get; }
	}

	public class AccountSignature : AccountSignatureBase, IAccountSignature {

		public SafeArrayHandle Autograph { get; } = SafeArrayHandle.CreateZeroArray();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.Autograph);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);
			dehydrator.WriteNonNullable(this.Autograph);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);
			this.Autograph.Entry = rehydrator.ReadNonNullableArray();

		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("Autograph", this.Autograph);
		}
	}
}