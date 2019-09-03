using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Passive {

	public class PassiveRepresentativeBallotingMethodType : SimpleUShort<PassiveRepresentativeBallotingMethodType> {

		public PassiveRepresentativeBallotingMethodType() {
		}

		public PassiveRepresentativeBallotingMethodType(ushort value) : base(value) {
		}

		public static implicit operator PassiveRepresentativeBallotingMethodType(ushort d) {
			return new PassiveRepresentativeBallotingMethodType(d);
		}

		public static bool operator ==(PassiveRepresentativeBallotingMethodType a, PassiveRepresentativeBallotingMethodType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(PassiveRepresentativeBallotingMethodType a, PassiveRepresentativeBallotingMethodType b) {
			return !(a == b);
		}
	}

	public sealed class PassiveRepresentativeBallotingMethodTypes : UShortConstantSet<PassiveRepresentativeBallotingMethodType> {

		public readonly PassiveRepresentativeBallotingMethodType TopLowestHashes;

		static PassiveRepresentativeBallotingMethodTypes() {
		}

		private PassiveRepresentativeBallotingMethodTypes() : base(1000) {
			this.TopLowestHashes = this.CreateBaseConstant();
		}

		public static PassiveRepresentativeBallotingMethodTypes Instance { get; } = new PassiveRepresentativeBallotingMethodTypes();
	}
}