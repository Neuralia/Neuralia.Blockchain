using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class TransactionValidationResult : ValidationResult {

		public TransactionValidationResult(ValidationResults result) : base(result) {
		}

		public TransactionValidationResult(ValidationResults result, EventValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public TransactionValidationResult(ValidationResults result, List<EventValidationErrorCode> errorCodes) : base(result, errorCodes) {
		}

		public TransactionValidationResult(ValidationResults result, TransactionValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public TransactionValidationResult(ValidationResults result, List<TransactionValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<EventValidationErrorCode>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new TransactionValidationException(this);
		}
	}

	public class TransactionValidationErrorCodes : EventValidationErrorCodes {

		public readonly TransactionValidationErrorCode INVALID_CHANGE_XMSS_KEY_BIT_SIZE;
		public readonly TransactionValidationErrorCode INVALID_CHANGE_XMSS_KEY_TYPE;
		public readonly TransactionValidationErrorCode INVALID_JOINT_KEY_ACCOUNT;
		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE;

		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT;
		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNTs;
		public readonly TransactionValidationErrorCode INVALID_POW_SOLUTION;

		public readonly TransactionValidationErrorCode INVALID_POW_SOLUTIONS_COUNT;

		public readonly TransactionValidationErrorCode INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION;
		public readonly TransactionValidationErrorCode INVALID_SUPERKEY_KEY_TYPE;
		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_KEY_TYPE;

		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE;
		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_XMSS_KEY_TYPE;
		public readonly TransactionValidationErrorCode ONLY_ONE_TRANSACTION_PER_SCOPE;

		static TransactionValidationErrorCodes() {
		}

		protected TransactionValidationErrorCodes() {
			this.INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT = this.CreateChildConstant();
			this.INVALID_JOINT_SIGNATURE_ACCOUNTs = this.CreateChildConstant();
			this.INVALID_JOINT_KEY_ACCOUNT = this.CreateChildConstant();
			this.INVALID_JOINT_SIGNATURE = this.CreateChildConstant();

			this.INVALID_POW_SOLUTIONS_COUNT = this.CreateChildConstant();
			this.INVALID_POW_SOLUTION = this.CreateChildConstant();
			this.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION = this.CreateChildConstant();
			this.ONLY_ONE_TRANSACTION_PER_SCOPE = this.CreateChildConstant();

			this.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE = this.CreateChildConstant();
			this.INVALID_TRANSACTION_XMSS_KEY_TYPE = this.CreateChildConstant();

			this.INVALID_CHANGE_XMSS_KEY_BIT_SIZE = this.CreateChildConstant();
			this.INVALID_CHANGE_XMSS_KEY_TYPE = this.CreateChildConstant();

			this.INVALID_SUPERKEY_KEY_TYPE = this.CreateChildConstant();
			this.INVALID_TRANSACTION_KEY_TYPE = this.CreateChildConstant();

			//this.PrintValues(";");		
		}

		public static new TransactionValidationErrorCodes Instance { get; } = new TransactionValidationErrorCodes();

		protected TransactionValidationErrorCode CreateChildConstant(TransactionValidationErrorCode offset = default) {
			return new TransactionValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class TransactionValidationErrorCode : EventValidationErrorCode {

		public TransactionValidationErrorCode() {
		}

		public TransactionValidationErrorCode(ushort value) : base(value) {
		}

		public static implicit operator TransactionValidationErrorCode(ushort d) {
			return new TransactionValidationErrorCode(d);
		}

		public static bool operator ==(TransactionValidationErrorCode a, TransactionValidationErrorCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(TransactionValidationErrorCode a, TransactionValidationErrorCode b) {
			return !(a == b);
		}
	}

}