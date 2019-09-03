using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {
	public interface IAccountFeature : ISnapshot {

		ushort FeatureType { get; set; }
		int? CertificateId { get; set; }

		int? Options { get; set; }

		byte[] Data { get; set; }

		/// <summary>
		///     the timestamp at which the feature begins
		/// </summary>
		DateTime? Start { get; set; }

		/// <summary>
		///     the timestamp at which the feature ends
		/// </summary>
		DateTime? End { get; set; }
	}
}