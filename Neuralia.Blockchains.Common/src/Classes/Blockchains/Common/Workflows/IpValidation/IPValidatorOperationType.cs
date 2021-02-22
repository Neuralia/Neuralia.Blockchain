using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation {
	public class IPValidatorOperationType : SimpleUShort<IPValidatorOperationType> {

		public IPValidatorOperationType() {
		}

		public IPValidatorOperationType(byte value) : base(value) {
		}

		public static implicit operator IPValidatorOperationType(byte d) {
			return new IPValidatorOperationType(d);
		}

		public static bool operator ==(IPValidatorOperationType a, IPValidatorOperationType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(IPValidatorOperationType a, IPValidatorOperationType b) {
			return !(a == b);
		}
	}

	public sealed class IPValidatorOperationTypes : UShortConstantSet<IPValidatorOperationType> {
		public readonly IPValidatorOperationType Questions;
		

		static IPValidatorOperationTypes() {
		}

		private IPValidatorOperationTypes() : base((ushort) 100) {
			this.Questions = this.CreateBaseConstant();
		}

		public static IPValidatorOperationTypes Instance { get; } = new IPValidatorOperationTypes();
	}
}