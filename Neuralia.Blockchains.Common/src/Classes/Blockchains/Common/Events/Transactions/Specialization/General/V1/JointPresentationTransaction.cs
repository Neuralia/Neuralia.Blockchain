using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IJointPresentationTransaction : IPresentationTransaction, IJointTransaction, IJointMembers {

		byte RequiredSignatureCount { get; set; }
	}

	/// <summary>
	///     declare a special joint account with multiple account signature required
	/// </summary>
	public abstract class JointPresentationTransaction : Transaction, IJointPresentationTransaction {

		public List<ITransactionJointAccountMember> MemberAccounts { get; } = new List<ITransactionJointAccountMember>();

		public AccountId AssignedAccountId { get; set; } = null;
		public long? CorrelationId { get; set; }
		public List<ITransactionAccountAttribute> Attributes { get; } = new List<ITransactionAccountAttribute>();

		public byte RequiredSignatureCount { get; set; }

		public int THSNonce { get; set; }
		public int THSSolution { get; set; }
		public ushort THSDifficulty { get; set; }

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray(types);
			
			nodeList.Add(this.CorrelationId);
			nodeList.Add(this.RequiredSignatureCount);

			nodeList.Add(this.MemberAccounts.OrderBy(a => a.AccountId));

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("AssignedAccountId", this.AssignedAccountId?.ToString());
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId ?? 0);
			jsonDeserializer.SetProperty("RequiredSignatureCount", this.RequiredSignatureCount);

			//

			jsonDeserializer.SetArray("Attributes", this.Attributes);
			jsonDeserializer.SetArray("MemberAccounts", this.MemberAccounts);

			//
			jsonDeserializer.SetProperty("THSNonce", this.THSNonce);
			jsonDeserializer.SetProperty("THSDifficulty", this.THSDifficulty);
			jsonDeserializer.SetProperty("THSSolution", this.THSSolution);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.Range;
		public override AccountId[] ImpactedAccounts => TargetAccountsAndSender();
		public override AccountId[] TargetAccounts => GetAccountIds(this.AssignedAccountId);
		
		
		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.JOINT_PRESENTATION, 1, 0);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			bool isNull = rehydrator.ReadBool();

			this.AssignedAccountId = null;
			if(!isNull) {
				this.AssignedAccountId = new AccountId();
				this.AssignedAccountId.Rehydrate(rehydrator);
			}
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

			this.THSNonce = rehydrator.ReadInt();
			this.THSDifficulty = rehydrator.ReadUShort();
			this.THSSolution = rehydrator.ReadInt();
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			dehydrator.Write(this.AssignedAccountId == null);

			if(this.AssignedAccountId != null) {
				this.AssignedAccountId.Dehydrate(dehydrator);
			}
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

			dehydrator.Write(this.THSNonce);
			dehydrator.Write(this.THSDifficulty);
			dehydrator.Write(this.THSSolution);
		}

		protected abstract ITransactionAccountAttribute CreateTransactionAccountFeature();
		protected abstract ITransactionJointAccountMember CreateTransactionJointAccountMember();
	}
}