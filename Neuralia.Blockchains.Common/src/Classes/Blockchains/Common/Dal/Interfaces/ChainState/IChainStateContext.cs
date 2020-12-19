using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState {

	public interface IChainStateContext : IContextInterfaceBase {
	}

	public interface IChainStateContext<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateContext
		where MODEL_SNAPSHOT : class, IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
	}
}