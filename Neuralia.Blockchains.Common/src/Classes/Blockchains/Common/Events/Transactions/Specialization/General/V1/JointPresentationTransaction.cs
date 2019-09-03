using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json.Linq;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IJointPresentationTransaction : IPresentationTransaction, IPresentation {
		List<ITransactionJointAccountMember> MemberAccounts { get; }
		byte RequiredSignatureCount { get; set; }
	}

	/// <summary>
	///     declare a special joint account with multiple account signature required
	/// </summary>
	public abstract class JointPresentationTransaction : Transaction, IJointPresentationTransaction {

		public List<ITransactionJointAccountMember> MemberAccounts { get; } = new List<ITransactionJointAccountMember>();

		public AccountId AssignedAccountId { get; set; } = new AccountId();
		public long? CorrelationId { get; set; }
		public List<ITransactionAccountFeature> Features { get; } = new List<ITransactionAccountFeature>();

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

			jsonDeserializer.SetArray("Features", this.Features);
			jsonDeserializer.SetArray("MemberAccounts", this.MemberAccounts);

			//
			jsonDeserializer.SetProperty("PowNonce", this.PowNonce);
			jsonDeserializer.SetProperty("PowDifficulty", this.PowDifficulty);
			jsonDeserializer.SetArray("PowSolutions", this.PowSolutions.Select(s => new JValue(s)));
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.JOINT_PRESENTATION, 1, 0);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.AssignedAccountId.Rehydrate(rehydrator);
			this.CorrelationId = rehydrator.ReadNullableLong();
			this.RequiredSignatureCount = rehydrator.ReadByte();

			byte accountFeatureCount = rehydrator.ReadByte();

			this.Features.Clear();

			for(short i = 0; i < accountFeatureCount; i++) {
				ITransactionAccountFeature feature = this.CreateTransactionAccountFeature();

				feature.Rehydrate(rehydrator);

				this.Features.Add(feature);
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

			dehydrator.Write((byte) this.Features.Count);

			foreach(ITransactionAccountFeature feature in this.Features) {
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

		protected abstract ITransactionAccountFeature CreateTransactionAccountFeature();
		protected abstract ITransactionJointAccountMember CreateTransactionJointAccountMember();
	}
}