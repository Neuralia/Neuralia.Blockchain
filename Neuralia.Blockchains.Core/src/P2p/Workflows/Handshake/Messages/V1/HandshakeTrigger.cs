using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1 {
	public class HandshakeTrigger<R> : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public readonly SoftwareVersion clientSoftwareVersion = new SoftwareVersion();

		public Dictionary<BlockchainType, ChainSettings> chainSettings = new Dictionary<BlockchainType, ChainSettings>();

		public GeneralSettings generalSettings = new GeneralSettings();
		
		/// <summary>
		///     since its impossible to know otherwise, we communicate our listening port, in case it is non standard. (0 means
		///     off)
		/// </summary>
		public int listeningPort;

		public DateTime localTime;

		public int networkId;

		public long nonce;

		public Enums.PeerTypes peerType;

		public string PerceivedIP;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(GlobalSettings.Instance.NetworkId);
			dehydrator.Write(this.localTime);
			dehydrator.Write(this.listeningPort);
			dehydrator.Write(this.nonce);
			dehydrator.Write((byte) this.peerType);
			
			this.generalSettings.Dehydrate(dehydrator);

			dehydrator.Write(this.PerceivedIP);

			this.clientSoftwareVersion.Dehydrate(dehydrator);

			// now the chain optionsBase
			dehydrator.Write((byte) this.chainSettings.Count);

			foreach(var chainsetting in this.chainSettings) {
				dehydrator.Write(chainsetting.Key.Value);

				chainsetting.Value.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.networkId = rehydrator.ReadInt();
			this.localTime = rehydrator.ReadDateTime();
			this.listeningPort = rehydrator.ReadInt();
			this.nonce = rehydrator.ReadLong();
			this.peerType = (Enums.PeerTypes) rehydrator.ReadByte();
			
			this.generalSettings.Rehydrate(rehydrator);
			
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

			nodesList.Add(this.networkId);
			nodesList.Add(this.localTime);
			nodesList.Add(this.listeningPort);
			nodesList.Add(this.nonce);
			nodesList.Add((byte) this.peerType);
			nodesList.Add(this.generalSettings);
			nodesList.Add(this.PerceivedIP);

			foreach(var chainsetting in this.chainSettings.OrderBy(s => s.Key)) {
				nodesList.Add(chainsetting.Key.Value);

				nodesList.Add(chainsetting.Value);
			}

			return nodesList;
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (HandshakeMessageFactory<R>.TRIGGER_ID, 1, 0);
		}

		protected override short SetWorkflowType() {
			return WorkflowIDs.HANDSHAKE;
		}
	}
}