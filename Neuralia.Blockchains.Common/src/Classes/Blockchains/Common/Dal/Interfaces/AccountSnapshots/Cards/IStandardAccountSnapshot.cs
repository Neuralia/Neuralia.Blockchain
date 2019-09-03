namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards {

	public interface IStandardAccountSnapshot : IAccountSnapshot {
	}

	public interface IStandardAccountSnapshot<ACCOUNT_FEATURE> : IAccountSnapshot<ACCOUNT_FEATURE>, IStandardAccountSnapshot
		where ACCOUNT_FEATURE : IAccountFeature {
	}

}