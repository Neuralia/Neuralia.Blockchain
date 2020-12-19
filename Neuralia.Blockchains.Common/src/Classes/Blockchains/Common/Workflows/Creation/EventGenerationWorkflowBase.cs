using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation {

	public interface IEventGenerationWorkflowBase : IChainWorkflow {
		IWalletGenerationCache WalletGenerationCache { get; set; }
	}

	public interface IEventGenerationWorkflowBase<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IEventGenerationWorkflowBase
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

	}

	public abstract class EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ENVELOPE_TYPE, DEHYDRATED_TYPE, BLOCKCHAIN_EVENT_TYPE, VERSION_TYPE> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IEventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ENVELOPE_TYPE : class, IEnvelope<DEHYDRATED_TYPE>
		where DEHYDRATED_TYPE : class, IDehydrateBlockchainEvent<BLOCKCHAIN_EVENT_TYPE>
		where BLOCKCHAIN_EVENT_TYPE : class, IBlockchainEvent<DEHYDRATED_TYPE, IBlockchainEventsRehydrationFactory, VERSION_TYPE>
		where VERSION_TYPE : SimpleUShort<VERSION_TYPE>, new() {

		private static HashSet<string> runningProcesses = new HashSet<string>();
		private static object processLocker = new object();
		protected bool PreDispatch { get; private set; }
		protected CorrelationContext correlationContext;

		private BLOCKCHAIN_EVENT_TYPE blockchainEvent;

		protected BLOCKCHAIN_EVENT_TYPE BlockchainEvent {
			get {
				if(this.blockchainEvent == null) {
					this.blockchainEvent = this.envelope.Contents.RehydratedEvent;
				}

				return this.blockchainEvent;
			}
		}

		protected ENVELOPE_TYPE envelope;
		protected Exception exception = null;
		public IWalletGenerationCache WalletGenerationCache { get; set; }

		public EventGenerationWorkflowBase(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext, IWalletGenerationCache WalletGenerationCache = null) : base(centralCoordinator) {
			// we make creations sequential
			
			this.ExecutionMode = Workflow.ExecutingMode.Sequential;
			this.correlationContext = correlationContext;
			this.WalletGenerationCache = WalletGenerationCache;

			this.SetCorrelationContextRetry();
			this.Error2 += (sender, ex) => this.ExceptionOccured(ex);
		}

		/// <summary>
		/// restore the correlation id from a previous run to ensure uniformity
		/// </summary>
		private void SetCorrelationContextRetry() {
			if(this.WalletGenerationCache != null) {
				this.correlationContext = new CorrelationContext(this.WalletGenerationCache.CorrelationId);
			}
		}
		protected virtual int Timeout => 60;

		public abstract class WorkflowTask {
			public struct ActionSet {
				public ActionTypes ActionType { get; set; }
				public Func<LockContext, Task> ActionCallback { get; set; }

				public enum ActionTypes {
					SetEntry,
					UpdateCache,
					Custom
				}
			}

			public string Name { get; set; }
			public ActionSet[] PreActions { get; set; } = null;
			public ActionSet[] PostActions { get; set; } = null;
		}

		public class SingleWorkflowTask : WorkflowTask {
			public Func<LockContext, Task> Action { get; set; }
		}

		public class SingleTransactionWorkflowTask : WorkflowTask {
			public Func<IWalletProvider, CancellationToken, LockContext, Task> Action { get; set; }
		}

		public class TransactionWorkflowTask : WorkflowTask {
			public readonly List<SingleTransactionWorkflowTask> tasks = new List<SingleTransactionWorkflowTask>();
			public readonly List<Func<Task>> Completions = new List<Func<Task>>();
			
			public void AddSingleTask(SingleTransactionWorkflowTask task) {
				this.tasks.Add(task);
			}
		}

		private readonly List<WorkflowTask> tasks = new List<WorkflowTask>();
		private WorkflowTask lastTask;

		protected const string PREPROCESS_TASK_NAME = "preprocess";
		protected const string ASSEMBLE_TASK_NAME = "assemble";
		protected const string PREVALIDATE_CONTENTS_TASK_NAME = "prevalidatecontents";
		protected const string PROCESS_ENVELOPE_TASK_NAME = "processenvelope";
		protected const string SIGN_ENVELOPE_TASK_NAME = "sign";
		protected const string VALIDATE_CONTENTS_TASK_NAME = "validate_contents";
		protected const string POST_PROCESS_TASK_NAME = "post_process";
		protected const string DISPATCH_TASK_NAME = "dispatch";
		protected const string WORKFLOW_COMPLETED_TASK_NAME = "workflow_completed";
		
	#region workflow tasks

		protected void AddSingleTask(SingleWorkflowTask task) {
			this.lastTask = task;
			this.tasks.Add(task);
		}

		protected void AddSingleTask(string name, Func<LockContext, Task> action, WorkflowTask.ActionSet[] preActions = null, WorkflowTask.ActionSet[] postActions = null) {
			this.AddSingleTask(new SingleWorkflowTask() {Name = name, Action = action, PreActions = preActions, PostActions = postActions});
		}

		protected void AddWalletTransactionTask(string name, Func<IWalletProvider, CancellationToken, LockContext, Task> action, WorkflowTask.ActionSet[] preActions = null, WorkflowTask.ActionSet[] postActions = null, Func<Task> transactionSuccessAction = null) {
			this.AddWalletTransactionTask(new SingleTransactionWorkflowTask() {Name = name, Action = action, PreActions = preActions, PostActions = postActions}, transactionSuccessAction);
		}

		protected void AddWalletTransactionTask(SingleTransactionWorkflowTask task, Func<Task> transactionSuccessAction = null) {
			TransactionWorkflowTask workflowTask = null;

			if(this.lastTask is TransactionWorkflowTask transactionWorkflowTask) {
				workflowTask = transactionWorkflowTask;
			}

			if(workflowTask == null) {
				workflowTask = new TransactionWorkflowTask();
				workflowTask.Name = task.Name;
				this.tasks.Add(workflowTask);

				if(transactionSuccessAction != null) {
					workflowTask.Completions.Add(transactionSuccessAction);
				}
			}

			workflowTask.AddSingleTask(task);
		}

		protected virtual void BuildWorkflowStructure() {
			this.AddTaskPreProcess();

			this.AddTaskAssembleEvent();

			this.AddTaskPreValidateContents();

			this.AddTaskProcessEnvelope();

			this.AddTaskSignEnvelope();

			this.AddTaskValidateContents();

			this.AddTaskPostProcess();

			this.AddTaskDispatch();

			this.AddTaskWorkflowCompleted();
		}

		protected virtual void AddTaskPreProcess() {
			this.AddSingleTask(PREPROCESS_TASK_NAME, this.PreProcess);
		}

		protected virtual void AddTaskAssembleEvent() {
			this.AddSingleTask(ASSEMBLE_TASK_NAME, async lc => {
				this.envelope = await this.AssembleEvent(lc).ConfigureAwait(false);
				this.EnvelopeSet();
			});
		}

		protected virtual void AddTaskPreValidateContents() {
			this.AddSingleTask(PREVALIDATE_CONTENTS_TASK_NAME, this.PreValidateContents, null, new[] {new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.SetEntry}});
		}

		protected virtual void AddTaskProcessEnvelope() {
			this.AddSingleTask(PROCESS_ENVELOPE_TASK_NAME, this.ProcessEnvelope, null, new[] {new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.UpdateCache}});
		}

		protected virtual void AddTaskSignEnvelope() {
			this.AddWalletTransactionTask(SIGN_ENVELOPE_TASK_NAME, this.SignEnvelope, null, new[] {new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = this.EventSigned}});
		}

		protected virtual void AddTaskValidateContents() {
			this.AddSingleTask(VALIDATE_CONTENTS_TASK_NAME, this.ValidateContents);
		}

		protected virtual void AddTaskPostProcess() {
			this.AddSingleTask(POST_PROCESS_TASK_NAME, this.PostProcess);
		}

		protected virtual void AddTaskDispatch() {
			this.AddSingleTask(DISPATCH_TASK_NAME, this.Dispatch, new []{new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = this.PerformSanityChecksPreDispatch}}, new[] {new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.Custom, ActionCallback = this.Dispatched}, new WorkflowTask.ActionSet() {ActionType = WorkflowTask.ActionSet.ActionTypes.UpdateCache}});
		}

		protected virtual void AddTaskWorkflowCompleted() {
			this.AddSingleTask(WORKFLOW_COMPLETED_TASK_NAME, this.WorkflowCompleted);
		}

	#endregion

		private void EnvelopeSet() {

			if(this.envelope == null) {
				return;
			}

			string key = this.envelope.GetId();

			if(string.IsNullOrWhiteSpace(key)) {
				return;
			}

			lock(processLocker) {

				if(runningProcesses.Contains(key)) {
					this.CancelTokenSource.Cancel();

					return;
				}

				runningProcesses.Add(key);
			}
		}

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			try {
				this.BuildWorkflowStructure();

				int startingIndex = 0;
				var indexedTasks = this.tasks.Select((t, i) => (task: t, index: i)).ToList();
				bool inTransaction = false;

				//TODO: this can be cleaned up
				if(this.WalletGenerationCache == null) {

					// try to load if anything
					this.WalletGenerationCache = await this.LoadExistingGenerationCacheEntry(lockContext).ConfigureAwait(false);

					if(this.WalletGenerationCache != null && this.WalletGenerationCache.Expiration < DateTimeEx.CurrentTime) {
						
						await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteGenerationCacheEntry(this.WalletGenerationCache.Key, lockContext).ConfigureAwait(false);

						this.WalletGenerationCache = null;
					}

					if(this.WalletGenerationCache != null) {
						this.SetCorrelationContextRetry();
					}
				}

				if(this.WalletGenerationCache != null) {
					if(this.WalletGenerationCache.Event != null && !this.WalletGenerationCache.Event.IsZero) {
						this.envelope = (ENVELOPE_TYPE) this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope(this.WalletGenerationCache.Event);
						this.envelope.RehydrateContents();

						this.envelope.Contents.Rehydrate(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

						var task = indexedTasks.SingleOrDefault(e => e.task.Name == this.WalletGenerationCache.Step);

						if(task != default) {
							startingIndex = task.index;
						}

						if(this.WalletGenerationCache.StepStatus == Wallet.Account.WalletGenerationCache.StepStatuses.Completed) {
							// we were done, move on to the next
							startingIndex++;
						}
					} else {
						// we had no event bytes, might as well clear and start over
						await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteGenerationCacheEntry(this.WalletGenerationCache.Key, lockContext).ConfigureAwait(false);

						this.WalletGenerationCache = null;
					}
				}

				// finally, after all this, if still null, we start from scratch
				if(this.WalletGenerationCache == null) {

					this.WalletGenerationCache = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletAccountGenerationCache();
					this.WalletGenerationCache.EventType = this.GetEventType();
					this.WalletGenerationCache.EventSubType = this.GetEventSubType();
					this.WalletGenerationCache.Timestamp = DateTimeEx.CurrentTime;
					this.WalletGenerationCache.CorrelationId = this.correlationContext.CorrelationId;
				}

				if(this.envelope != null) {
					this.EnvelopeSet();
				}

				this.CheckShouldCancel();

				Task ExecuteSimpleStep(SingleWorkflowTask task, string step, LockContext lc) {
					return ExecuteStep(lc2 => {

						return task.Action(lc2);
					}, task, step, lc);
				}

				Task ExecuteTransactionStep(SingleTransactionWorkflowTask task, IWalletProvider provider, CancellationToken token, string step, LockContext lc) {
					return ExecuteStep(lc2 => {

						return task.Action(provider, token, lc2);
					}, task, step, lc);
				}

				async Task ExecuteStep(Func<LockContext, Task> action, WorkflowTask task, string step, LockContext lc) {

					async Task PerformSideActions(WorkflowTask.ActionSet[] actions, LockContext lc2) {

						if(actions != null) {
							foreach(var action1 in actions) {
								if(action1.ActionType == WorkflowTask.ActionSet.ActionTypes.UpdateCache) {
									await this.UpdateGenerationCacheEntry(lc2).ConfigureAwait(false);
								} else if(action1.ActionType == WorkflowTask.ActionSet.ActionTypes.SetEntry) {
									await this.SetEntry(lc2).ConfigureAwait(false);
								} else if(action1.ActionType == WorkflowTask.ActionSet.ActionTypes.Custom && action1.ActionCallback != null) {
									await action1.ActionCallback(lc2).ConfigureAwait(false);
								}
							}
						}
					}

					this.WalletGenerationCache.Step = step;
					this.WalletGenerationCache.SubStep = ""; // reset this
					this.WalletGenerationCache.StepStatus = Wallet.Account.WalletGenerationCache.StepStatuses.New;
					await PerformSideActions(task.PreActions, lc).ConfigureAwait(false);

					await action(lc).ConfigureAwait(false);

					this.WalletGenerationCache.StepStatus = Wallet.Account.WalletGenerationCache.StepStatuses.Completed;
					await PerformSideActions(task.PostActions, lc).ConfigureAwait(false);
				}

				// begin performing the workflow
				foreach(var taskEntry in indexedTasks.Where(t => t.index >= startingIndex)) {

					if(taskEntry.task is SingleWorkflowTask singleWorkflowTask) {
						await ExecuteSimpleStep(singleWorkflowTask, singleWorkflowTask.Name, lockContext).ConfigureAwait(false);
					} else if(taskEntry.task is TransactionWorkflowTask transactionWorkflowTask) {

						await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

							foreach(var subTask in transactionWorkflowTask.tasks) {
								token.ThrowIfCancellationRequested();
								await ExecuteTransactionStep(subTask, provider, token, transactionWorkflowTask.Name, lc).ConfigureAwait(false);
							}

							await this.UpdateGenerationCacheEntry(lc).ConfigureAwait(false);
						}, null, this.Timeout).ConfigureAwait(false);

						foreach(var func in transactionWorkflowTask.Completions) {
							await func().ConfigureAwait(false);
						}
					}

					this.CheckShouldCancel();
				}

				try {
					await Repeater.RepeatAsync(async () => {
						await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteGenerationCacheEntry(this.WalletGenerationCache.Key, lockContext).ConfigureAwait(false);
						await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet(lockContext).ConfigureAwait(false);
					}).ConfigureAwait(false);
				} catch(Exception ex) {
					// we can let this go, its not critical.
					this.CentralCoordinator.Log.Error(ex, "Failed to clear cache from entry");
				}

				this.WalletGenerationCache = null;

				this.CentralCoordinator.Log.Information("Creation of event completed successfully");

			} catch(EventValidationException vex) {
				this.exception = vex;
				this.ValidationFailed(this.envelope, vex.Result);

				throw;
			} catch(Exception ex) {
				this.exception = ex;

				this.CentralCoordinator.Log.Error(ex, "Creation of event failed");

				throw;
			} finally {
				try {
					await this.Finally(lockContext).ConfigureAwait(false);
				} catch {

				}
				if(this.envelope != null) {
					string key = this.envelope.GetId();

					lock(processLocker) {
						if(runningProcesses.Contains(key)) {
							runningProcesses.Remove(key);
						}
					}
				}
			}
		}

		protected abstract Task<ENVELOPE_TYPE> AssembleEvent(LockContext lockContext);

		protected virtual Task Finally(LockContext lockContext) {

			return Task.CompletedTask;
		}

		protected virtual Task Dispatch(LockContext lockContext) {
			return Task.CompletedTask;
		}

		protected virtual Task Dispatched(LockContext lockContext) {
			// lets mark it as signed
			this.WalletGenerationCache.Dispatched = true;

			return Task.CompletedTask;
		}

		protected virtual Task WorkflowCompleted(LockContext lockContext) {
			
			return Task.CompletedTask;
		}

		protected abstract TimeSpan GetEnvelopeExpiration();
		protected abstract WalletGenerationCache.DispatchEventTypes GetEventType();
		protected abstract string GetEventSubType();
		
		/// <summary>
		/// load an existing entry, see if we could continue it.
		/// </summary>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		protected virtual Task<IWalletGenerationCache> LoadExistingGenerationCacheEntry(LockContext lockContext) {
			return Task.FromResult(this.WalletGenerationCache);
		}

		protected virtual Task SetEntry(LockContext lockContext) {

			this.WalletGenerationCache.Key = this.envelope.GetId();

			var cachedEvent = this.BlockchainEvent;

			// here we have to clear the envelope and dehydrate the content again, there may have been changes
			this.envelope.Clear();

			// dehydrate the contents again to capture changes if any

			if(cachedEvent != null) {
				this.envelope.Contents = cachedEvent.Dehydrate(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);
			}

			// and now the latest envelope
			this.WalletGenerationCache.Event = this.envelope.DehydrateEnvelope();
			this.WalletGenerationCache.Version = this.BlockchainEvent.Version.ToString();
			this.WalletGenerationCache.NextRetry = DateTimeEx.CurrentTime.AddMinutes(5);
			this.WalletGenerationCache.Expiration = DateTimeEx.CurrentTime + this.GetEnvelopeExpiration() - TimeSpan.FromHours(2);

			return Task.CompletedTask;
		}

		protected virtual async Task UpdateGenerationCacheEntry(LockContext lockContext) {
			await this.SetEntry(lockContext).ConfigureAwait(false);
			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateGenerationCacheEntry(this.WalletGenerationCache, lockContext).ConfigureAwait(false);
			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet(lockContext).ConfigureAwait(false);
		}

		protected virtual Task ProcessEnvelope(LockContext lockContext) {
			return Task.CompletedTask;

		}

		protected override async Task Initialize(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			await this.PerformSanityChecks(lockContext).ConfigureAwait(false);

			await base.Initialize(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
		}

		protected virtual Task PerformSanityChecksPreDispatch(LockContext lockContext) {
			this.PreDispatch = true;
			return this.PerformSanityChecks(lockContext);
		}

		
		protected virtual async Task PerformSanityChecks(LockContext lockContext) {

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletIsLoaded();

			var networkingProvider = this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase;

			if(networkingProvider.NoNetworking) {

				throw new EventGenerationException("Failed to prepare event. We are not connected to the p2p network nor have internet access.");
			}

			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			
			if(!chainConfiguration.RegistrationMethod.HasFlag(AppSettingsBase.ContactMethods.Web) && !networkingProvider.MinimumDispatchPeerCountAchieved()) {

				if(networkingProvider.NoPeerConnections) {
					throw new EventGenerationException("Failed to create event. We are not connected to any peers on the p2p network");
				}

				throw new EventGenerationException($"Failed to create event. We do not have enough peers. we need a minimum of {chainConfiguration.MinimumDispatchPeerCount}");
			}

			await this.CheckSyncStatus(lockContext).ConfigureAwait(false);

			await this.CheckAccountStatus(lockContext).ConfigureAwait(false);
		}

		private async Task WaitForSync(Action<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, LockContext> syncAction, LockContext lockContext, Action<Func<LockContext, Task>> register, Action<Func<LockContext, Task>> unregister, string name) {
			using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			Task Catcher(LockContext lc) {
				resetEvent.Set();

				return Task.CompletedTask;
			}

			register(Catcher);

			try {
				BlockchainTask<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, bool, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction(async (service, taskRoutingContext2, lc) => {
					syncAction(service, lc);
				});

				await this.DispatchTaskSync(blockchainTask, lockContext).ConfigureAwait(false);

				if(!resetEvent.Wait(TimeSpan.FromSeconds(10))) {

					throw new ApplicationException($"The {name} is not synced. Cannot continue");
				}
			} finally {
				unregister(Catcher);
			}

		}

		protected virtual async Task CheckSyncStatus(LockContext lockContext) {
			bool likelySynced = this.centralCoordinator.IsChainLikelySynchronized;

			if(!likelySynced) {

				await this.WaitForSync((service, lc) => service.SynchronizeBlockchain(false, lc), lockContext, catcher => this.centralCoordinator.BlockchainSynced += catcher, catcher => this.centralCoordinator.BlockchainSynced -= catcher, "blockchain").ConfigureAwait(false);
			}

			bool? walletSynced = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait(lockContext).ConfigureAwait(false);

			if(!walletSynced.HasValue || !walletSynced.Value) {

				await this.WaitForSync((service, lc) => service.SynchronizeWallet(false, lc, true), lockContext, catcher => this.centralCoordinator.WalletSynced += catcher, catcher => this.centralCoordinator.WalletSynced -= catcher, "wallet").ConfigureAwait(false);
			}
		}

		protected virtual Task ExceptionOccured(Exception ex) {
			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.Error(this.centralCoordinator.ChainId, ex.Message), this.correlationContext);

			this.CentralCoordinator.Log.Error(ex, "Failed to create event");

			return Task.CompletedTask;
		}

		protected virtual async Task CheckAccountStatus(LockContext lockContext) {
			// now we ensure our account is not presented or repsenting
			Enums.PublicationStatus accountStatus = (await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

			if(accountStatus != Enums.PublicationStatus.Published) {
				throw new EventGenerationException("The account has not been published and can not be used.");
			}
		}

		protected virtual Task PreProcess(LockContext lockContext) {

			return Task.CompletedTask;
		}

		protected virtual Task PostProcess(LockContext lockContext) {
			return Task.CompletedTask;
		}

		/// <summary>
		///     validate contents
		/// </summary>
		/// <param name="envelope"></param>
		protected virtual Task PreValidateContents(LockContext lockContext) {

			return Task.CompletedTask;
		}

		protected abstract Task SignEnvelope(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext);

		protected virtual Task EventSigned(LockContext lockContext) {
			// lets mark it as signed
			this.WalletGenerationCache.Signed = true;

			return Task.CompletedTask;
		}

		protected virtual async Task ValidateContents(LockContext lockContext) {
			var result = new ValidationResult(ValidationResult.ValidationResults.Invalid);

			try {

				await this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(this.envelope, false, validationResult => {
					result = validationResult;
				}, lockContext, ChainValidationProvider.ValidationModes.Self).ConfigureAwait(false);

				if(result.Invalid) {

					throw result.GenerateException();
				}

			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to validate event");

				throw;
			}

		}

		protected virtual void ValidationFailed(ENVELOPE_TYPE envelope, ValidationResult results) {

		}

	}
}