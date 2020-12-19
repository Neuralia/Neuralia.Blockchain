using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1 {
	public class ServerHandshake<R> : NetworkMessage<R>
		where R : IRehydrationFactory {
		public enum HandshakeStatuses : byte {
			Ok = 0,
			TimeOutOfSync = 1,
			ChainUnsupported = 2,
			ClientVersionRefused = 3,
			InvalidNetworkId = 4,
			Loopback = 5,
			AlreadyConnected = 6,
			AlreadyConnecting = 7,
			InvalidPeer = 8,
			
			/// <summary>
			///     we already have too many connections
			/// </summary>
			ConnectionsSaturated = 255
		}

		public readonly SoftwareVersion clientSoftwareVersion = new SoftwareVersion();

		public bool? Connectable;

		public GeneralSettings generalSettings = new GeneralSettings();

		public DateTime localTime;

		public NodeInfo nodeInfo = new NodeInfo();

		public long nonce;

		public Guid PerceivedIP;

		public HandshakeStatuses Status = HandshakeStatuses.Ok;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.localTime);
			dehydrator.Write((byte) this.Status);
			dehydrator.Write(this.nonce);
			this.nodeInfo.Dehydrate(dehydrator);
			this.generalSettings.Dehydrate(dehydrator);
			dehydrator.Write(this.PerceivedIP);
			dehydrator.Write(this.Connectable);

			this.clientSoftwareVersion.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.localTime = rehydrator.ReadDateTime();
			this.Status = rehydrator.ReadByteEnum<HandshakeStatuses>();
			this.nonce = rehydrator.ReadLong();
			this.nodeInfo.Rehydrate(rehydrator);
			this.generalSettings.Rehydrate(rehydrator);
			this.PerceivedIP = rehydrator.ReadGuid();
			this.Connectable = rehydrator.ReadNullableBool();

			this.clientSoftwareVersion.SetVersion(rehydrator.Rehydrate<SoftwareVersion>());
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add((byte) this.Status);
			nodesList.Add(this.localTime);
			nodesList.Add(this.PerceivedIP);
			nodesList.Add(this.nonce);
			nodesList.Add(this.nodeInfo);
			nodesList.Add(this.generalSettings);
			nodesList.Add(this.Connectable);

			return nodesList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (HandshakeMessageFactory<R>.SERVER_HANDSHAKE_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.HANDSHAKE;
		}
	}
}