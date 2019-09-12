using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IAccountSnapshot : ISnapshot, ITypedCollectionExposure<IAccountFeature> {
		long AccountId { get; set; }

		/// <summary>
		///     The block this account was presented and confirmed
		/// </summary>
		long InceptionBlockId { get; set; }

		/// <summary>
		///     The trust level we currently have on chain
		/// </summary>
		byte TrustLevel { get; set; }
		
		long? CorrelationId { get; set; }
	}

	public interface IAccountSnapshot<ACCOUNT_FEATURE> : IAccountSnapshot
		where ACCOUNT_FEATURE : IAccountFeature {

		List<ACCOUNT_FEATURE> AppliedFeatures { get; }
	}

}