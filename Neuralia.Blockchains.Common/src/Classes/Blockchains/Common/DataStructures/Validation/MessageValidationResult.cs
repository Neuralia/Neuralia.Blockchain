using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation {
	public class MessageValidationResult : ValidationResult {
		public MessageValidationResult(ValidationResults result) : base(result) {
		}

		public MessageValidationResult(ValidationResults result, IEventValidationErrorCodeBase errorCode) : base(result, errorCode) {
		}

		public MessageValidationResult(ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) : base(result, errorCodes) {
		}

		public MessageValidationResult(ValidationResults result, MessageValidationErrorCode errorCode) : base(result, errorCode) {
		}

		public MessageValidationResult(ValidationResults result, List<MessageValidationErrorCode> errorCodes) : base(result, errorCodes?.Cast<IEventValidationErrorCodeBase>().ToList()) {
		}

		public override EventValidationException GenerateException() {
			return new MessageValidationException(this);
		}
	}

	public class MessageValidationErrorCodes : EventValidationErrorCodes<MessageValidationErrorCode> {

		public readonly MessageValidationErrorCode INVALID_MESSAGE_XMSS_KEY_BIT_SIZE;
		public readonly MessageValidationErrorCode INVALID_MESSAGE_XMSS_KEY_TYPE;
		public readonly MessageValidationErrorCode INVALID_MODERATOR_MESSAGE_XMSS_KEY_TYPE;
		
		static MessageValidationErrorCodes() {
		}

		protected MessageValidationErrorCodes() {

			this.CreateBaseConstant(ref this.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE, nameof(this.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE));
			this.CreateBaseConstant(ref this.INVALID_MESSAGE_XMSS_KEY_TYPE, nameof(this.INVALID_MESSAGE_XMSS_KEY_TYPE));
			this.CreateBaseConstant(ref this.INVALID_MODERATOR_MESSAGE_XMSS_KEY_TYPE, nameof(this.INVALID_MESSAGE_XMSS_KEY_TYPE));

			//this.PrintValues(";");		
		}

		public static MessageValidationErrorCodes Instance { get; } = new MessageValidationErrorCodes();

		protected MessageValidationErrorCode CreateChildConstant(MessageValidationErrorCode offset = default) {
			return new MessageValidationErrorCode(base.CreateChildConstant(offset).Value);
		}
	}

	public class MessageValidationErrorCode : EventValidationErrorCodeBase<MessageValidationErrorCode> {
		public MessageValidationErrorCode() {
		}

		public MessageValidationErrorCode(ushort value) : base(value) {
		}

		public override string ErrorPrefix => "MSG";

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