using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {
	public class MinerResponse : IMinerResponse {

		public byte Version => 1;

		public long? SecondTierAnswer { get; set; }
		public long? DigestTierAnswer { get; set; }
		public long? FirstTierAnswer  { get; set; }

		public AccountId AccountId { get; set; } = new AccountId();
		public int ResponseCode { get; set; }
		public ResponseType Response { get; set; } = ResponseType.Invalid;

		public void Rehydrate(IDataRehydrator rehydrator) {

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			int version = rehydrator.ReadByte();
			this.Response = rehydrator.ReadByteEnum<ResponseType>();
			this.AccountId.Rehydrate(rehydrator);

			this.ResponseCode = rehydrator.ReadInt();
			
			this.SecondTierAnswer = null;
			bool isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.SecondTierAnswer = tool.Value;
			}

			this.DigestTierAnswer = null;
			isAnswerSet           = rehydrator.ReadBool();

			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.DigestTierAnswer = tool.Value;
			}
			
			this.FirstTierAnswer = null;
			isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				tool.Rehydrate(rehydrator);
				this.FirstTierAnswer = tool.Value;
			}
		}

		public SafeArrayHandle Dehydrate() {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write(this.Version);
			dehydrator.Write((byte) this.Response);

			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.ResponseCode);
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			dehydrator.Write(this.SecondTierAnswer != null);

			if(this.SecondTierAnswer != null) {
				tool.Value = this.SecondTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.DigestTierAnswer != null);

			if(this.DigestTierAnswer != null) {
				tool.Value = this.DigestTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.FirstTierAnswer != null);

			if(this.FirstTierAnswer != null) {
				tool.Value = this.FirstTierAnswer.Value;
				tool.Dehydrate(dehydrator);
			}

			return dehydrator.ToArray();
		}
	}
}