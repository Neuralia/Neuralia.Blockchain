using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {

	public class ValidatorRequest : IValidatorRequest {

		public long Password { get; set; }

		public BlockchainType Chain { get; set; }

		public byte Version => 1;

		public IValidatorRequest Rehydrate(IDataRehydrator rehydrator) {

			this.Password = rehydrator.ReadLong();
			this.Chain = rehydrator.ReadUShort();

			return this;
		}

		public IByteArray Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Password);
			dehydrator.Write(this.Chain.Value);

			return dehydrator.ToArray();
		}
	}
}