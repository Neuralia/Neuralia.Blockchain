using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class BlockValidationResult : ValidationResult {
		public BlockValidationResult(ValidationResults result) : base(result) {
		}

		public BlockValidationResult(ValidationResults result, EventValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public BlockValidationResult(ValidationResults result, List<EventValidationErrorCode> errorCodes) : base(result, errorCodes) {
		}

		public BlockValidationResult(ValidationResults result, BlockValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public BlockValidationResult(ValidationResults result, List<BlockValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<EventValidationErrorCode>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new BlockValidationException(this);
		}
	}

	public class BlockValidationErrorCodes : EventValidationErrorCodes {
		public readonly BlockValidationErrorCode GENESIS_HASH_SET;
		public readonly BlockValidationErrorCode GENESIS_PTAH_HASH_VERIFICATION_FAILED;
		public readonly BlockValidationErrorCode INVALID_BLOCK_KEY_CORRELATION_TYPE;

		public readonly BlockValidationErrorCode INVALID_BLOCK_SIGNATURE_TYPE;
		public readonly BlockValidationErrorCode INVALID_DIGEST_KEY;

		public readonly BlockValidationErrorCode LAST_BLOCK_HEIGHT_INVALID;

		public readonly BlockValidationErrorCode SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED;

		static BlockValidationErrorCodes() {
		}

		public BlockValidationErrorCodes() {
			this.LAST_BLOCK_HEIGHT_INVALID = this.CreateChildConstant();
			this.INVALID_DIGEST_KEY = this.CreateChildConstant();
			this.GENESIS_HASH_SET = this.CreateChildConstant();
			this.GENESIS_PTAH_HASH_VERIFICATION_FAILED = this.CreateChildConstant();
			this.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED = this.CreateChildConstant();

			this.INVALID_BLOCK_SIGNATURE_TYPE = this.CreateChildConstant();
			this.INVALID_BLOCK_KEY_CORRELATION_TYPE = this.CreateChildConstant();

			//this.PrintValues(";");		
		}

		public static new BlockValidationErrorCodes Instance { get; } = new BlockValidationErrorCodes();

		public BlockValidationErrorCode CreateChildConstant(BlockValidationErrorCode offset = default) {
			return new BlockValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class BlockValidationErrorCode : EventValidationErrorCode {

		public BlockValidationErrorCode() {
		}

		public BlockValidationErrorCode(ushort value) : base(value) {
		}

		public static implicit operator BlockValidationErrorCode(ushort d) {
			return new BlockValidationErrorCode(d);
		}

		public static bool operator ==(BlockValidationErrorCode a, BlockValidationErrorCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(BlockValidationErrorCode a, BlockValidationErrorCode b) {
			return !(a == b);
		}
	}

}