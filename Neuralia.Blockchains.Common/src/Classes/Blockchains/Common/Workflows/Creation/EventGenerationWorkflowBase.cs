using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Exceptions.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
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

		public EventGenerationWorkflowBase(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator) {
			// we make creations sequential
			this.ExecutionMode = Workflow.ExecutingMode.Sequential;
			this.correlationContext = correlationContext;

			this.Error2 += (sender, ex) => {
				this.ExceptionOccured(ex);
			};
		}

		protected virtual int Timeout => 60;

		protected abstract ENVELOPE_TYPE AssembleEvent();

		protected virtual void PreTransaction() {

		}

		protected abstract List<IRoutedTask> EventValidated(ENVELOPE_TYPE envelope);

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

			ENVELOPE_TYPE envelope = null;

			try {
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(token => {

					token.ThrowIfCancellationRequested();

					this.PreTransaction();

					try {
						this.PreProcess();

						token.ThrowIfCancellationRequested();

						envelope = this.AssembleEvent();

						token.ThrowIfCancellationRequested();

						ValidationResult result = this.ValidateContents(envelope);

						if(result.Invalid) {
							throw result.GenerateException();
						}

						token.ThrowIfCancellationRequested();

						var validationTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateValidationTask<ValidationResult>();

						validationTask.SetAction((validationService, taskRoutingContext2) => {

							token.ThrowIfCancellationRequested();

							IRoutedTask validateEventTask = validationService.ValidateEnvelopedContent(envelope, validationResult => {
								validationTask.Results = validationResult;
							});

							taskRoutingContext2.AddChild(validateEventTask);
						}, (results, taskRoutingContext2) => {
							if(results.Success) {
								if(validationTask.Results.Invalid) {

									throw validationTask.Results.GenerateException();
								}

							} else {
								Log.Error(results.Exception, "Failed to validate event");

								results.Rethrow();
							}
						});

						this.DispatchTaskSync(validationTask);

						this.PostProcess();

					} catch(Exception e) {
						throw new EventGenerationException(envelope, e);
					}
				}, this.Timeout);
			} catch(EventValidationException vex) {
				this.ValidationFailed(envelope, vex.Result);

				throw;
			}

			//TODO: we leave this out of the wallet transaction. perhaps the new Event should be saved somewhere?
			// we just validated, lets see if we want to do anything
			var extraRedirects = this.EventValidated(envelope);

			if(extraRedirects.Any()) {
				foreach(IRoutedTask task in extraRedirects) {
					this.DispatchTaskSync(task);
				}
			}
		}

		protected override void Initialize(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {

			this.PerformSanityChecks();

			base.Initialize(workflow, taskRoutingContext);
		}

		protected virtual void PerformSanityChecks() {

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletIsLoaded();

			if(this.centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.NoNetworking) {

				throw new EventGenerationException("Failed to prepare event. We are not connected to the p2p network nor have internet access.");
			}

			this.CheckSyncStatus();

			this.CheckAccounyStatus();
		}

		private void WaitForSync(Action<IBlockchainManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>> syncAction, Action<Action> register, Action<Action> unregister, string name) {
			AutoResetEvent resetEvent = new AutoResetEvent(false);

			void Catcher() {
				resetEvent.Set();
			}

			register(Catcher);

			try {
				var blockchainTask = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateBlockchainTask<bool>();

				blockchainTask.SetAction((service, taskRoutingContext2) => {
					syncAction(service);
				});

				this.DispatchTaskSync(blockchainTask);

				if(!resetEvent.WaitOne(TimeSpan.FromSeconds(10))) {

					throw new ApplicationException($"The {name} is not synced. Cannot continue");
				}
			} finally {
				unregister(Catcher);
			}
		}

		protected virtual void CheckSyncStatus() {
			bool likelySynced = this.centralCoordinator.IsChainLikelySynchronized;

			if(!likelySynced) {

				this.WaitForSync(service => service.SynchronizeBlockchain(false), catcher => this.centralCoordinator.BlockchainSynced += catcher, catcher => this.centralCoordinator.BlockchainSynced -= catcher, "blockchain");
			}

			var walletSynned = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.SyncedNoWait;

			if(!walletSynned.HasValue || !walletSynned.Value) {

				this.WaitForSync(service => service.SynchronizeWallet(false, true), catcher => this.centralCoordinator.WalletSynced += catcher, catcher => this.centralCoordinator.WalletSynced -= catcher, "wallet");
			}
		}

		protected virtual void ExceptionOccured(Exception ex) {
			Log.Error(ex, "Failed to create event");
		}

		protected virtual void CheckAccounyStatus() {
			// now we ensure our account is not presented or repsenting
			Enums.PublicationStatus accountStatus = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount().Status;

			if(accountStatus != Enums.PublicationStatus.Published) {
				throw new EventGenerationException("The account has not been published and can not be used.");
			}
		}

		protected virtual void PreProcess() {

		}

		protected virtual void PostProcess() {

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