using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {
	public interface IAccountAttribute : ISnapshot {

		AccountAttributeType AttributeType { get; set; }
		uint CorrelationId { get; set; }

		byte[] Context { get; set; }

		/// <summary>
		///     the timestamp at which the feature begins
		/// </summary>
		DateTime? Start { get; set; }

		/// <summary>
		///     the timestamp at which the feature ends
		/// </summary>
		DateTime? Expiration { get; set; }
	}
}