using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Implementations {
	public class JointMemberAccountEntry : JointMemberAccount, IJointMemberAccountEntry {

		public long Id { get; set; }
		public long ParentId { get; set; }
	}
}