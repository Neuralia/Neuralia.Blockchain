using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {

	public interface IAccountFeatureEntry : IAccountFeature {

		long Id { get; set; }

		long AccountId { get; set; }
	}

}