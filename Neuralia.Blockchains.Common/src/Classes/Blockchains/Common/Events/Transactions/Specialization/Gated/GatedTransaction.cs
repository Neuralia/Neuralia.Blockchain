using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated {

	public interface IGatedTransaction<out T> : ITransaction, IJointTransaction<T>
		where T : IJointSignatureType {
		uint CorrelationId { get; set; }
		AccountId SenderAccountId { get; }
	}

	public abstract class GatedTransaction<T> : Transaction, IGatedTransaction<T>
		where T : IJointSignatureType {

		public bool TermsOfUseAccepted { get; set; }
		public uint CorrelationId { get; set; }
		public AccountId SenderAccountId => this.TransactionId.Account;

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TermsOfUseAccepted);
			nodeList.Add(this.CorrelationId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TermsOfUseAccepted", this.TermsOfUseAccepted);
			jsonDeserializer.SetProperty("SenderAccountId", this.SenderAccountId);
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId);
		}

		public override ImmutableList<AccountId> TargetAccounts => new[] {this.SenderAccountId}.ToImmutableList();

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.TermsOfUseAccepted = rehydrator.ReadBool();

			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Rehydrate(rehydrator);
			this.CorrelationId = (uint) data.Value;
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.TermsOfUseAccepted);

			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Value = this.CorrelationId;
			data.Dehydrate(dehydrator);
		}
	}
}