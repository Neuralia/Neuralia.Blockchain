using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState {
	public interface IChainStateDal : IDalInterfaceBase {
	}

	public interface IChainStateDal<CHAIN_STATE_CONTEXT, MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateDal
		where CHAIN_STATE_CONTEXT : IChainStateContext
		where MODEL_SNAPSHOT : class, IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
		Func<MODEL_SNAPSHOT> CreateNewEntry { get; set; }

		void PerformOperation(Action<CHAIN_STATE_CONTEXT, LockContext> process, LockContext lockContext);
		T PerformOperation<T>(Func<CHAIN_STATE_CONTEXT, LockContext, T> process, LockContext lockContext);

		Task PerformOperationAsync(Func<CHAIN_STATE_CONTEXT, LockContext, Task> process, LockContext lockContext);
		Task<T> PerformOperationAsync<T>(Func<CHAIN_STATE_CONTEXT, LockContext, Task<T>> process, LockContext lockContext);

		Task<MODEL_SNAPSHOT> LoadFullState(CHAIN_STATE_CONTEXT db);
		Task<MODEL_SNAPSHOT> LoadSimpleState(CHAIN_STATE_CONTEXT db);
	}
}