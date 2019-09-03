using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results {
	public interface IElectionResult : IVersionable<ElectionContextType> {
		byte BlockOffset { get; set; }

		void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree);
	}

	public abstract class ElectionResult : Versionable<ElectionContextType>, IElectionResult {

		/// <summary>
		///     Maturity time from current block to give us the block ID
		/// </summary>
		public byte BlockOffset { get; set; }

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("BlockOffset", this.BlockOffset);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.BlockOffset = rehydrator.ReadByte();
		}

		public virtual void Rehydrate(IDataRehydrator rehydrator, Dictionary<int, TransactionId> transactionIndexesTree) {
			this.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BlockOffset);

			return nodeList;
		}
	}
}