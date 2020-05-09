using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.Questions.V1 {

	public interface IDigestBytesetElectionQuestion : IElectionDigestQuestion {
		AdaptiveLong1_9 DigestId { get; set; }
		AdaptiveLong1_9 Offset { get; set; }
		byte Length { get; set; }
	}

	public class DigestBytesetElectionQuestion : ElectionDigestQuestion, IDigestBytesetElectionQuestion {
		public AdaptiveLong1_9 DigestId { get; set; } = new AdaptiveLong1_9();

		public AdaptiveLong1_9 Offset { get; set; } = new AdaptiveLong1_9();

		public byte Length { get; set; }

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty(nameof(this.DigestId), this.DigestId.ToString());
			jsonDeserializer.SetProperty(nameof(this.Offset), this.Offset.ToString());
			jsonDeserializer.SetProperty(nameof(this.Length), this.Length.ToString());
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.DigestId.Rehydrate(rehydrator);
			this.Offset.Rehydrate(rehydrator);
			this.Length = rehydrator.ReadByte();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.DigestId);
			nodeList.Add(this.Offset);
			nodeList.Add(this.Length);

			return nodeList;
		}

		protected override ComponentVersion<ElectionQuestionType> SetIdentity() {
			return (ElectionQuestionTypes.Instance.DigestByteset, 1, 0);
		}
	}
}