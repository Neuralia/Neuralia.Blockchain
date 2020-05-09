using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class BlockValidationResult : ValidationResult {
		public BlockValidationResult(ValidationResults result) : base(result) {
		}

		public BlockValidationResult(ValidationResults result, IEventValidationErrorCodeBase errorCode) : base(result, errorCode) {
		}

		public BlockValidationResult(ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) : base(result, errorCodes) {
		}

		public BlockValidationResult(ValidationResults result, BlockValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public BlockValidationResult(ValidationResults result, List<BlockValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<IEventValidationErrorCodeBase>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new BlockValidationException(this);
		}
	}

	public class BlockValidationErrorCodes : EventValidationErrorCodes<BlockValidationErrorCode> {
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

			this.CreateBaseConstant(ref this.LAST_BLOCK_HEIGHT_INVALID, nameof(this.LAST_BLOCK_HEIGHT_INVALID));
			this.CreateBaseConstant(ref this.INVALID_DIGEST_KEY, nameof(this.INVALID_DIGEST_KEY));
			this.CreateBaseConstant(ref this.GENESIS_HASH_SET, nameof(this.GENESIS_HASH_SET));
			this.CreateBaseConstant(ref this.GENESIS_PTAH_HASH_VERIFICATION_FAILED, nameof(this.GENESIS_PTAH_HASH_VERIFICATION_FAILED));
			this.CreateBaseConstant(ref this.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED, nameof(this.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED));

			this.CreateBaseConstant(ref this.INVALID_BLOCK_SIGNATURE_TYPE, nameof(this.INVALID_BLOCK_SIGNATURE_TYPE));
			this.CreateBaseConstant(ref this.INVALID_BLOCK_KEY_CORRELATION_TYPE, nameof(this.INVALID_BLOCK_KEY_CORRELATION_TYPE));

			//this.PrintValues(";");		
		}

		public static BlockValidationErrorCodes Instance { get; } = new BlockValidationErrorCodes();

		public BlockValidationErrorCode CreateChildConstant(BlockValidationErrorCode offset = default) {
			return new BlockValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class BlockValidationErrorCode : EventValidationErrorCodeBase<BlockValidationErrorCode> {
		public BlockValidationErrorCode() {
		}

		public BlockValidationErrorCode(ushort value) : base(value) {
		}

		public override string ErrorPrefix => "BLK";

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