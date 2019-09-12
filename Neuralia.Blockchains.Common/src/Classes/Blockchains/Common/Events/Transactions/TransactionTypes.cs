using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions {

	public class TransactionType : SimpleUShort<TransactionType> {

		public TransactionType() {
		}

		public TransactionType(ushort value) : base(value) {
		}

		public static implicit operator TransactionType(ushort d) {
			return new TransactionType(d);
		}

		public static bool operator ==(TransactionType a, TransactionType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(TransactionType a, TransactionType b) {
			return !(a == b);
		}
	}

	public class TransactionTypes : UShortConstantSet<TransactionType> {

		public readonly TransactionType ACCREDITATION_CERTIFICATE;
		public readonly TransactionType DEBUG;
		public readonly TransactionType GENESIS;
		public readonly TransactionType JOINT_PRESENTATION;
		public readonly TransactionType KEY_CHANGE;
		public readonly TransactionType MODERATION_ACCOUNT_RESET;
		public readonly TransactionType MODERATION_ACCOUNT_RESET_WARNING;
		public readonly TransactionType MODERATION_ELECTION_POOL_PRESENTATION;

		public readonly TransactionType MODERATION_KEY_CHANGE;
		public readonly TransactionType MODERATION_OPERATING_RULES;
		public readonly TransactionType MODERATION_PRESENTATION;
		public readonly TransactionType MODERATION_RECLAIM_ACCOUNTS;

		public readonly TransactionType NONE;
		public readonly TransactionType SET_ACCOUNT_RECOVERY;
		public readonly TransactionType SET_ACCOUNT_CORRELATION_ID;
		public readonly TransactionType SIMPLE_PRESENTATION;

		static TransactionTypes() {
		}

		protected TransactionTypes() : base(10_000) {

			this.GENESIS = this.CreateBaseConstant();
			this.SIMPLE_PRESENTATION = this.CreateBaseConstant();
			this.JOINT_PRESENTATION = this.CreateBaseConstant();
			this.KEY_CHANGE = this.CreateBaseConstant();
			this.ACCREDITATION_CERTIFICATE = this.CreateBaseConstant();
			this.SET_ACCOUNT_RECOVERY = this.CreateBaseConstant();
			this.SET_ACCOUNT_CORRELATION_ID = this.CreateBaseConstant();

			this.SetOffset(1_000);

			this.MODERATION_KEY_CHANGE = this.CreateBaseConstant();
			this.MODERATION_ACCOUNT_RESET = this.CreateBaseConstant();
			this.MODERATION_ACCOUNT_RESET_WARNING = this.CreateBaseConstant();
			this.MODERATION_RECLAIM_ACCOUNTS = this.CreateBaseConstant();
			this.MODERATION_PRESENTATION = this.CreateBaseConstant();
			this.MODERATION_OPERATING_RULES = this.CreateBaseConstant();
			this.MODERATION_ELECTION_POOL_PRESENTATION = this.CreateBaseConstant();

			this.DEBUG = this.CreateBaseConstant();
			this.NONE = this.CreateBaseConstant();
		}

		public static TransactionTypes Instance { get; } = new TransactionTypes();
	}
}