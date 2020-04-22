using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation {

	public interface IEventGenerationWorkflowBase<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class EventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER, ENVELOPE_TYPE> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IEventGenerationWorkflowBase<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ENVELOPE_TYPE : class, IEnvelope {

		protected readonly CorrelationContext correlationContext;
		protected ENVELOPE_TYPE envelope;

		public EventGenerationWorkflowBase(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator) {
			// we make creations sequential
			this.ExecutionMode = Workflow.ExecutingMode.Sequential;
			this.correlationContext = correlationContext;

			this.Error2 += (sender, ex) => this.ExceptionOccured(ex);
		}

		protected virtual int Timeout => 60;

		protected abstract Task<ENVELOPE_TYPE> AssembleEvent(LockContext lockContext);

		protected virtual Task PreTransaction(LockContext lockContext) {

			return Task.CompletedTask;
		}

		protected abstract Task EventGenerationCompleted(ENVELOPE_TYPE envelope, LockContext lockContext);

		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext1) {

			LockContext lockContext = null;
			try {
				await centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

					token.ThrowIfCancellationRequested();

                    await PreTransaction(lc).ConfigureAwait(false);

					try {
                        await this.PreProcess(lc).ConfigureAwait(false);

						token.ThrowIfCancellationRequested();

                        envelope = await AssembleEvent(lc).ConfigureAwait(false);

                        ProcessEnvelope(envelope);

						token.ThrowIfCancellationRequested();

						ValidationResult result = ValidateContents(envelope);

						if(result.Invalid) {
							throw result.GenerateException();
						}

						token.ThrowIfCancellationRequested();
						
						try{

                            await centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(envelope, false, validationResult => {
								result = validationResult;
							}).ConfigureAwait(false);
							
							if(result.Invalid) {

								throw result.GenerateException();
							}

						}catch(Exception ex) {
							Log.Error(ex, "Failed to validate event");

							throw;
						}


                        await this.PostProcess(lc).ConfigureAwait(false);

					} catch(Exception e) {
						throw new EventGenerationException(envelope, e);
					}
					
					// we just validated and is completed, lets see if we want to do anything
					await this.EventGenerationCompleted(this.envelope, lc).ConfigureAwait(false);

				}, null, Timeout).ConfigureAwait(false);
			} catch(EventValidationException vex) {
				this.ValidationFailed(this.envelope, vex.Result);

				throw;
			}
		}

		protected virtual void ProcessEnvelope(ENVELOPE_TYPE envelope) {
			
		}

		protected override async Task Initialize(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			await this.PerformSanityChecks(lockContext).ConfigureAwait(false);

			await base.Initialize(workflow, taskRoutingContext, lockContext).ConfigureAwait(false);
		}

		protected virtual async Task PerformSanityChecks(LockContext lockContext) {

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoNetworking) {

				throw new EventGenerationException("Failed to prepare event. We are not connected to the p2p network nor have internet access.");
			}

			await this.CheckSyncStatus(lockContext).ConfigureAwait(false);

			await CheckAccounyStatus(lockContext).ConfigureAwait(false);
		}

		private async Task WaitForSync(Action<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, LockContext> syncAction, LockContext lockContext, Action<Func<LockContext, Task>> register, Action<Func<LockContext, Task>> unregister, string name) {
			using ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			Task Catcher(LockContext lockContext) {
				resetEvent.Set();

				return Task.CompletedTask;
			}

			register(Catcher);

			try {
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

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

			var walletSynned = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait(lockContext).ConfigureAwait(false);

			if(!walletSynned.HasValue || !walletSynned.Value) {

				await this.WaitForSync((service, lc) => service.SynchronizeWallet(false, lc, true), lockContext, catcher => this.centralCoordinator.WalletSynced += catcher, catcher => this.centralCoordinator.WalletSynced -= catcher, "wallet").ConfigureAwait(false);
			}
		}

		protected virtual Task ExceptionOccured(Exception ex) {
			Log.Error(ex, "Failed to create event");

			return Task.CompletedTask;
		}

		protected virtual async Task CheckAccounyStatus(LockContext lockContext) {
			// now we ensure our account is not presented or repsenting
			Enums.PublicationStatus accountStatus = (await centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

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
		protected virtual ValidationResult ValidateContents(ENVELOPE_TYPE envelope) {
			return new ValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected virtual void ValidationFailed(ENVELOPE_TYPE envelope, ValidationResult results) {

		}
	}
}