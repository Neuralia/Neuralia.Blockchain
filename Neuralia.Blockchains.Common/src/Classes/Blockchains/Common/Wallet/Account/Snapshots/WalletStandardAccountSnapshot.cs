using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots {
	public interface IWalletStandardAccountSnapshot : IStandardAccountSnapshot, IWalletAccountSnapshot {
	}

	public interface IWalletStandardAccountSnapshot<ACCOUNT_ATTRIBUTE> : IStandardAccountSnapshot<ACCOUNT_ATTRIBUTE>, IWalletAccountSnapshot<ACCOUNT_ATTRIBUTE>, IWalletStandardAccountSnapshot
		where ACCOUNT_ATTRIBUTE : IAccountAttribute {
	}

	public abstract class WalletStandardAccountSnapshot<ACCOUNT_ATTRIBUTE> : WalletAccountSnapshot<ACCOUNT_ATTRIBUTE>, IWalletStandardAccountSnapshot<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : AccountAttribute, new() {
	}
}