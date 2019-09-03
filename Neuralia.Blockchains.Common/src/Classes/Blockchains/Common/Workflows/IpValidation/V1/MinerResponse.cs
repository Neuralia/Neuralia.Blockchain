using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {
	public class MinerResponse : IMinerResponse {

		public AccountId AccountId { get; set; } = new AccountId();
		public ResponseType Response { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.Response = (ResponseType) rehydrator.ReadByte();
			this.AccountId.Rehydrate(rehydrator);
		}

		public IByteArray Dehydrate() {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write((byte) this.Response);

			this.AccountId.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}
	}
}