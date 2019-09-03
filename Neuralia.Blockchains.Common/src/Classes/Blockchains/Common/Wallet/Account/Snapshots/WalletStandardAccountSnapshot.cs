using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots {
	public interface IWalletStandardAccountSnapshot : IStandardAccountSnapshot, IWalletAccountSnapshot {
	}

	public interface IWalletStandardAccountSnapshot<ACCOUNT_FEATURE> : IStandardAccountSnapshot<ACCOUNT_FEATURE>, IWalletAccountSnapshot<ACCOUNT_FEATURE>, IWalletStandardAccountSnapshot
		where ACCOUNT_FEATURE : IAccountFeature {
	}

	public abstract class WalletStandardAccountSnapshot<ACCOUNT_FEATURE> : WalletAccountSnapshot<ACCOUNT_FEATURE>, IWalletStandardAccountSnapshot<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : AccountFeature, new() {
	}
}