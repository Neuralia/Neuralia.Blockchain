using System;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {
	public interface INetworkWorkflow<R> : ITargettedNetworkingWorkflow<R>
		where R : IRehydrationFactory {
	}

	/// <summary>
	///     A base class for workflows that perform network operations
	/// </summary>
	public abstract class NetworkWorkflow<MESSAGE_FACTORY, R> : TargettedNetworkingWorkflow<R>, INetworkWorkflow<R>
		where MESSAGE_FACTORY : IMessageFactory<R>
		where R : IRehydrationFactory {

		protected readonly AppSettingsBase AppSettingsBase;

		private readonly DataDispatcher dataDispatcher;
		protected readonly INetworkingService networkingService;

		protected MESSAGE_FACTORY messageFactory;

		public NetworkWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {
			this.networkingService = DIService.Instance.GetService<INetworkingService>();

			this.messageFactory = this.CreateMessageFactory();

			if(GlobalSettings.ApplicationSettings.P2PEnabled) {
				// this is our own workflow, we ensure the client is always 0. (no client, but rather us)
				this.ClientId = this.GetClientId();

				this.dataDispatcher = new DataDispatcher(serviceSet.TimeService, (faultyConnection) => {
					// just in case, attempt to remove the connection if it was not already
					this.networkingService.ConnectionStore.RemoveConnection(faultyConnection);
				});
			} else {
				// no network
				this.dataDispatcher = null;
			}
		}

		protected MESSAGE_FACTORY MessageFactory {
			get => this.messageFactory;
			set => this.messageFactory = value;
		}

		protected virtual Guid GetClientId() {
			return this.networkingService.ConnectionStore.MyClientUuid;
		}

		protected abstract MESSAGE_FACTORY CreateMessageFactory();

		protected bool SendMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			return this.dataDispatcher?.SendMessage(peerConnection, message) ?? false;
		}

		protected bool SendFinalMessage(PeerConnection peerConnection, INetworkMessageSet message) {
			return this.dataDispatcher?.SendFinalMessage(peerConnection, message) ?? false;
		}

		protected bool SendBytes(PeerConnection peerConnection, ByteArray data) {
			return this.dataDispatcher?.SendBytes(peerConnection, data) ?? false;
		}

		protected bool SendFinalBytes(PeerConnection peerConnection, ByteArray data) {
			return this.dataDispatcher?.SendFinalBytes(peerConnection, data) ?? false;
		}
	}
}