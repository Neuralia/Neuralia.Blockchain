using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1 {

	public interface IBlockBytesetElectionQuestion : IElectionBlockQuestion {
		BlockId BlockId { get; set; }
		AdaptiveLong1_9 Offset { get; set; }
		byte Length { get; set; }
	}

	public class BlockBytesetElectionQuestion : ElectionBlockQuestion, IBlockBytesetElectionQuestion {

		public BlockId BlockId { get; set; } = new BlockId();

		public AdaptiveLong1_9 Offset { get; set; } = new AdaptiveLong1_9();

		public byte Length { get; set; }

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty(nameof(this.BlockId), this.BlockId);
			jsonDeserializer.SetProperty(nameof(this.Offset), this.Offset);
			jsonDeserializer.SetProperty(nameof(this.Length), this.Length);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.BlockId.Rehydrate(rehydrator);
			this.Offset.Rehydrate(rehydrator);
			this.Length = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BlockId);
			nodeList.Add(this.Offset);
			nodeList.Add(this.Length);

			return nodeList;
		}

		protected override ComponentVersion<ElectionQuestionType> SetIdentity() {
			return (ElectionQuestionTypes.Instance.BlockByteset, 1, 0);
		}
	}
}