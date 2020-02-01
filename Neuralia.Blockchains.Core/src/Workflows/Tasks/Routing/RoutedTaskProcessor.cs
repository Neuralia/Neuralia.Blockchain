using System;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Routing {
	public static class RoutedTaskProcessor {

		public static void ProcessTask(InternalRoutedTask task, IRoutedTaskRoutingHandler currentService) {

			if((task.RoutingStatus == RoutedTask.RoutingStatuses.New) || (task.RoutingStatus == RoutedTask.RoutingStatuses.Disposed)) {
				return;
			}

			Action<Exception> exceptionHandling = null;
			Action<InternalRoutedTask> completedHandling = null;

			if(task.RoutingStatus == RoutedTask.RoutingStatuses.Dispatched) {
				exceptionHandling = exdi => {
					//TODO: what to do here?
					// an exception occured, we stop everything and move back to the parent
					task.ResetChildContext();
					task.TaskExecutionResults.Exception = exdi;
				};

				completedHandling = ReturnToCaller;

			} else if(task.RoutingStatus == RoutedTask.RoutingStatuses.Returned) {
				exceptionHandling = ex => {
					//TODO: what to do here?
					// an exception occured, we stop everything and move back to the parent
					task.ResetChildContext();

					if(task.ParentContext != null) {
						task.ParentContext.RaiseException(ex);
					} else {
						task.TaskExecutionResults.Exception = ex;
					}
				};

				completedHandling = currentTask => {

					// this task is done
					currentTask.RoutingStatus = RoutedTask.RoutingStatuses.Disposed;

					// inform anybody listening
					currentTask.TriggerOnCompleted();

					// next in line
					if(currentTask.TaskExecutionResults.Success) {
						currentTask.ParentContext?.ProcessNextTask(currentService);
					} else {
						// error occured
						if(task.ParentContext != null) {
							currentTask.ParentContext?.RaiseException(currentTask.TaskExecutionResults.Exception);
						} else {
							currentTask.TaskExecutionResults.ExceptionDispatchInfo.Throw();
						}

					}
				};
			}

			PerformTaskStateMachine(task, currentService, exceptionHandling, completedHandling);
		}

		private static void PerformTaskStateMachine(InternalRoutedTask task, IRoutedTaskRoutingHandler currentService, Action<Exception> exceptionHandling, Action<InternalRoutedTask> completedHandling) {

			if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenCompleted) {
				task.ExecutionStatus = RoutedTask.ExecutionStatuses.New;
				task.ResetChildContext();
			}

			if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.New) {

				Action run = null;

				if(task.RoutingStatus == RoutedTask.RoutingStatuses.Dispatched) {

					run = () => {
						try {
							// if we are inside a wallet transaction and we were sent here by the owner of the thread, then we want to pass on the thread access to this child thread.
							if(currentService.TaskRouter.IsWalletProviderTransaction(task)) {
								// ok, we are in a wallet transaction. we must pass on the active thread Id to this one
								//TODO: what to do here?
								throw new ApplicationException();
							} else {
								// this is a regular call
								task.TriggerBaseAction(currentService);
							}

						} catch(NotReadyForProcessingException nrex) {
							throw;
						} catch(Exception ex) {

							exceptionHandling(ex);

						}
					};
				} else if(task.RoutingStatus == RoutedTask.RoutingStatuses.Returned) {
					run = () => {
						try {
							task.TriggerDispatchReturned();

							if(task.TaskExecutionResults.Error && (task.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
								// there will be an error
							} else {
								// we are done with it
								task.TaskExecutionResults.Reset();
							}

						} catch(Exception ex) {
							exceptionHandling(ex);
						}

					};
				}

				// run this dispatched event
				run?.Invoke();

				if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenDispatched) {
					// the task has been stashed, go no further for now
					return;
				}

				task.ExecutionStatus = RoutedTask.ExecutionStatuses.Executed;

			}

			if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.Executed) {

				// dispatch any children that may need ot be dispatched
				if(task.HasChildren && task.TaskExecutionResults.Success) {
					// ok, lets route the children. it may have already been dispatched by an async call. if so, we just do nothing here

					task.ExecutionStatus = RoutedTask.ExecutionStatuses.ChildrenDispatched;

					task.ChildrenContext.ProcessNextTask(currentService);

					if((task.Mode == RoutedTask.ExecutionMode.Async) && !task.ChildrenContext.IsRunning) {
						throw new ApplicationException("The context is not running but we have a children dispatched status");
					}
				} else {

					// no children, so we return
					completedHandling(task);

				}

			}

		}

		/// <summary>
		///     Return the task to it's caller if it has one
		/// </summary>
		public static void ReturnToCaller(InternalRoutedTask task) {
			if((task.RoutingStatus == RoutedTask.RoutingStatuses.Dispatched) && ((task.ExecutionStatus == RoutedTask.ExecutionStatuses.Executed) || (task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenCompleted))) {

				if((task.Caller == null) && (task.ParentContext != null)) {
					throw new ApplicationException("A task in the routing chain can not have a null caller source. It must be set.");
				}

				task.RoutingStatus = RoutedTask.RoutingStatuses.Returned;
				task.ExecutionStatus = RoutedTask.ExecutionStatuses.New;
				task.ResetChildContext();

				// send it back to the parent
				if(task.Mode == RoutedTask.ExecutionMode.Async) {
					task.Caller?.ReceiveTask(task);
				} else if(task.Mode == RoutedTask.ExecutionMode.Sync) {
					task.Caller?.ReceiveTaskSynchronous(task);
				} else {
					throw new ApplicationException("Invalid task mode");
				}

				// if the caller is null, then its the end of the chain. if we have an exception and it must be rethrown, then we do it here
				if(task.Caller == null) {
					if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenCompleted) {
						if(task.ChildrenContext.TaskExecutionResults.Error && (task.ChildrenContext.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
							task.ChildrenContext.TaskExecutionResults.ExceptionDispatchInfo.Throw();
						}
					}

					if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.New) {
						if(task.TaskExecutionResults.Error && (task.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
							task.TaskExecutionResults.ExceptionDispatchInfo.Throw();
						}
					}
				}
			} else if(task.RoutingStatus == RoutedTask.RoutingStatuses.Returned) {

				if((task.ExecutionStatus == RoutedTask.ExecutionStatuses.Executed) || (task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenCompleted)) {
					// end of the road
					task.RoutingStatus = RoutedTask.RoutingStatuses.Disposed;

					if(task.Mode == RoutedTask.ExecutionMode.Async) {
						task.Caller?.ReceiveTask(task);
					} else if(task.Mode == RoutedTask.ExecutionMode.Sync) {
						task.Caller?.ReceiveTaskSynchronous(task);
					} else {
						throw new ApplicationException("Invalid task mode");
					}

					if(task.Caller == null) {
						if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.ChildrenCompleted) {
							if(task.ChildrenContext.TaskExecutionResults.Error && (task.ChildrenContext.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
								task.ChildrenContext.TaskExecutionResults.ExceptionDispatchInfo.Throw();
							}
						}

						if(task.ExecutionStatus == RoutedTask.ExecutionStatuses.New) {
							if(task.TaskExecutionResults.Error && (task.TaskExecutionResults.HandlingMode == TaskExecutionResults.ExceptionHandlingModes.Rethrow)) {
								task.TaskExecutionResults.ExceptionDispatchInfo.Throw();
							}
						}
					}
				}
			}
		}
	}
}