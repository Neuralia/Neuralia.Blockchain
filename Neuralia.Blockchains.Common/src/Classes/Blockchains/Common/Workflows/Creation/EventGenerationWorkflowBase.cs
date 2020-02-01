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
		protected ENVELOPE_TYPE envelope;

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

		protected abstract void EventGenerationCompleted(ENVELOPE_TYPE envelope);

		protected override void PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext) {
			
			try {
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {

					token.ThrowIfCancellationRequested();

					this.PreTransaction();

					try {
						this.PreProcess();

						token.ThrowIfCancellationRequested();

						this.envelope = this.AssembleEvent();
						this.ProcessEnvelope(this.envelope);

						token.ThrowIfCancellationRequested();

						ValidationResult result = this.ValidateContents(this.envelope);

						if(result.Invalid) {
							throw result.GenerateException();
						}

						token.ThrowIfCancellationRequested();


						try{

							token.ThrowIfCancellationRequested();

							this.centralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ValidateEnvelopedContent(this.envelope, false, validationResult => {
								result = validationResult;
							});
							
							if(result.Invalid) {

								throw result.GenerateException();
							}

						}catch(Exception ex) {
							Log.Error(ex, "Failed to validate event");

							throw;
						}


						this.PostProcess();

					} catch(Exception e) {
						throw new EventGenerationException(this.envelope, e);
					}
					
					// we just validated and is completed, lets see if we want to do anything
					this.EventGenerationCompleted(this.envelope);
					
				}, this.Timeout);
			} catch(EventValidationException vex) {
				this.ValidationFailed(this.envelope, vex.Result);

				throw;
			}
		}

		protected virtual void ProcessEnvelope(ENVELOPE_TYPE envelope) {
			
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
			using(ManualResetEventSlim resetEvent = new ManualResetEventSlim(false)) {

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

					if(!resetEvent.Wait(TimeSpan.FromSeconds(10))) {

						throw new ApplicationException($"The {name} is not synced. Cannot continue");
					}
				} finally {
					unregister(Catcher);
				}
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