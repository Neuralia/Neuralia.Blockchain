using System;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated {

	public interface IThreeWayGatedTransaction : IGatedTransaction<IThreeWayJointSignatureType> {
		AccountId VerifierAccountId { get; set; }
		AccountId ReceiverAccountId { get; set; }
		byte Duration { get; set; }
	}
	
	public abstract class ThreeWayGatedTransaction : GatedTransaction<IThreeWayJointSignatureType>, IThreeWayGatedTransaction {

		
		public AccountId VerifierAccountId { get; set; } = new AccountId();
		public AccountId ReceiverAccountId { get; set; } = new AccountId();

		/// <summary>
		/// time in days
		/// </summary>
		public byte Duration { get; set; } = 10;
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			
			nodeList.Add(this.VerifierAccountId);
			nodeList.Add(this.ReceiverAccountId);
			nodeList.Add(this.Duration);

			return nodeList;
		}

		protected override void Sanitize() {
			base.Sanitize();

			// thats more than enough!
			if(this.Duration > 100) {
				this.Duration = 100;
			}
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("VerifierAccountId", this.VerifierAccountId);
			jsonDeserializer.SetProperty("ReceiverAccountId", this.ReceiverAccountId);
			jsonDeserializer.SetProperty("ReceiverAccountId", this.Duration);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);
			
			this.VerifierAccountId.Rehydrate(rehydrator);
			this.ReceiverAccountId.Rehydrate(rehydrator);
			
			this.Duration = rehydrator.ReadByte();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.VerifierAccountId.Dehydrate(dehydrator);
			this.ReceiverAccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.Duration);
		}

		
		public override ImmutableList<AccountId> TargetAccounts => new [] {this.VerifierAccountId, this.SenderAccountId, this.ReceiverAccountId}.ToImmutableList();
	}
}