using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class TransactionValidationResult : ValidationResult {

		public TransactionValidationResult(ValidationResults result) : base(result) {
		}

		public TransactionValidationResult(ValidationResults result, IEventValidationErrorCodeBase errorCode) : base(result, errorCode) {
		}

		public TransactionValidationResult(ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) : base(result, errorCodes) {
		}

		public TransactionValidationResult(ValidationResults result, TransactionValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public TransactionValidationResult(ValidationResults result, List<TransactionValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<IEventValidationErrorCodeBase>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new TransactionValidationException(this);
		}
	}

	public class TransactionValidationErrorCodes : EventValidationErrorCodes<TransactionValidationErrorCode> {

		public readonly TransactionValidationErrorCode INVALID_CHANGE_XMSS_KEY_BIT_SIZE;
		public readonly TransactionValidationErrorCode INVALID_CHANGE_XMSS_KEY_TYPE;
		public readonly TransactionValidationErrorCode INVALID_JOINT_KEY_ACCOUNT;
		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE;

		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT;
		public readonly TransactionValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNTs;
		public readonly TransactionValidationErrorCode INVALID_POW_SOLUTION;
		
		public readonly TransactionValidationErrorCode INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION;
		public readonly TransactionValidationErrorCode INVALID_SUPERKEY_KEY_TYPE;
		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_KEY_TYPE;

		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE;
		public readonly TransactionValidationErrorCode INVALID_TRANSACTION_XMSS_KEY_TYPE;
		public readonly TransactionValidationErrorCode INVALID_KEY_TYPE;
		public readonly TransactionValidationErrorCode ONLY_ONE_TRANSACTION_PER_SCOPE;
		public readonly TransactionValidationErrorCode INVALID_PRESENTATION_ENVELOPE_SIGNATURE;
		public readonly TransactionValidationErrorCode USER_ACCOUNT_PRESENTATION_NO_APPOINTMENT_CODE;
		
		static TransactionValidationErrorCodes() {
		}

		protected TransactionValidationErrorCodes() {

			this.CreateBaseConstant(ref this.INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT, nameof(this.INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT));
			this.CreateBaseConstant(ref this.INVALID_JOINT_SIGNATURE_ACCOUNTs, nameof(this.INVALID_JOINT_SIGNATURE_ACCOUNTs));
			this.CreateBaseConstant(ref this.INVALID_JOINT_KEY_ACCOUNT, nameof(this.INVALID_JOINT_KEY_ACCOUNT));
			this.CreateBaseConstant(ref this.INVALID_JOINT_SIGNATURE, nameof(this.INVALID_JOINT_SIGNATURE));

			this.CreateBaseConstant(ref this.INVALID_POW_SOLUTION, nameof(this.INVALID_POW_SOLUTION));
			this.CreateBaseConstant(ref this.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION, nameof(this.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION));
			this.CreateBaseConstant(ref this.ONLY_ONE_TRANSACTION_PER_SCOPE, nameof(this.ONLY_ONE_TRANSACTION_PER_SCOPE));

			this.CreateBaseConstant(ref this.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE, nameof(this.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE));
			this.CreateBaseConstant(ref this.INVALID_TRANSACTION_XMSS_KEY_TYPE, nameof(this.INVALID_TRANSACTION_XMSS_KEY_TYPE));

			this.CreateBaseConstant(ref this.INVALID_CHANGE_XMSS_KEY_BIT_SIZE, nameof(this.INVALID_CHANGE_XMSS_KEY_BIT_SIZE));
			this.CreateBaseConstant(ref this.INVALID_CHANGE_XMSS_KEY_TYPE, nameof(this.INVALID_CHANGE_XMSS_KEY_TYPE));

			this.CreateBaseConstant(ref this.INVALID_SUPERKEY_KEY_TYPE, nameof(this.INVALID_SUPERKEY_KEY_TYPE));
			this.CreateBaseConstant(ref this.INVALID_TRANSACTION_KEY_TYPE, nameof(this.INVALID_TRANSACTION_KEY_TYPE));
			this.CreateBaseConstant(ref this.INVALID_PRESENTATION_ENVELOPE_SIGNATURE, nameof(this.INVALID_PRESENTATION_ENVELOPE_SIGNATURE));
			this.CreateBaseConstant(ref this.USER_ACCOUNT_PRESENTATION_NO_APPOINTMENT_CODE, nameof(this.USER_ACCOUNT_PRESENTATION_NO_APPOINTMENT_CODE));
			this.CreateBaseConstant(ref this.INVALID_KEY_TYPE, nameof(this.INVALID_KEY_TYPE));
			
			
			//this.PrintValues(";");		
		}

		public static TransactionValidationErrorCodes Instance { get; } = new TransactionValidationErrorCodes();

		protected TransactionValidationErrorCode CreateChildConstant(TransactionValidationErrorCode offset = default) {
			return new TransactionValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class TransactionValidationErrorCode : EventValidationErrorCodeBase<TransactionValidationErrorCode> {

		public TransactionValidationErrorCode() {
		}

		public TransactionValidationErrorCode(ushort value) : base(value) {
		}

		public override string ErrorPrefix => "TRX";

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