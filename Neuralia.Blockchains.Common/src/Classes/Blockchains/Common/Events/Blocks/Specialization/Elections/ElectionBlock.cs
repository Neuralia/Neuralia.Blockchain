using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Simple;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections {

	/// <summary>
	///     anything implementing this will trigger an election
	/// </summary>
	public interface IElectionBlock : ISimpleBlock {
		IElectionContext ElectionContext { get; set; }
		IByteArray DehydratedElectionContext { get; }
	}

	public abstract class ElectionBlock : SimpleBlock, IElectionBlock {

		// contents
		public IElectionContext ElectionContext { get; set; }

		/// <summary>
		///     the compressed ytes of the election context
		/// </summary>
		public IByteArray DehydratedElectionContext { get; private set; }

		public override HashNodeList GetStructuresArray(IByteArray previousBlockHash) {
			HashNodeList nodeList = base.GetStructuresArray(previousBlockHash);

			nodeList.Add(this.ElectionContext);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("ElectionContext", this.ElectionContext);

		}

		protected override ComponentVersion<BlockType> SetIdentity() {
			return (BlockTypes.Instance.Election, 1, 0);
		}

		protected override void RehydrateBody(IDataRehydrator rehydratorBody, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			this.DehydratedElectionContext = rehydratorBody.ReadNonNullableArray();

			IElectionContextRehydrationFactory electionContextRehydrationFactory = rehydrationFactory.CreateBlockComponentsRehydrationFactory();
			this.ElectionContext = electionContextRehydrationFactory.CreateElectionContext(this.DehydratedElectionContext);

		}
	}
}