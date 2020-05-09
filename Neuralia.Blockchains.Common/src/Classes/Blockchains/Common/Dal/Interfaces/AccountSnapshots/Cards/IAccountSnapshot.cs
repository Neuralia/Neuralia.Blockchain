using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IAccountSnapshot : ISnapshot, ITypedCollectionExposure<IAccountAttribute> {
		long AccountId { get; set; }

		/// <summary>
		///     The block this account was presented and confirmed
		/// </summary>
		long InceptionBlockId { get; set; }

		/// <summary>
		///     The trust level we currently have on chain
		/// </summary>
		byte TrustLevel { get; set; }

		bool Correlated { get; set; }

		public ImmutableList<IAccountAttribute> AppliedAttributesBase { get; }
	}

	public interface IAccountSnapshot<ACCOUNT_ATTRIBUTE> : IAccountSnapshot
		where ACCOUNT_ATTRIBUTE : IAccountAttribute {

		List<ACCOUNT_ATTRIBUTE> AppliedAttributes { get; }
	}

}