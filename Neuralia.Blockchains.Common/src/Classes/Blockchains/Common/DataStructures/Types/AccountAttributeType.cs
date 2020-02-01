using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types {

	public class AccountAttributeType : SimpleUShort<AccountAttributeType> {

		public AccountAttributeType() {
		}

		public AccountAttributeType(ushort value) : base(value) {
		}

		public static implicit operator AccountAttributeType(ushort d) {
			return new AccountAttributeType(d);
		}

		public static bool operator ==(AccountAttributeType a, AccountAttributeType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(AccountAttributeType a, AccountAttributeType b) {
			return !(a == b);
		}
	}

	public class AccountAttributesTypes : UShortConstantSet<AccountAttributeType> {
		public readonly AccountAttributeType GATED_ACCOUNT;
		public readonly AccountAttributeType RESETABLE_ACCOUNT;
		
		public readonly AccountAttributeType THREE_WAY_GATED_TRANSFER;
		// account types


		static AccountAttributesTypes() {
		}

		protected AccountAttributesTypes() : base(10_000) {

			this.RESETABLE_ACCOUNT = this.CreateBaseConstant();
			this.GATED_ACCOUNT = this.CreateBaseConstant();
			this.THREE_WAY_GATED_TRANSFER = this.CreateBaseConstant();
		}

		public static AccountAttributesTypes Instance { get; } = new AccountAttributesTypes();
	}
}