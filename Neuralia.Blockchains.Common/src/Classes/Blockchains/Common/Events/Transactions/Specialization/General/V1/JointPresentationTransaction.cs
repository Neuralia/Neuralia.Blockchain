using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IJointPresentationTransaction : IPresentationTransaction, IPresentation, IJointTransaction, IJointMembers {

		byte RequiredSignatureCount { get; set; }
	}

	/// <summary>
	///     declare a special joint account with multiple account signature required
	/// </summary>
	public abstract class JointPresentationTransaction : Transaction, IJointPresentationTransaction {

		public List<ITransactionJointAccountMember> MemberAccounts { get; } = new List<ITransactionJointAccountMember>();

		public AccountId AssignedAccountId { get; set; } = new AccountId();
		public long? CorrelationId { get; set; }
		public List<ITransactionAccountAttribute> Attributes { get; } = new List<ITransactionAccountAttribute>();

		public byte RequiredSignatureCount { get; set; }

		public int PowNonce { get; set; }
		public List<int> PowSolutions { get; set; } = new List<int>();
		public ushort PowDifficulty { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.AssignedAccountId);
			nodeList.Add(this.CorrelationId);
			nodeList.Add(this.RequiredSignatureCount);

			nodeList.Add(this.MemberAccounts.OrderBy(a => a.AccountId));

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("AssignedAccountId", this.AssignedAccountId);
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId ?? 0);
			jsonDeserializer.SetProperty("RequiredSignatureCount", this.RequiredSignatureCount);

			//

			jsonDeserializer.SetArray("Attributes", this.Attributes);
			jsonDeserializer.SetArray("MemberAccounts", this.MemberAccounts);

			//
			jsonDeserializer.SetProperty("PowNonce", this.PowNonce);
			jsonDeserializer.SetProperty("PowDifficulty", this.PowDifficulty);
			jsonDeserializer.SetArray("PowSolutions", this.PowSolutions);
		}

		public override ImmutableList<AccountId> TargetAccounts => this.MemberAccounts.Select(e => e.AccountId.ToAccountId()).ToImmutableList();

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.JOINT_PRESENTATION, 1, 0);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.AssignedAccountId.Rehydrate(rehydrator);
			this.CorrelationId = rehydrator.ReadNullableLong();
			this.RequiredSignatureCount = rehydrator.ReadByte();

			byte accountAttributeCount = rehydrator.ReadByte();

			this.Attributes.Clear();

			for(short i = 0; i < accountAttributeCount; i++) {
				ITransactionAccountAttribute attribute = this.CreateTransactionAccountFeature();

				attribute.Rehydrate(rehydrator);

				this.Attributes.Add(attribute);
			}

			byte memberAccountCount = rehydrator.ReadByte();

			this.MemberAccounts.Clear();

			for(short i = 0; i < memberAccountCount; i++) {
				ITransactionJointAccountMember memberAccount = this.CreateTransactionJointAccountMember();
				memberAccount.Rehydrate(rehydrator);
				this.MemberAccounts.Add(memberAccount);
			}

			this.PowNonce = rehydrator.ReadInt();
			this.PowDifficulty = rehydrator.ReadUShort();
			byte solutionsCount = rehydrator.ReadByte();

			for(short i = 0; i < solutionsCount; i++) {
				this.PowSolutions.Add(rehydrator.ReadInt());
			}
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.AssignedAccountId.Dehydrate(dehydrator);
			dehydrator.Write(this.CorrelationId);
			dehydrator.Write(this.RequiredSignatureCount);

			dehydrator.Write((byte) this.Attributes.Count);

			foreach(ITransactionAccountAttribute feature in this.Attributes) {
				feature.Dehydrate(dehydrator);
			}

			dehydrator.Write((byte) this.MemberAccounts.Count);

			foreach(ITransactionJointAccountMember memberAccount in this.MemberAccounts) {
				memberAccount.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.PowNonce);
			dehydrator.Write(this.PowDifficulty);
			dehydrator.Write((byte) this.PowSolutions.Count);

			foreach(int solution in this.PowSolutions) {
				dehydrator.Write(solution);
			}
		}

		protected abstract ITransactionAccountAttribute CreateTransactionAccountFeature();
		protected abstract ITransactionJointAccountMember CreateTransactionJointAccountMember();
	}
}