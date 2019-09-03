using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {

	public class AccountFeatureEntry : AccountFeature, IAccountFeatureEntry {

		public long Id { get; set; }
		public long AccountId { get; set; }
	}
}