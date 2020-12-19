using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {

	public interface IBlockAccountSignature : IAccountSignature {
		bool IsHashPublished { get; set; }
		IdKeyUseIndexSet KeyUseIndex { get; set;}
	}

	public class BlockAccountSignature : AccountSignature, IBlockAccountSignature {
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("IsHashPublished", this.IsHashPublished);
			jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex);
		}

		/// <summary>
		///     This tell us if a hash has been published that can be used to supplement verification security
		/// </summary>
		public bool IsHashPublished { get; set; }

		public IdKeyUseIndexSet KeyUseIndex { get; set; } = new IdKeyUseIndexSet();

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.IsHashPublished = rehydrator.ReadBool();
			this.KeyUseIndex.Rehydrate(rehydrator);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.IsHashPublished);
			this.KeyUseIndex.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.IsHashPublished);
			nodeList.Add(this.KeyUseIndex);
			
			return nodeList;
		}
	}
}