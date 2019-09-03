using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation {

	public class MessageValidationException : EventValidationException {

		public MessageValidationException(ValidationResult results) : base(results) {
		}

		public MessageValidationException(MessageValidationResult results) : base(results) {
		}
	}
}