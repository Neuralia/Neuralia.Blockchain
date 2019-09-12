using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases {
	public interface IServerChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : INetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class ServerChainWorkflow<T, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : NetworkChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IServerChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where T : WorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly PeerConnection PeerConnection;

		/// <summary>
		///     the trigger message that prompted the creation of this server workflow
		/// </summary>
		protected readonly BlockchainTriggerMessageSet<T> triggerMessage;

		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);
			
			this.triggerMessage?.Message?.Dispose();
		}

		public ServerChainWorkflow(CENTRAL_COORDINATOR centralCoordinator, BlockchainTriggerMessageSet<T> triggerMessage, PeerConnection peerConnectionn) : base(centralCoordinator) {
			if(GlobalSettings.ApplicationSettings.P2pEnabled) {

				this.triggerMessage = triggerMessage;
				this.PeerConnection = peerConnectionn;

				// we give ourselves the same ID as the other side.
				this.CorrelationId = this.triggerMessage.Header.WorkflowCorrelationId;

				// we need this also, to scope this workflow as a response to a certain client
				this.ClientId = peerConnectionn.ClientUuid;
			}
		}

		protected bool Send(INetworkMessageSet message) {
			return this.SendMessage(this.PeerConnection, message);
		}

		protected bool SendFinal(INetworkMessageSet message) {
			return this.SendFinalMessage(this.PeerConnection, message);
		}

		private bool SendBytes(IByteArray data) {
			return this.SendBytes(this.PeerConnection, data);
		}

		private bool SendFinalBytes(IByteArray data) {
			return this.SendFinalBytes(this.PeerConnection, data);
		}
	}
}