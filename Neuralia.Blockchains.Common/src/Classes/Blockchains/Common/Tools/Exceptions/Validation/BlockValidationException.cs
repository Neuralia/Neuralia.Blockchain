using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation {

	public class BlockValidationException : EventValidationException {

		public BlockValidationException(ValidationResult results) : base(results) {
		}

		public BlockValidationException(BlockValidationResult results) : base(results) {
		}
	}
}