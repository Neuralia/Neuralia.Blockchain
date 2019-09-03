using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class DigestValidationResult : ValidationResult {
		public DigestValidationResult(ValidationResults result) : base(result) {
		}

		public DigestValidationResult(ValidationResults result, EventValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public DigestValidationResult(ValidationResults result, List<EventValidationErrorCode> errorCodes) : base(result, errorCodes) {
		}

		public DigestValidationResult(ValidationResults result, DigestValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public DigestValidationResult(ValidationResults result, List<DigestValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<EventValidationErrorCode>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new DigestValidationException(this);
		}
	}

	public class DigestValidationErrorCodes : EventValidationErrorCodes {

		public readonly DigestValidationErrorCode FAILED_DIGEST_HASH_VALIDATION;
		public readonly DigestValidationErrorCode INVALID_CHANNEL_INDEX_HASH;
		public readonly DigestValidationErrorCode INVALID_DIGEST_CHANNEL_HASH;
		public readonly DigestValidationErrorCode INVALID_DIGEST_DESCRIPTOR_HASH;
		public readonly DigestValidationErrorCode INVALID_DIGEST_HASH;
		public readonly DigestValidationErrorCode INVALID_DIGEST_KEY;
		public readonly DigestValidationErrorCode INVALID_SLICE_HASH;

		static DigestValidationErrorCodes() {
		}

		public DigestValidationErrorCodes() {
			this.FAILED_DIGEST_HASH_VALIDATION = this.CreateChildConstant();
			this.INVALID_SLICE_HASH = this.CreateChildConstant();
			this.INVALID_DIGEST_DESCRIPTOR_HASH = this.CreateChildConstant();
			this.INVALID_CHANNEL_INDEX_HASH = this.CreateChildConstant();
			this.INVALID_DIGEST_CHANNEL_HASH = this.CreateChildConstant();
			this.INVALID_DIGEST_HASH = this.CreateChildConstant();
			this.INVALID_DIGEST_KEY = this.CreateChildConstant();

			//this.PrintValues(";");		
		}

		public static new DigestValidationErrorCodes Instance { get; } = new DigestValidationErrorCodes();

		public DigestValidationErrorCode CreateChildConstant(DigestValidationErrorCode offset = default) {
			return new DigestValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class DigestValidationErrorCode : EventValidationErrorCode {

		public DigestValidationErrorCode() {
		}

		public DigestValidationErrorCode(ushort value) : base(value) {
		}

		public static implicit operator DigestValidationErrorCode(ushort d) {
			return new DigestValidationErrorCode(d);
		}

		public static bool operator ==(DigestValidationErrorCode a, DigestValidationErrorCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(DigestValidationErrorCode a, DigestValidationErrorCode b) {
			return !(a == b);
		}
	}
}