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

		public ValidationResult(ValidationResults result) : this(result, (List<IEventValidationErrorCodeBase>) null) {

		}

		public ValidationResult(ValidationResults result, IEventValidationErrorCodeBase errorCode) : this(result, new[] {errorCode}.ToList()) {

		}

		public ValidationResult(ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) {

			this.Result = result;

			if(errorCodes != null) {
				this.ErrorCodes = errorCodes.ToImmutableList();
			} else {
				this.ErrorCodes = Array.Empty<IEventValidationErrorCodeBase>().ToImmutableList();
			}
		}

		public string ErrorCodesJoined => string.Join(",", this.ErrorCodes.Select(e => $"{e}"));

		public ImmutableList<IEventValidationErrorCodeBase> ErrorCodes { get; }
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
			return $"{this.Result} - errors: [{this.ErrorCodesJoined}]";
		}
	}

	public class EventValidationErrorCode : EventValidationErrorCodeBase<EventValidationErrorCode> {

		public EventValidationErrorCode() {
		}

		public EventValidationErrorCode(ushort value) : base(value) {
		}

		public EventValidationErrorCode(ISimpleNumericValue<ushort> value) : base(value.Value) {
		}

		public override string ErrorPrefix => "VR";

		public static implicit operator EventValidationErrorCode(ushort d) {
			return new EventValidationErrorCode(d);
		}
	}

	public interface IEventValidationErrorCodeBase {
		string ErrorPrefix { get; }
		string ErrorName { get; set; }
		ushort Value { get; }
	}

	public abstract class EventValidationErrorCodeBase<T> : NamedSimpleUShort<T>, IEventValidationErrorCodeBase
		where T : class, ISimpleNumeric<T, ushort>, new() {

		public EventValidationErrorCodeBase() {
		}

		public EventValidationErrorCodeBase(ushort value) : base(value) {
		}

		public static bool operator ==(EventValidationErrorCodeBase<T> a, EventValidationErrorCodeBase<T> b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(EventValidationErrorCodeBase<T> a, EventValidationErrorCodeBase<T> b) {
			return !(a == b);
		}
	}

	public class EventValidationErrorCodes : EventValidationErrorCodes<EventValidationErrorCode> {
		public static EventValidationErrorCodes Instance { get; } = new EventValidationErrorCodes();
	}

	public class EventValidationErrorCodes<T> : UShortConstantSet<T>
		where T : EventValidationErrorCodeBase<T>, new() {
		
		public readonly EventValidationErrorCode INVALID;
		public readonly EventValidationErrorCode INVALID_IDENTITY_AUTOGRAPH;
		public readonly EventValidationErrorCode ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID;

		public readonly EventValidationErrorCode ENVELOPE_EXPIRED;
		public readonly EventValidationErrorCode FAILED_PUBLISHED_HASH_VALIDATION;
		public readonly EventValidationErrorCode HASH_INVALID;
		public readonly EventValidationErrorCode IMPOSSIBLE_BLOCK_DECLARATION_ID;
		public readonly EventValidationErrorCode INVALID_AUTOGRAPH;

		public readonly EventValidationErrorCode INVALID_BYTES;

		public readonly EventValidationErrorCode INVALID_ID;
		public readonly EventValidationErrorCode INVALID_JOINT_SIGNATURE_ACCOUNTS;
		public readonly EventValidationErrorCode INVALID_JOINT_SIGNATURE_COUNT;

		public readonly EventValidationErrorCode INVALID_KEY_TYPE;
		public readonly EventValidationErrorCode INVALID_REFERENCED_TRANSACTION;
		public readonly EventValidationErrorCode INVALID_TIMESTAMP;
		public readonly EventValidationErrorCode INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION;

		public readonly EventValidationErrorCode JOINT_TRANSACTION_SINGLE_SIGNATURE;
		public readonly EventValidationErrorCode KEY_NOT_YET_SYNCED;
		public readonly EventValidationErrorCode MISSING_REQUIRED_JOINT_ACCOUNT;

		public readonly EventValidationErrorCode MOBILE_CANNOT_VALIDATE;

		public readonly EventValidationErrorCode NOT_WITHIN_ACCEPTABLE_TIME_RANGE;
		public readonly EventValidationErrorCode SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE;
		public readonly EventValidationErrorCode SIGNATURE_VERIFICATION_FAILED;
		public readonly EventValidationErrorCode TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE;
		
		public readonly EventValidationErrorCode ACCOUNT_ID_TYPE_INVALID;
		public readonly EventValidationErrorCode KEY_FAILED_INDEX_LOCK;
		public readonly EventValidationErrorCode IDENTITY_AUTOGRAPHN_NOT_SET;
		
		public readonly EventValidationErrorCode MODERATION_TRANSACTION_NOT_ACCEPTED;


		protected EventValidationErrorCodes() : base(10_000) {
			this.CreateBaseConstant(ref this.INVALID, nameof(this.INVALID));
			this.CreateBaseConstant(ref this.ENVELOPE_EXPIRED, nameof(this.ENVELOPE_EXPIRED));
			this.CreateBaseConstant(ref this.NOT_WITHIN_ACCEPTABLE_TIME_RANGE, nameof(this.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));
			this.CreateBaseConstant(ref this.HASH_INVALID, nameof(this.HASH_INVALID));
			this.CreateBaseConstant(ref this.IMPOSSIBLE_BLOCK_DECLARATION_ID, nameof(this.IMPOSSIBLE_BLOCK_DECLARATION_ID));
			this.CreateBaseConstant(ref this.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID, nameof(this.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));
			this.CreateBaseConstant(ref this.SIGNATURE_VERIFICATION_FAILED, nameof(this.SIGNATURE_VERIFICATION_FAILED));
			this.CreateBaseConstant(ref this.TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE, nameof(this.TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE));
			this.CreateBaseConstant(ref this.FAILED_PUBLISHED_HASH_VALIDATION, nameof(this.FAILED_PUBLISHED_HASH_VALIDATION));
			this.CreateBaseConstant(ref this.INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION, nameof(this.INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION));

			this.CreateBaseConstant(ref this.INVALID_ID, nameof(this.INVALID_ID));
			this.CreateBaseConstant(ref this.INVALID_TIMESTAMP, nameof(this.INVALID_TIMESTAMP));
			this.CreateBaseConstant(ref this.INVALID_IDENTITY_AUTOGRAPH, nameof(this.INVALID_IDENTITY_AUTOGRAPH));
			

			this.CreateBaseConstant(ref this.INVALID_KEY_TYPE, nameof(this.INVALID_KEY_TYPE));
			this.CreateBaseConstant(ref this.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE, nameof(this.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE));
			this.CreateBaseConstant(ref this.MOBILE_CANNOT_VALIDATE, nameof(this.MOBILE_CANNOT_VALIDATE));
			this.CreateBaseConstant(ref this.KEY_NOT_YET_SYNCED, nameof(this.KEY_NOT_YET_SYNCED));
			this.CreateBaseConstant(ref this.INVALID_BYTES, nameof(this.INVALID_BYTES));

			this.CreateBaseConstant(ref this.JOINT_TRANSACTION_SINGLE_SIGNATURE, nameof(this.JOINT_TRANSACTION_SINGLE_SIGNATURE));
			this.CreateBaseConstant(ref this.INVALID_JOINT_SIGNATURE_COUNT, nameof(this.INVALID_JOINT_SIGNATURE_COUNT));
			this.CreateBaseConstant(ref this.INVALID_JOINT_SIGNATURE_ACCOUNTS, nameof(this.INVALID_JOINT_SIGNATURE_ACCOUNTS));
			this.CreateBaseConstant(ref this.INVALID_REFERENCED_TRANSACTION, nameof(this.INVALID_REFERENCED_TRANSACTION));
			this.CreateBaseConstant(ref this.MISSING_REQUIRED_JOINT_ACCOUNT, nameof(this.MISSING_REQUIRED_JOINT_ACCOUNT));

			this.CreateBaseConstant(ref this.ACCOUNT_ID_TYPE_INVALID, nameof(this.ACCOUNT_ID_TYPE_INVALID));
			this.CreateBaseConstant(ref this.KEY_FAILED_INDEX_LOCK, nameof(this.KEY_FAILED_INDEX_LOCK));
			this.CreateBaseConstant(ref this.INVALID_AUTOGRAPH, nameof(this.INVALID_AUTOGRAPH));
			this.CreateBaseConstant(ref this.IDENTITY_AUTOGRAPHN_NOT_SET, nameof(this.IDENTITY_AUTOGRAPHN_NOT_SET));
			
			this.CreateBaseConstant(ref this.MODERATION_TRANSACTION_NOT_ACCEPTED, nameof(this.MODERATION_TRANSACTION_NOT_ACCEPTED));
			
			
		}

		protected void CreateBaseConstant(ref T errorCode, string name, EventValidationErrorCode offset = default) {

			errorCode = this.CreateBaseConstant();
			errorCode.ErrorName = name;
		}

		protected void CreateBaseConstant(ref EventValidationErrorCode errorCode, string name, EventValidationErrorCode offset = default) {

			errorCode = new EventValidationErrorCode(this.CreateBaseConstant());
			errorCode.ErrorName = name;
		}
	}
}