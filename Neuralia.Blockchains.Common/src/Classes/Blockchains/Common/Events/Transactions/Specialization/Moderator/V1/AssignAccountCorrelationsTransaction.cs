using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {

	public interface IAssignAccountCorrelationsTransaction : IModerationTransaction {
		List<AccountId> EnableAccounts { get; }
		List<AccountId> DisableAccounts { get; }
	}

	public abstract class AssignAccountCorrelationsTransaction : ModerationTransaction, IAssignAccountCorrelationsTransaction {

		public List<AccountId> EnableAccounts { get; } = new List<AccountId>();
		public List<AccountId> DisableAccounts { get; } = new List<AccountId>();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.EnableAccounts.Count);
			nodeList.Add(this.EnableAccounts);

			nodeList.Add(this.DisableAccounts.Count);
			nodeList.Add(this.DisableAccounts);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("EnableAccounts", this.EnableAccounts);
			jsonDeserializer.SetArray("DisableAccounts", this.DisableAccounts);
		}

		public override ImmutableList<AccountId> TargetAccounts {
			get {
				List<AccountId> entries = this.EnableAccounts.ToList();
				entries.AddRange(this.DisableAccounts);

				return entries.ToImmutableList();
			}
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

			this.EnableAccounts.Clear();
			this.EnableAccounts.AddRange(AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters));

			this.DisableAccounts.Clear();
			this.DisableAccounts.AddRange(AccountIdGroupSerializer.Rehydrate(rehydrator, true, parameters));

		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			AccountIdGroupSerializer.AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId>();

			AccountIdGroupSerializer.Dehydrate(this.EnableAccounts, dehydrator, true, parameters);

			AccountIdGroupSerializer.Dehydrate(this.DisableAccounts, dehydrator, true, parameters);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.MODERATION_ASSIGN_ACCOUNT_CORRELATIONS, 1, 0);
		}
	}
}