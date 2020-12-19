using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots {
	public interface IJointMemberAccountEntry : IJointMemberAccount {
		long Id { get; set; }
		long ParentId { get; set; }
	}
}