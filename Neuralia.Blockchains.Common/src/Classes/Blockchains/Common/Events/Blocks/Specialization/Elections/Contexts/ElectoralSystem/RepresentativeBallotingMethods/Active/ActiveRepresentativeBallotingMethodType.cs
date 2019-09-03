using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active {

	public class ActiveRepresentativeBallotingMethodType : SimpleUShort<ActiveRepresentativeBallotingMethodType> {

		public ActiveRepresentativeBallotingMethodType() {
		}

		public ActiveRepresentativeBallotingMethodType(ushort value) : base(value) {
		}

		public static implicit operator ActiveRepresentativeBallotingMethodType(ushort d) {
			return new ActiveRepresentativeBallotingMethodType(d);
		}

		public static bool operator ==(ActiveRepresentativeBallotingMethodType a, ActiveRepresentativeBallotingMethodType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(ActiveRepresentativeBallotingMethodType a, ActiveRepresentativeBallotingMethodType b) {
			return !(a == b);
		}
	}

	public sealed class ActiveRepresentativeBallotingMethodTypes : UShortConstantSet<ActiveRepresentativeBallotingMethodType> {

		public readonly ActiveRepresentativeBallotingMethodType EncryptedSecret;

		static ActiveRepresentativeBallotingMethodTypes() {
		}

		private ActiveRepresentativeBallotingMethodTypes() : base(1000) {
			this.EncryptedSecret = this.CreateBaseConstant();
		}

		public static ActiveRepresentativeBallotingMethodTypes Instance { get; } = new ActiveRepresentativeBallotingMethodTypes();
	}
}