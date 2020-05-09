using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets {

	public class RejectionCode : SimpleUShort<RejectionCode> {

		public RejectionCode() {
		}

		public RejectionCode(ushort value) : base(value) {
		}

		public static implicit operator RejectionCode(ushort d) {
			return new RejectionCode(d);
		}

		public static bool operator ==(RejectionCode a, RejectionCode b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(RejectionCode a, RejectionCode b) {
			return !(a == b);
		}
	}

	public class RejectionCodes : UShortConstantSet<RejectionCode> {

		public readonly RejectionCode ACCOUNT_EMPTY;
		public readonly RejectionCode ACCOUNT_TOO_YOUNG;
		public readonly RejectionCode CORRELATION_ID_ALREADY_SET;
		public readonly RejectionCode INVALID_ACCOUNT;

		public readonly RejectionCode INVALID_CERTIFICATE;
		public readonly RejectionCode INVALID_RECIPIENT;

		public readonly RejectionCode NONE;

		public readonly RejectionCode OTHER = 0;

		static RejectionCodes() {
		}

		protected RejectionCodes() : base(10_000) {

			this.NONE = this.CreateBaseConstant();
			this.ACCOUNT_TOO_YOUNG = this.CreateBaseConstant();
			this.ACCOUNT_EMPTY = this.CreateBaseConstant();
			this.INVALID_RECIPIENT = this.CreateBaseConstant();
			this.INVALID_ACCOUNT = this.CreateBaseConstant();
			this.CORRELATION_ID_ALREADY_SET = this.CreateBaseConstant();
			this.INVALID_CERTIFICATE = this.CreateBaseConstant();
		}

		public static RejectionCodes Instance { get; } = new RejectionCodes();
	}
}