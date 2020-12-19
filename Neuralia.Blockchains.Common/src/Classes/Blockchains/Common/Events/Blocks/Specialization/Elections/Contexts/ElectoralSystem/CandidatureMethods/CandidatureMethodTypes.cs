using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods {

	public class CandidatureMethodType : SimpleUShort<CandidatureMethodType> {

		public CandidatureMethodType() {
		}

		public CandidatureMethodType(ushort value) : base(value) {
		}

		public static implicit operator CandidatureMethodType(ushort d) {
			return new CandidatureMethodType(d);
		}

		public static bool operator ==(CandidatureMethodType a, CandidatureMethodType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(CandidatureMethodType a, CandidatureMethodType b) {
			return !(a == b);
		}
	}

	public sealed class CandidatureMethodTypes : UShortConstantSet<CandidatureMethodType> {

		public readonly CandidatureMethodType SimpleHash;

		static CandidatureMethodTypes() {
		}

		private CandidatureMethodTypes() : base(1000) {
			this.SimpleHash = this.CreateBaseConstant();
		}

		public static CandidatureMethodTypes Instance { get; } = new CandidatureMethodTypes();
	}

}