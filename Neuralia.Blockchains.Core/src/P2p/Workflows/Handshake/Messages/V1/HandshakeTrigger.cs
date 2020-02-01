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
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Handshake.Messages.V1 {
	public class HandshakeTrigger<R> : WorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public readonly SoftwareVersion clientSoftwareVersion = new SoftwareVersion();
		
		public GeneralSettings generalSettings = new GeneralSettings();
		
		/// <summary>
		///     since its impossible to know otherwise, we communicate our listening port, in case it is non standard. (0 means
		///     off)
		/// </summary>
		public int listeningPort;

		public DateTime localTime;

		public int networkId;

		public long nonce;

		public NodeInfo nodeInfo = new NodeInfo();

		public string PerceivedIP;

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(GlobalSettings.Instance.NetworkId);
			dehydrator.Write(this.localTime);
			dehydrator.Write(this.listeningPort);
			dehydrator.Write(this.nonce);
			this.nodeInfo.Dehydrate(dehydrator);

			this.generalSettings.Dehydrate(dehydrator);

			dehydrator.Write(this.PerceivedIP);

			this.clientSoftwareVersion.Dehydrate(dehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator, R rehydrationFactory) {
			base.Rehydrate(rehydrator, rehydrationFactory);

			this.networkId = rehydrator.ReadInt();
			this.localTime = rehydrator.ReadDateTime();
			this.listeningPort = rehydrator.ReadInt();
			this.nonce = rehydrator.ReadLong();
			
			this.nodeInfo.Rehydrate(rehydrator);
			
			this.generalSettings.Rehydrate(rehydrator);
			
			this.PerceivedIP = rehydrator.ReadString();

			this.clientSoftwareVersion.SetVersion(rehydrator.Rehydrate<SoftwareVersion>());
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.networkId);
			nodesList.Add(this.localTime);
			nodesList.Add(this.listeningPort);
			nodesList.Add(this.nonce);
			nodesList.Add(this.nodeInfo);
			nodesList.Add(this.generalSettings);
			nodesList.Add(this.PerceivedIP);
			
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