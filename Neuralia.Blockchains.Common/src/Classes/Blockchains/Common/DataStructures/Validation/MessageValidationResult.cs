using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class MessageValidationResult : ValidationResult {
		public MessageValidationResult(ValidationResults result) : base(result) {
		}

		public MessageValidationResult(ValidationResults result, EventValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public MessageValidationResult(ValidationResults result, List<EventValidationErrorCode> errorCodes) : base(result, errorCodes) {
		}

		public MessageValidationResult(ValidationResults result, MessageValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public MessageValidationResult(ValidationResults result, List<MessageValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<EventValidationErrorCode>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new MessageValidationException(this);
		}
	}

	public class MessageValidationErrorCodes : EventValidationErrorCodes {

		public readonly MessageValidationErrorCode INVALID_MESSAGE_XMSS_KEY_BIT_SIZE;
		public readonly MessageValidationErrorCode INVALID_MESSAGE_XMSS_KEY_TYPE;

		static MessageValidationErrorCodes() {
		}

		protected MessageValidationErrorCodes() {
			this.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE = this.CreateChildConstant();
			this.INVALID_MESSAGE_XMSS_KEY_TYPE = this.CreateChildConstant();

			//this.PrintValues(";");		
		}

		public static new MessageValidationErrorCodes Instance { get; } = new MessageValidationErrorCodes();

		protected MessageValidationErrorCode CreateChildConstant(MessageValidationErrorCode offset = default) {
			return new MessageValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class MessageValidationErrorCode : EventValidationErrorCode {

		public MessageValidationErrorCode() {
		}

		public MessageValidationErrorCode(ushort value) : base(value) {
		}

		public static implicit operator MessageValidationErrorCode(ushort d) {
			return new MessageValidationErrorCode(d);
		}

		public static bool operator ==(MessageValidationErrorCode a, MessageValidationErrorCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(MessageValidationErrorCode a, MessageValidationErrorCode b) {
			return !(a == b);
		}
	}
}