using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {

	public class AccountAttributeEntry : AccountAttribute, IAccountAttributeEntry {
		
		public long AccountId { get; set; }
	}
}