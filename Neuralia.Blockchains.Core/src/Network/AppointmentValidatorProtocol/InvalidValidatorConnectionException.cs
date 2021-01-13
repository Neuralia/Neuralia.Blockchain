using System;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public class InvalidValidatorConnectionException : ApplicationException {

		public bool HasConnected { get; private set; }

		public InvalidValidatorConnectionException() {

		}
		
		public InvalidValidatorConnectionException(bool hasConnected) {
			this.HasConnected = hasConnected;
		}

		public InvalidValidatorConnectionException(string message, bool hasConnected) : base(message) {
			this.HasConnected = hasConnected;
		}

		public InvalidValidatorConnectionException(string message, Exception innerException, bool hasConnected) : base(message, innerException) {
			this.HasConnected = hasConnected;
		}
	}
}