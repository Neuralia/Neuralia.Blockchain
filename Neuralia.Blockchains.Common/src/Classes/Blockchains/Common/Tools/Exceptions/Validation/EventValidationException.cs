using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation {
	public class EventValidationException : ApplicationException {

		public EventValidationException(ValidationResult results) {
			this.Result = results;
		}

		public ValidationResult Result { get; set; }

		protected virtual string EventName => "Event";

		public override string ToString() {
			return $"{EventName} Validation errors: " + this.Result.ErrorCodesJoined;
		}
	}

}