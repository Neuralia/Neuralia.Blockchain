namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IStandardAccountSnapshot : IAccountSnapshot {
	}

	public interface IStandardAccountSnapshot<ACCOUNT_ATTRIBUTE> : IAccountSnapshot<ACCOUNT_ATTRIBUTE>, IStandardAccountSnapshot
		where ACCOUNT_ATTRIBUTE : IAccountAttribute {
	}

}