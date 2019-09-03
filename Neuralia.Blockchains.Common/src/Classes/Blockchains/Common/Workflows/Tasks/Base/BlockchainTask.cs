using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base {
	public interface IBlockchainTask : IRoutedTask {
	}

	public interface IBlockchainTask<K> : IBlockchainTask, IRoutedTaskResult<K> {
	}

	public interface IBlockchainTask<out T, K> : IBlockchainTask<K>, IRoutedTask<T, K>
		where T : IRoutedTaskRoutingHandler {
	}

	public interface IBlockchainTask<out WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockchainTask<WALLET_MANAGER, K>
		where WALLET_MANAGER : IRoutedTaskRoutingHandler
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public class BlockchainTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : RoutedTask<WALLET_MANAGER, K>, IBlockchainTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WALLET_MANAGER : IRoutedTaskRoutingHandler
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public BlockchainTask() : base(Enums.BLOCKCHAIN_SERVICE) {

		}

		public BlockchainTask(Action<WALLET_MANAGER, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) : base(Enums.BLOCKCHAIN_SERVICE, newAction, newCompleted) {

		}
	}
}