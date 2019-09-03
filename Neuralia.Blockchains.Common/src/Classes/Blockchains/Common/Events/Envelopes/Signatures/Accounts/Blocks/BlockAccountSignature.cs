using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {

	public interface IBlockAccountSignature : IAccountSignature {
		bool IsHashPublished { get; set; }
	}

	public abstract class BlockAccountSignature : AccountSignature, IBlockAccountSignature {
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("IsHashPublished", this.IsHashPublished);
		}

		/// <summary>
		///     This tell us if a hash has been published that can be used to supplement verification security
		/// </summary>
		public bool IsHashPublished { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.IsHashPublished = rehydrator.ReadBool();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.IsHashPublished);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.IsHashPublished);

			return nodeList;
		}
	}
}