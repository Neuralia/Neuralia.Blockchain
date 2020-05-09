// using System;
// using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
// using Neuralia.Blockchains.Core;
// using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
//
// namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base {
// 	public interface IValidationTask : IRoutedTask {
// 	}
//
// 	public interface IValidationTask<K> : IValidationTask, IRoutedTaskResult<K> {
// 	}
//
// 	public interface IValidationTask<out T, K> : IValidationTask<K>, IRoutedTask<T, K>
// 		where T : IRoutedTaskRoutingHandler {
// 	}
//
// 	public interface IValidationTask<out WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IValidationTask<WALLET_MANAGER, K>
// 		where WALLET_MANAGER : IRoutedTaskRoutingHandler
// 		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
// 		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
// 	}
//
// 	public class ValidationTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : RoutedTask<WALLET_MANAGER, K>, IValidationTask<WALLET_MANAGER, K, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
// 		where WALLET_MANAGER : IRoutedTaskRoutingHandler
// 		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
// 		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
//
// 		public ValidationTask() : base(Enums.VALIDATION_SERVICE) {
//
// 		}
//
// 		public ValidationTask(Action<WALLET_MANAGER, TaskRoutingContext> newAction, Action<TaskExecutionResults, TaskRoutingContext> newCompleted) : base(Enums.VALIDATION_SERVICE, newAction, newCompleted) {
//
// 		}
// 	}
// }

