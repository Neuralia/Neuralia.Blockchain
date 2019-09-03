using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {

	public class AccountFeature : IAccountFeature {

		public ushort FeatureType { get; set; }
		public int? CertificateId { get; set; }
		public int? Options { get; set; }
		public byte[] Data { get; set; }
		public DateTime? Start { get; set; }
		public DateTime? End { get; set; }
	}
}