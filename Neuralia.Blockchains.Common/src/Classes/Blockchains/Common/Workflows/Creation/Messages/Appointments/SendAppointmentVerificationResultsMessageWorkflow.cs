using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
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
		
		protected readonly List<IAppointmentRequesterResult> entries;
		private readonly Dictionary<long, bool> verificationResults;

		public SendAppointmentVerificationResultsMessageWorkflow(List<IAppointmentRequesterResult> entries, Dictionary<long, bool> verificationResults, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.entries = entries;
			this.verificationResults = verificationResults;
		}

		protected override Task<ISignedMessageEnvelope> AssembleEvent(LockContext lockContext) {
			return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateAppointmentVerificationResultsMessage(this.entries, this.verificationResults, lockContext);
		}
		
		protected override ChainNetworkingProvider.MessageDispatchTypes MessageDispatchType => ChainNetworkingProvider.MessageDispatchTypes.AppointmentValidatorResults;
		
	}
}