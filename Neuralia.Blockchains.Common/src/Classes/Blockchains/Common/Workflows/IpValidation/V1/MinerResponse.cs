using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1 {
	public class MinerResponse : Versionable, IMinerResponse {

		public List<IValidatorOperationResponse> Operations { get; } = new List<IValidatorOperationResponse>();
		public AccountId AccountId { get; set; } = new AccountId();
		public int ResponseCode { get; set; }
		public ResponseType Response { get; set; } = ResponseType.Invalid;
		public SoftwareVersion Compatibility { get; } = new SoftwareVersion(0,0,0,1);

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);
			
			this.Response = rehydrator.ReadByteEnum<ResponseType>();
			this.AccountId.Rehydrate(rehydrator);

			this.ResponseCode = rehydrator.ReadInt();
			
			this.Compatibility.Rehydrate(rehydrator);

			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			this.Operations.Clear();
			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			for(int i = 0; i < count; i++) {

				IValidatorOperationResponse operation = ValidatorOperationRehydrator.RehydrateResponse(rehydrator);
				
				operation.Rehydrate(rehydrator);

				this.Operations.Add(operation);
			}
		}

		public SafeArrayHandle Dehydrate() {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			
			dehydrator.Write((byte) this.Response);

			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.ResponseCode);
			
			this.Compatibility.Dehydrate(dehydrator);
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

			tool.Value = this.Operations.Count;
			tool.Dehydrate(dehydrator);

			foreach(var operation in this.Operations) {
				operation.Dehydrate(dehydrator);
			}
		}
		
		protected override ComponentVersion SetIdentity() {
			return (1,0);
		}
	}
}