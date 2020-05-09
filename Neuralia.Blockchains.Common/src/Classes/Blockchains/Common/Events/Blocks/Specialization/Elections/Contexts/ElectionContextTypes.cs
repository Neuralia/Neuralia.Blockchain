using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts {
	public class ElectionContextType : SimpleUShort<ElectionContextType> {

		public ElectionContextType() {
		}

		public ElectionContextType(byte value) : base(value) {
		}

		public static implicit operator ElectionContextType(byte d) {
			return new ElectionContextType(d);
		}

		public static bool operator ==(ElectionContextType a, ElectionContextType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(ElectionContextType a, ElectionContextType b) {
			return !(a == b);
		}
	}

	public sealed class ElectionContextTypes : UShortConstantSet<ElectionContextType> {

		public readonly ElectionContextType Active;
		public readonly ElectionContextType Passive;

		static ElectionContextTypes() {
		}

		private ElectionContextTypes() : base((ushort) 100) {
			this.Active = this.CreateBaseConstant();
			this.Passive = this.CreateBaseConstant();
		}

		public static ElectionContextTypes Instance { get; } = new ElectionContextTypes();
	}

}