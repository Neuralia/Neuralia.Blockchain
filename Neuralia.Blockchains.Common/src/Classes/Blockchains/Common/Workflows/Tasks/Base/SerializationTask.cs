using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base {
	public interface ISerializationTask : IRoutedTask {
	}

	public interface ISerializationTask<K> : ISerializationTask, IRoutedTaskResult<K> {
	}

	public interface ISerializationTask<out T, K> : ISerializationTask<K>, IRoutedTask<T, K>
		where T : IRoutedTaskRoutingHandler {
	}

	public interface ISerializationTask<out WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ISerializationTask<WALLET_MANAGER, K>
		where WALLET_MANAGER : IRoutedTaskRoutingHandler
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public class SerializationTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : RoutedTask<WALLET_MANAGER, K>, ISerializationTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WALLET_MANAGER : IRoutedTaskRoutingHandler
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public SerializationTask() : base(Enums.SERIALIZATION_SERVICE) {

		}

		public SerializationTask(Action<WALLET_MANAGER, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) : base(Enums.SERIALIZATION_SERVICE, newAction, newCompleted) {

		}
	}
}