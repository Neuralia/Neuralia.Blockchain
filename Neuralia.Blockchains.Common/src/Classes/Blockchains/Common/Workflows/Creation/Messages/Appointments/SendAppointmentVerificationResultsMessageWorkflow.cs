using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments {

	public interface ISendAppointmentVerificationResultsMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewSignedMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     Prepare and dispatch a miner registration message on the blockchain as a gossip message
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class SendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : GenerateNewSignedMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		
		private readonly DateTime appointment;
		private List<int> processedIds;
		
		public SendAppointmentVerificationResultsMessageWorkflow(DateTime appointment, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {

			this.appointment = appointment;
			this.ExecutionMode = Workflow.ExecutingMode.Single;
		}

		public override string GenerationWorkflowTypeName => "SendAppointmentVerificationResultsMessageWorkflow";
		
		protected override Task PreProcess(LockContext lockContext) {

			return base.PreProcess(lockContext);
		}
		
		protected override async Task PostProcess(LockContext lockContext) {
			
			try {
				var appointmentRegistryDal = this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentRegistryDal;

				await appointmentRegistryDal.ClearReadyAppointmentRequesterResult(this.processedIds).ConfigureAwait(false);
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error("Failed to Clear Ready Appointment Requester Results");
			}
			
			await base.PostProcess(lockContext).ConfigureAwait(false);
		}

		
		protected override async Task<ISignedMessageEnvelope> AssembleEvent(LockContext lockContext) {
			
			var appointmentRegistryDal = this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentRegistryDal;
			
			int total = await appointmentRegistryDal.GetReadyAppointmentRequesterResultCount(this.appointment).ConfigureAwait(false);

			if(total == 0) {
				throw new ApplicationException("no resutls found");
			}
			
			var results = await centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateAppointmentVerificationResultsMessage(appointment, (app, skip, take) => {
				return Repeater.RepeatAsync(() => appointmentRegistryDal.GetReadyAppointmentRequesterResult(appointment, skip, take));
			}, lockContext).ConfigureAwait(false);

			this.processedIds = results.processedIds;
			
			return results.envelope;
		}
		
		protected override ChainNetworkingProvider.MessageDispatchTypes MessageDispatchType => ChainNetworkingProvider.MessageDispatchTypes.AppointmentValidatorResults;
		
	}
}