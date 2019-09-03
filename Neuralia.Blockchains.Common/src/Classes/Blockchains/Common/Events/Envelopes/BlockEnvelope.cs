using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IBlockEnvelope : IEnvelope<IDehydratedBlock> {
		long BlockId { get; set; }
	}

	public class BlockEnvelope<BLOCK_TYPE> : Envelope<IDehydratedBlock, EnvelopeType>, IBlockEnvelope
		where BLOCK_TYPE : IBlock {

		protected BlockEnvelope() {

		}

		public long BlockId { get; set; }

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Contents.Hash);
			nodeList.Add(this.BlockId);

			return nodeList;
		}

		protected override IDehydratedBlock RehydrateContents(IDataRehydrator rh) {

			IDehydratedBlock dehydratedBlock = new DehydratedBlock();
			dehydratedBlock.Rehydrate(rh);

			return dehydratedBlock;
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {

			// whatever else we want to dehydrate
			dehydrator.Write(this.BlockId);
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			this.BlockId = rehydrator.ReadLong();
		}

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.Block, 1, 0);
		}
	}
}