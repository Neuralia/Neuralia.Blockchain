using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.Base {

	public interface INetworkMessage : ITreeHashable {

		ComponentVersion<SimpleUShort> Version { get; }

		short WorkflowType { get; }
		void Dehydrate(IDataDehydrator dr);
		void Rehydrate(IDataRehydrator dr, IRehydrationFactory rehydrationFactory);
	}

	public interface INetworkMessage<R> : INetworkMessage
		where R : IRehydrationFactory {

		void Rehydrate(IDataRehydrator dr, R rehydrationFactory);
	}

	public abstract class NetworkMessage<R> : INetworkMessage<R>
		where R : IRehydrationFactory {

		public NetworkMessage() {

			this.WorkflowType = this.SetWorkflowType();
			this.Version = this.SetIdentity();

			if(this.Version.IsNull) {
				throw new ApplicationException("Version has not been set for this component");
			}

			if(this.WorkflowType == 0) {
				throw new ApplicationException("Message workflow type must be set");
			}

		}

		public ComponentVersion<SimpleUShort> Version { get; }

		public virtual void Dehydrate(IDataDehydrator dr) {
			dr.Write(this.WorkflowType);
			this.Version.Dehydrate(dr);
		}

		public void Rehydrate(IDataRehydrator dr, IRehydrationFactory rehydrationFactory) {
			this.Rehydrate(dr, (R)rehydrationFactory);
		}

		public virtual void Rehydrate(IDataRehydrator dr, R rehydrationFactory) {
			int readWorkflowType = dr.ReadShort();
			ComponentVersion<SimpleUShort> version = dr.Rehydrate<ComponentVersion<SimpleUShort>>();

			if(this.WorkflowType != readWorkflowType) {
				throw new ApplicationException("The rehydrated workflow type is different from the one we have");
			}

			version.EnsureEqual(this.Version);
		}

		public short WorkflowType { get; protected set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.WorkflowType);
			nodeList.Add(this.Version);

			return nodeList;
		}

		protected abstract ComponentVersion<SimpleUShort> SetIdentity();

		protected abstract short SetWorkflowType();
	}
}