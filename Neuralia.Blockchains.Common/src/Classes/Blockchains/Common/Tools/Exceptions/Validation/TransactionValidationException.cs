using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation {

	public class TransactionValidationException : EventValidationException {

		public TransactionValidationException(ValidationResult results) : base(results) {
		}

		public TransactionValidationException(TransactionValidationResult results) : base(results) {
		}
	}

}