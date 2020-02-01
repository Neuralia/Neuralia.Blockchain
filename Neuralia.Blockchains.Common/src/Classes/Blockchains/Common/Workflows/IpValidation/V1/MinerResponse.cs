using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {
	public class MinerResponse : IMinerResponse {

		public byte Version => 1;
		
		public AccountId AccountId { get; set; } = new AccountId();
		public ResponseType Response { get; set; }

		public AdaptiveLong1_9 SecondTierAnswer { get; set; }
		public AdaptiveLong1_9 FirstTierAnswer { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {

			int version = rehydrator.ReadByte();
			this.Response = (ResponseType) rehydrator.ReadByte();
			this.AccountId.Rehydrate(rehydrator);

			this.SecondTierAnswer = null;
			bool isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				this.SecondTierAnswer = new AdaptiveLong1_9();
				this.SecondTierAnswer.Rehydrate(rehydrator);
			}
			
			this.FirstTierAnswer = null;
			isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				this.FirstTierAnswer = new AdaptiveLong1_9();
				this.FirstTierAnswer.Rehydrate(rehydrator);
			}
		}

		public SafeArrayHandle Dehydrate() {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write(this.Version);
			dehydrator.Write((byte) this.Response);

			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.SecondTierAnswer != null);

			if(this.SecondTierAnswer != null) {
				this.SecondTierAnswer.Dehydrate(dehydrator);
			}
			
			dehydrator.Write(this.FirstTierAnswer != null);

			if(this.FirstTierAnswer != null) {
				this.FirstTierAnswer.Dehydrate(dehydrator);
			}
			
			return dehydrator.ToArray();
		}
	}
}