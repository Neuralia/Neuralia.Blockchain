using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation {
	public class DigestValidationException : EventValidationException {

		public DigestValidationException(ValidationResult results) : base(results) {
		}

		public DigestValidationException(DigestValidationResult results) : base(results) {
		}
		
		protected override string EventName => "Digest";
	}
}