using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {

	public interface ISuperSecretBlockAccountSignature : IBlockAccountSignature {
		KeyAddress KeyAddress { get; set; }

		long PromisedNonce1 { get; set; }
		long PromisedNonce2 { get; set; }
		IByteArray PromisedPublicKey { get; set; }

		Guid ConfirmationUuid { get; set; }
	}

	/// <summary>
	///     a special secret key where we use one of our special super keys
	/// </summary>
	public class SuperSecretBlockAccountSignature : BlockAccountSignature, ISuperSecretBlockAccountSignature {

		public KeyAddress KeyAddress { get; set; } = new KeyAddress();
		public long PromisedNonce1 { get; set; }
		public long PromisedNonce2 { get; set; }

		public IByteArray PromisedPublicKey { get; set; }

		public Guid ConfirmationUuid { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodelist = base.GetStructuresArray();

			nodelist.Add(this.KeyAddress);
			nodelist.Add(this.PromisedNonce1);
			nodelist.Add(this.PromisedNonce2);
			nodelist.Add(this.PromisedPublicKey);

			nodelist.Add(this.ConfirmationUuid);

			return nodelist;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.KeyAddress.Dehydrate(dehydrator);
			dehydrator.Write(this.PromisedNonce1);
			dehydrator.Write(this.PromisedNonce2);
			dehydrator.WriteNonNullable(this.PromisedPublicKey);

			dehydrator.Write(this.ConfirmationUuid);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.KeyAddress.Rehydrate(rehydrator);

			this.PromisedNonce1 = rehydrator.ReadLong();
			this.PromisedNonce2 = rehydrator.ReadLong();
			this.PromisedPublicKey = rehydrator.ReadNonNullableArray();

			this.ConfirmationUuid = rehydrator.ReadGuid();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("KeyAddress", this.KeyAddress);

			jsonDeserializer.SetProperty("PromisedNonce1", this.PromisedNonce1);
			jsonDeserializer.SetProperty("PromisedNonce2", this.PromisedNonce2);
			jsonDeserializer.SetProperty("PromisedPublicKey", this.PromisedPublicKey);
		}
	}
}