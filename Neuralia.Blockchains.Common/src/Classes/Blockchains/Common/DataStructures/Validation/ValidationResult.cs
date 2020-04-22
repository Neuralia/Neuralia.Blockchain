using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {

	public class ValidationResult {
		[Flags]
		public enum ValidationResults : long {
			// base values
			Invalid = 0,
			Valid = 1,

			// valid values (1->32)
			EmbededKeyValid = Valid | (1L << 1),

			// invalid values (33->64)
			CantValidate = Invalid | (1L << (32 + 1))
		}

		public ValidationResult() : this(ValidationResults.Invalid) {

		}

		public ValidationResult(ValidationResults result) : this(result, (List<EventValidationErrorCode>) null) {

		}

		public ValidationResult(ValidationResults result, EventValidationErrorCode errorCode) : this(result, new[] {errorCode}.ToList()) {

		}

		public ValidationResult(ValidationResults result, List<EventValidationErrorCode> errorCodes) {

			this.Result = result;

			if(errorCodes != null) {
				this.ErrorCodes = errorCodes.ToImmutableList();
			} else {
				this.ErrorCodes = Array.Empty<EventValidationErrorCode>().ToImmutableList();
			}
		}

		public string ErrorCodesJoined => string.Join(",", this.ErrorCodes.Select(e => e.Value));
		
		public ImmutableList<EventValidationErrorCode> ErrorCodes { get; }
		public ValidationResults Result { get; set; }

		public bool Valid => this.Result.HasFlag(ValidationResults.Valid);
		public bool Invalid => !this.Valid;

		public virtual EventValidationException GenerateException() {
			return new EventValidationException(this);
		}

		public static bool operator ==(ValidationResult a, ValidationResults b) {
			if(ReferenceEquals(null, a)) {
				return false;
			}

			return a.Result == b;
		}

		public static bool operator !=(ValidationResult a, ValidationResults b) {
			return !(a == b);
		}

		public static bool operator ==(ValidationResult a, ValidationResult b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			if(ReferenceEquals(null, b)) {
				return false;
			}

			return a.Result == b.Result;
		}

		public static bool operator !=(ValidationResult a, ValidationResult b) {
			return !(a == b);
		}

		public override string ToString() {
			return this.Result.ToString();
		}
	}

	public class EventValidationErrorCode : SimpleUShort<EventValidationErrorCode> {

		public EventValidationErrorCode() {
		}

		public EventValidationErrorCode(ushort value) : base(value) {
		}

		public static implicit operator EventValidationErrorCode(ushort d) {
			return new EventValidationErrorCode(d);
		}

		public static bool operator ==(EventValidationErrorCode a, EventValidationErrorCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(EventValidationErrorCode a, EventValidationErrorCode b) {
			return !(a == b);
		}
	}

	public class EventValidationErrorCodes : UShortConstantSet<EventValidationErrorCode> {
		
		public readonly EventValidationErrorCode ENVELOPE_EXPIRED;
		public readonly EventValidationErrorCode ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID;
		public readonly EventValidationErrorCode FAILED_PUBLISHED_HASH_VALIDATION;
		public readonly EventValidationErrorCode HASH_INVALID;
		public readonly EventValidationErrorCode IMPOSSIBLE_BLOCK_DECLARATION_ID;

		public readonly EventValidationErrorCode INVALID_KEY_TYPE;
		public readonly EventValidationErrorCode KEY_NOT_YET_SYNCED;

		public readonly EventValidationErrorCode INVALID_BYTES;
		
		public readonly EventValidationErrorCode INVALID_ID;
		public readonly EventValidationErrorCode INVALID_TIMESTAMP;
		public readonly EventValidationErrorCode INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION;
		public readonly EventValidationErrorCode GOSSIP_PRESENTATION_TRANSACTIONS_NOT_ALLOWED;
		
		public readonly EventValidationErrorCode MOBILE_CANNOT_VALIDATE;

		public readonly EventValidationErrorCode NOT_WITHIN_ACCEPTABLE_TIME_RANGE;
		public readonly EventValidationErrorCode SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE;
		public readonly EventValidationErrorCode SIGNATURE_VERIFICATION_FAILED;
		public readonly EventValidationErrorCode TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE;
		
		public readonly EventValidationErrorCode JOINT_TRANSACTION_SINGLE_SIGNATURE;
		public readonly EventValidationErrorCode INVALID_JOINT_SIGNATURE_COUNT;
		public readonly EventValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNTS;
		public readonly EventValidationErrorCode INVALID_REFERENCED_TRANSACTION;
		public readonly EventValidationErrorCode MISSING_REQUIRED_JOINT_ACCOUNT;
		
		static EventValidationErrorCodes() {
		}

		protected EventValidationErrorCodes() : base(10_000) {

			this.ENVELOPE_EXPIRED = this.CreateBaseConstant();
			this.NOT_WITHIN_ACCEPTABLE_TIME_RANGE = this.CreateBaseConstant();
			this.HASH_INVALID = this.CreateBaseConstant();
			this.IMPOSSIBLE_BLOCK_DECLARATION_ID = this.CreateBaseConstant();
			this.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID = this.CreateBaseConstant();
			this.SIGNATURE_VERIFICATION_FAILED = this.CreateBaseConstant();
			this.TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE = this.CreateBaseConstant();
			this.FAILED_PUBLISHED_HASH_VALIDATION = this.CreateBaseConstant();
			this.INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION = this.CreateBaseConstant();
			this.GOSSIP_PRESENTATION_TRANSACTIONS_NOT_ALLOWED = this.CreateBaseConstant();
			
			this.INVALID_ID = this.CreateBaseConstant();
			this.INVALID_TIMESTAMP = this.CreateBaseConstant();

			this.INVALID_KEY_TYPE = this.CreateBaseConstant();
			this.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE = this.CreateBaseConstant();
			this.MOBILE_CANNOT_VALIDATE = this.CreateBaseConstant();
			this.KEY_NOT_YET_SYNCED = this.CreateBaseConstant();
			this.INVALID_BYTES = this.CreateBaseConstant();

			this.JOINT_TRANSACTION_SINGLE_SIGNATURE = this.CreateBaseConstant();
			this.INVALID_JOINT_SIGNATURE_COUNT = this.CreateBaseConstant();
			this.INVALID_JOINT_SIGNATURE_ACCOUNTS = this.CreateBaseConstant();
			this.INVALID_REFERENCED_TRANSACTION = this.CreateBaseConstant();
			this.MISSING_REQUIRED_JOINT_ACCOUNT = this.CreateBaseConstant();
		}

		public static EventValidationErrorCodes Instance { get; } = new EventValidationErrorCodes();
	}
}