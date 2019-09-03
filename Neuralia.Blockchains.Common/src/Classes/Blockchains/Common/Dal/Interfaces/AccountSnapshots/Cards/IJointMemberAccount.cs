namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {
	public interface IJointMemberAccount : ISnapshot {

		long AccountId { get; set; }

		bool Required { get; set; }
	}

}