using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels {

	public class DigestChannelType : SimpleUShort<DigestChannelType> {

		public DigestChannelType() {
		}

		public DigestChannelType(ushort value) : base(value) {
		}

		public static implicit operator DigestChannelType(ushort d) {
			return new DigestChannelType(d);
		}

		public static bool operator ==(DigestChannelType a, DigestChannelType b) {
			return a.Value == b.Value;
		}

		public static bool operator !=(DigestChannelType a, DigestChannelType b) {
			return a.Value != b.Value;
		}
	}

	public sealed class DigestChannelTypes : UShortConstantSet<DigestChannelType> {

		public readonly DigestChannelType AccreditationCertificates;
		public readonly DigestChannelType ChainOptions;

		public readonly DigestChannelType JointAccountSnapshot;
		
		public readonly DigestChannelType UserAccountKeys;
		public readonly DigestChannelType UserAccountSnapshot;
		
		public readonly DigestChannelType ServerAccountKeys;
		public readonly DigestChannelType ServerAccountSnapshot;

		public readonly DigestChannelType ModeratorAccountKeys;
		public readonly DigestChannelType ModeratorAccountSnapshot;
		
		static DigestChannelTypes() {
		}

		private DigestChannelTypes() : base(1000) {

			this.UserAccountSnapshot = this.CreateBaseConstant();
			this.UserAccountKeys = this.CreateBaseConstant();
			this.ServerAccountSnapshot = this.CreateBaseConstant();
			this.ServerAccountKeys = this.CreateBaseConstant();
			this.JointAccountSnapshot = this.CreateBaseConstant();
			this.AccreditationCertificates = this.CreateBaseConstant();
			this.ChainOptions = this.CreateBaseConstant();
		}

		public static DigestChannelTypes Instance { get; } = new DigestChannelTypes();
	}
}