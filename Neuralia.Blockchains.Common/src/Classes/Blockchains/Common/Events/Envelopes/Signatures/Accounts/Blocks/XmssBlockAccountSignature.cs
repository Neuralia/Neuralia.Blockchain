using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public interface IXmssBlockAccountSignature : IBlockAccountSignature {
		KeyAddress KeyAddress { get; set; }
	}

	public class XmssBlockAccountSignature : BlockAccountSignature, IXmssBlockAccountSignature {

		public KeyAddress KeyAddress { get; set; } = new KeyAddress();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.KeyAddress);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			this.KeyAddress.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.KeyAddress.Rehydrate(rehydrator);
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("KeyAddress", this.KeyAddress);
		}
	}
}