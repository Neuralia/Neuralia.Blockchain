using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated {

	public interface IGatedTransaction : ITransaction {
		uint CorrelationId { get; set; }
		AccountId SenderAccountId { get; }
		
		bool TermsOfUseAccepted { get; set; }
		ComponentVersion TermsOfUseVersion { get; set; }
	}

	public interface IGatedTransaction<out T> : IGatedTransaction, IJointTransaction<T>
		where T : IJointSignatureType {

	}

	public abstract class GatedTransaction<T> : Transaction, IGatedTransaction<T>
		where T : IJointSignatureType {

		public ComponentVersion TermsOfUseVersion { get; set; }
		public bool TermsOfUseAccepted { get; set; }
		public uint CorrelationId { get; set; }
		public AccountId SenderAccountId => this.TransactionId.Account;

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {

			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.TermsOfUseVersion);
			nodeList.Add(this.TermsOfUseAccepted);
			nodeList.Add(this.CorrelationId);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TermsOfUseVersion", this.TermsOfUseVersion);
			jsonDeserializer.SetProperty("TermsOfUseAccepted", this.TermsOfUseAccepted);
			jsonDeserializer.SetProperty("SenderAccountId", this.SenderAccountId);
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.TermsOfUseAccepted = rehydrator.ReadBool();

			if(this.TermsOfUseAccepted) {
				this.TermsOfUseVersion = new ComponentVersion();
				this.TermsOfUseVersion.Rehydrate(rehydrator);
			}
			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Rehydrate(rehydrator);
			this.CorrelationId = (uint) data.Value;
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			bool termsOfUseAccepted = this.TermsOfUseAccepted && (this.TermsOfUseVersion?.IsVersionSet??false);
			dehydrator.Write(termsOfUseAccepted);

			if(termsOfUseAccepted) {
				this.TermsOfUseVersion.Dehydrate(dehydrator);
			}

			AdaptiveLong1_9 data = new AdaptiveLong1_9();
			data.Value = this.CorrelationId;
			data.Dehydrate(dehydrator);
		}
	}
}