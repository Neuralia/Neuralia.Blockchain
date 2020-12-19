using System.Collections.Immutable;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {

	public interface IGenesisAccountPresentationTransaction : IModerationKeyedTransaction {

		AccountId AssignedAccountId { get; set; }
	}

	public abstract class GenesisAccountPresentationTransaction : ModerationKeyedTransaction, IGenesisAccountPresentationTransaction {

		/// <summary>
		///     This is a VERY special field. This account ID is not hashed, and will be provided filled by the moderator to assign
		///     a final public accountId to this new Account
		/// </summary>
		public AccountId AssignedAccountId { get; set; } = new AccountId();

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.AssignedAccountId.GetStructuresArray());

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("AssignedAccountId", this.AssignedAccountId);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.All;
		public override AccountId[] ImpactedAccounts =>this.TargetAccounts;
		public override AccountId[] TargetAccounts => new[] {this.AssignedAccountId};
		

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.AssignedAccountId.Rehydrate(rehydrator);
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.AssignedAccountId.Dehydrate(dehydrator);
		}
	}
}