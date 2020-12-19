using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.PrimariesBallotingMethods {

	public class PrimariesBallotingMethodType : SimpleUShort<PrimariesBallotingMethodType> {

		public PrimariesBallotingMethodType() {
		}

		public PrimariesBallotingMethodType(ushort value) : base(value) {
		}

		public static implicit operator PrimariesBallotingMethodType(ushort d) {
			return new PrimariesBallotingMethodType(d);
		}

		public static bool operator ==(PrimariesBallotingMethodType a, PrimariesBallotingMethodType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(PrimariesBallotingMethodType a, PrimariesBallotingMethodType b) {
			return !(a == b);
		}
	}

	public sealed class PrimariesBallotingMethodTypes : UShortConstantSet<PrimariesBallotingMethodType> {

		public readonly PrimariesBallotingMethodType HashTarget;

		static PrimariesBallotingMethodTypes() {
		}

		private PrimariesBallotingMethodTypes() : base(1000) {
			this.HashTarget = this.CreateBaseConstant();
		}

		public static PrimariesBallotingMethodTypes Instance { get; } = new PrimariesBallotingMethodTypes();
	}
}