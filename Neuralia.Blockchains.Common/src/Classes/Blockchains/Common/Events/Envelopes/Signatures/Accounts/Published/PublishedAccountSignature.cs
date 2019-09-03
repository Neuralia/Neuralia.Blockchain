using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published {

	public interface IPublishedAccountSignature : IAccountSignature {

		KeyAddress KeyAddress { get; set; }

		ICryptographicKey PublicKey { get; set; }
	}

	public class PublishedAccountSignature : AccountSignature, IPublishedAccountSignature {

		public KeyAddress KeyAddress { get; set; } = new KeyAddress();

		/// <summary>
		///     the accompanying public key if applicable. this can help nodes that are not fully sync validate the message, even
		///     if we dont know if the key truly is from it's account then.
		///     At least it can allow a message to go through the gossip network even if nodes are not fully synced
		/// </summary>
		public ICryptographicKey PublicKey { get; set; }

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("KeyAddress", this.KeyAddress);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.KeyAddress);
			nodelist.Add(this.PublicKey);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			base.Dehydrate(dehydrator);

			this.KeyAddress.Dehydrate(dehydrator);

			dehydrator.Write(this.PublicKey == null);
			this.PublicKey?.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.KeyAddress.Rehydrate(rehydrator);
			bool keyIsNull = rehydrator.ReadBool();

			if(keyIsNull == false) {
				this.PublicKey = KeyFactory.RehydrateKey(rehydrator);
			}
		}
	}
}