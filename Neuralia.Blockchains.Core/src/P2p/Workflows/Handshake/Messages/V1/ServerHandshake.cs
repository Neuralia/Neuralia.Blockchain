using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
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

		public Dictionary<BlockchainType, ChainSettings> chainSettings = new Dictionary<BlockchainType, ChainSettings>();

		public DateTime localTime;

		public long nonce;

		public Enums.PeerTypes peerType;

		public string PerceivedIP;

		public HandshakeStatuses Status;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.localTime);
			dehydrator.Write((byte) this.Status);
			dehydrator.Write(this.nonce);
			dehydrator.Write((byte) this.peerType);
			dehydrator.Write(this.PerceivedIP);

			this.clientSoftwareVersion.Dehydrate(dehydrator);

			// now the chain optionsBase
			dehydrator.Write((byte) (this.chainSettings?.Count ?? 0));

			if(this.chainSettings != null) {
				foreach(var chainsetting in this.chainSettings) {
					dehydrator.Write(chainsetting.Key.Value);

					chainsetting.Value.Dehydrate(dehydrator);
				}
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.localTime = rehydrator.ReadDateTime();
			this.Status = (HandshakeStatuses) rehydrator.ReadByte();
			this.nonce = rehydrator.ReadLong();
			this.peerType = (Enums.PeerTypes) rehydrator.ReadByte();
			this.PerceivedIP = rehydrator.ReadString();

			this.clientSoftwareVersion.SetVersion(rehydrator.Rehydrate<SoftwareVersion>());

			// now the chain optionsBase
			this.chainSettings = new Dictionary<BlockchainType, ChainSettings>();
			int chainSettingCount = rehydrator.ReadByte();

			for(int i = 0; i < chainSettingCount; i++) {
				BlockchainType chainid = rehydrator.ReadUShort();

				ChainSettings chainSetting = new ChainSettings();
				chainSetting.Rehydrate(rehydrator);

				this.chainSettings.Add(chainid, chainSetting);
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add((byte) this.Status);
			nodesList.Add(this.localTime);
			nodesList.Add(this.PerceivedIP);
			nodesList.Add(this.nonce);
			nodesList.Add((byte) this.peerType);

			foreach(var chainsetting in this.chainSettings.OrderBy(e => e.Key)) {
				nodesList.Add(chainsetting.Key.Value);

				nodesList.Add(chainsetting.Value);
			}

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