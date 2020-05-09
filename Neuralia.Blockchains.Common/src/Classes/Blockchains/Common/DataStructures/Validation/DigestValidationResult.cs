using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class DigestValidationResult : ValidationResult {
		public DigestValidationResult(ValidationResults result) : base(result) {
		}

		public DigestValidationResult(ValidationResults result, IEventValidationErrorCodeBase errorCode) : base(result, errorCode) {
		}

		public DigestValidationResult(ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) : base(result, errorCodes) {
		}

		public DigestValidationResult(ValidationResults result, DigestValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public DigestValidationResult(ValidationResults result, List<DigestValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<IEventValidationErrorCodeBase>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new DigestValidationException(this);
		}
	}

	public class DigestValidationErrorCodes : EventValidationErrorCodes<DigestValidationErrorCode> {

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

			this.CreateBaseConstant(ref this.FAILED_DIGEST_HASH_VALIDATION, nameof(this.FAILED_DIGEST_HASH_VALIDATION));
			this.CreateBaseConstant(ref this.INVALID_SLICE_HASH, nameof(this.INVALID_SLICE_HASH));
			this.CreateBaseConstant(ref this.INVALID_DIGEST_DESCRIPTOR_HASH, nameof(this.INVALID_DIGEST_DESCRIPTOR_HASH));
			this.CreateBaseConstant(ref this.INVALID_CHANNEL_INDEX_HASH, nameof(this.INVALID_CHANNEL_INDEX_HASH));
			this.CreateBaseConstant(ref this.INVALID_DIGEST_CHANNEL_HASH, nameof(this.INVALID_DIGEST_CHANNEL_HASH));
			this.CreateBaseConstant(ref this.INVALID_DIGEST_HASH, nameof(this.INVALID_DIGEST_HASH));
			this.CreateBaseConstant(ref this.INVALID_DIGEST_KEY, nameof(this.INVALID_DIGEST_KEY));

			//this.PrintValues(";");		
		}

		public static DigestValidationErrorCodes Instance { get; } = new DigestValidationErrorCodes();

		public DigestValidationErrorCode CreateChildConstant(DigestValidationErrorCode offset = default) {
			return new DigestValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class DigestValidationErrorCode : EventValidationErrorCodeBase<DigestValidationErrorCode> {
		public DigestValidationErrorCode() {
		}

		public DigestValidationErrorCode(ushort value) : base(value) {
		}

		public override string ErrorPrefix => "DIG";

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