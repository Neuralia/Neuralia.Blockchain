using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types {

	public class AccreditationCertificateType : SimpleUShort<AccreditationCertificateType> {

		public AccreditationCertificateType() {
		}

		public AccreditationCertificateType(ushort value) : base(value) {
		}

		public static implicit operator AccreditationCertificateType(ushort d) {
			return new AccreditationCertificateType(d);
		}

		public static bool operator ==(AccreditationCertificateType a, AccreditationCertificateType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(AccreditationCertificateType a, AccreditationCertificateType b) {
			return !(a == b);
		}
	}

	public class AccreditationCertificateTypes : UShortConstantSet<AccreditationCertificateType> {

		public readonly AccreditationCertificateType DELEGATE;
		public readonly AccreditationCertificateType MINING_CLUSTER;
		public readonly AccreditationCertificateType SDK_PROVIDER;
		public readonly AccreditationCertificateType THIRD_PARTY;
		public readonly AccreditationCertificateType LICENSE;
		
		public readonly AccreditationCertificateType GATED_VERIFIER;
		public readonly AccreditationCertificateType ESCROW;
		
		static AccreditationCertificateTypes() {
		}

		protected AccreditationCertificateTypes() : base(10_000) {

			this.THIRD_PARTY = this.CreateBaseConstant();
			this.DELEGATE = this.CreateBaseConstant();
			this.SDK_PROVIDER = this.CreateBaseConstant();
			this.MINING_CLUSTER = this.CreateBaseConstant();
			this.LICENSE = this.CreateBaseConstant();
			
			this.GATED_VERIFIER = this.CreateBaseConstant();
			this.ESCROW = this.CreateBaseConstant();
			
		}

		public static AccreditationCertificateTypes Instance { get; } = new AccreditationCertificateTypes();
	}
}