using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types {

	public class AccountFeatureType : SimpleUShort<AccountFeatureType> {

		public AccountFeatureType() {
		}

		public AccountFeatureType(ushort value) : base(value) {
		}

		public static implicit operator AccountFeatureType(ushort d) {
			return new AccountFeatureType(d);
		}

		public static bool operator ==(AccountFeatureType a, AccountFeatureType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(AccountFeatureType a, AccountFeatureType b) {
			return !(a == b);
		}
	}

	public class AccountFeatureTypes : UShortConstantSet<AccountFeatureType> {
		public readonly AccountFeatureType GATED_ACCOUNT;
		public readonly AccountFeatureType RESETABLE_ACCOUNT;

		// account types
		public readonly AccountFeatureType SAFU;

		static AccountFeatureTypes() {
		}

		protected AccountFeatureTypes() : base(10_000) {

			this.SAFU = this.CreateBaseConstant();
			this.RESETABLE_ACCOUNT = this.CreateBaseConstant();
			this.GATED_ACCOUNT = this.CreateBaseConstant();
		}

		public static AccountFeatureTypes Instance { get; } = new AccountFeatureTypes();
	}
}