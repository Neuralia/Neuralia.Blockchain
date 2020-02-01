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
		public readonly DigestChannelType StandardAccountKeys;
		public readonly DigestChannelType StandardAccountSnapshot;
		
		public readonly DigestChannelType JointAccountSnapshot;
		
		public readonly DigestChannelType AccreditationCertificates;
		public readonly DigestChannelType ChainOptions;

		static DigestChannelTypes() {
		}

		private DigestChannelTypes() : base(1000) {

			this.StandardAccountSnapshot = this.CreateBaseConstant();
			this.StandardAccountKeys = this.CreateBaseConstant();
			this.JointAccountSnapshot = this.CreateBaseConstant();
			this.AccreditationCertificates = this.CreateBaseConstant();
			this.ChainOptions = this.CreateBaseConstant();
		}

		public static DigestChannelTypes Instance { get; } = new DigestChannelTypes();
	}
}