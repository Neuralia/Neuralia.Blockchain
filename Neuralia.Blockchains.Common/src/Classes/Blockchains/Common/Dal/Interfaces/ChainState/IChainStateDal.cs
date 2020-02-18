using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState {
	public interface IChainStateDal : IDalInterfaceBase {
	}

	public interface IChainStateDal<CHAIN_STATE_CONTEXT, MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateDal
		where CHAIN_STATE_CONTEXT : IChainStateContext
		where MODEL_SNAPSHOT : class, IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
		Func<MODEL_SNAPSHOT> CreateNewEntry { get; set; }

		
		void PerformOperation(Action<CHAIN_STATE_CONTEXT> process);
		T PerformOperation<T>(Func<CHAIN_STATE_CONTEXT, T> process);
		
		Task PerformOperationAsync(Func<CHAIN_STATE_CONTEXT, Task> process);
		Task<T> PerformOperationAsync<T>(Func<CHAIN_STATE_CONTEXT, Task<T>> process);
		
		MODEL_SNAPSHOT LoadFullState(CHAIN_STATE_CONTEXT db);
		MODEL_SNAPSHOT LoadSimpleState(CHAIN_STATE_CONTEXT db);
	}
}