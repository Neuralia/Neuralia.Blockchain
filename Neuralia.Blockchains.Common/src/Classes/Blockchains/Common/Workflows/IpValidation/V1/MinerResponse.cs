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

		public AdaptiveLong1_9 Answer { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {

			int version = rehydrator.ReadByte();
			this.Response = (ResponseType) rehydrator.ReadByte();
			this.AccountId.Rehydrate(rehydrator);

			this.Answer = null;
			bool isAnswerSet = rehydrator.ReadBool();

			if(isAnswerSet) {
				this.Answer = new AdaptiveLong1_9();
				this.Answer.Rehydrate(rehydrator);
			}
		}

		public IByteArray Dehydrate() {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write(this.Version);
			dehydrator.Write((byte) this.Response);

			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.Answer != null);

			if(this.Answer != null) {
				this.Answer.Dehydrate(dehydrator);
			}
			
			return dehydrator.ToArray();
		}
	}
}