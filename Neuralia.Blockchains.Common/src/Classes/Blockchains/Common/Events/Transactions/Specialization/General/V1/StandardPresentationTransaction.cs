using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;


namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {

	public interface IStandardPresentationTransaction : IPresentationTransaction, IKeyedTransaction {

		XmssCryptographicKey TransactionCryptographicKey { get; }
		XmssCryptographicKey MessageCryptographicKey { get; }
		XmssCryptographicKey ChangeCryptographicKey { get; }
		SecretCryptographicKey SuperCryptographicKey { get; }

		bool IsTransactionKeyLoaded { get; }
		bool IsMessageKeyLoaded { get; }
		bool IsChangeKeyLoaded { get; }
		bool IsSuperKeyLoaded { get; }
	}

	public abstract class StandardPresentationTransaction : KeyedTransaction, IStandardPresentationTransaction {

		public StandardPresentationTransaction() {
			// TransactionKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);

			// MessageKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MESSAGE_KEY_ORDINAL_ID);

			// change key
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.CHANGE_KEY_ORDINAL_ID);

			// Superkey
			this.Keyset.Add<SecretCryptographicKey>(GlobalsService.SUPER_KEY_ORDINAL_ID);
		}

		public List<ITransactionAccountFeature> Features { get; } = new List<ITransactionAccountFeature>();

		public int PowNonce { get; set; }
		public List<int> PowSolutions { get; set; } = new List<int>();
		public ushort PowDifficulty { get; set; }

		/// <summary>
		///     This is a VERY special field. This account ID is not hashed, and will be provided filled by the moderator to assign
		///     a final public accountId to this new Account
		/// </summary>
		public AccountId AssignedAccountId { get; set; } = new AccountId();

		public long? CorrelationId { get; set; }

		public XmssCryptographicKey TransactionCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.TRANSACTION_KEY_ORDINAL_ID];
		public XmssCryptographicKey MessageCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MESSAGE_KEY_ORDINAL_ID];
		public XmssCryptographicKey ChangeCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.CHANGE_KEY_ORDINAL_ID];
		public SecretCryptographicKey SuperCryptographicKey => (SecretCryptographicKey) this.Keyset.Keys[GlobalsService.SUPER_KEY_ORDINAL_ID];

		public bool IsTransactionKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.TRANSACTION_KEY_ORDINAL_ID);
		public bool IsMessageKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MESSAGE_KEY_ORDINAL_ID);
		public bool IsChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.CHANGE_KEY_ORDINAL_ID);
		public bool IsSuperKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.SUPER_KEY_ORDINAL_ID);

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.CorrelationId);

			//note: the POW results SHOULD NOT be hashed. neither should be the AssignedAccountId

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("AssignedAccountId", this.AssignedAccountId?.ToString());
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId ?? 0);

			jsonDeserializer.SetArray("Features", this.Features);

			//
			jsonDeserializer.SetProperty("PowNonce", this.PowNonce);
			jsonDeserializer.SetProperty("PowDifficulty", this.PowDifficulty);
			jsonDeserializer.SetProperty("PowSolutions", this.PowSolutions);
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (PRESENTATION: TransactionTypes.Instance.SIMPLE_PRESENTATION, 1, 0);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.AssignedAccountId.Rehydrate(rehydrator);
			this.CorrelationId = rehydrator.ReadNullableLong();

			this.Features.Clear();
			byte accountFeatureCount = rehydrator.ReadByte();

			for(short i = 0; i < accountFeatureCount; i++) {
				ITransactionAccountFeature feature = this.CreateTransactionAccountFeature();

				feature.Rehydrate(rehydrator);

				this.Features.Add(feature);
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

			dehydrator.Write((byte) this.Features.Count);

			foreach(ITransactionAccountFeature feature in this.Features) {
				feature.Dehydrate(dehydrator);
			}

			dehydrator.Write(this.PowNonce);
			dehydrator.Write(this.PowDifficulty);
			dehydrator.Write((byte) this.PowSolutions.Count);

			foreach(int solution in this.PowSolutions) {
				dehydrator.Write(solution);
			}
		}

		protected abstract ITransactionAccountFeature CreateTransactionAccountFeature();
	}
}