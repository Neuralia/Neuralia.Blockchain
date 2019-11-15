using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets.GossipMessageMetadatas;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets {
	public interface IGossipMessageSet : INetworkMessageSet, ITreeHashable {

		new GossipHeader BaseHeader { get; }

		IGossipWorkflowTriggerMessage BaseMessage { get; }

		ByteArray DeserializedData { get; set; }

		bool HasDeserializedData { get; }

		IGossipMessageMetadata MessageMetadata { get; }

		Enums.PeerTypeSupport MinimumNodeTypeSupport { get; }
	}

	public interface IGossipMessageSet<R> : IGossipMessageSet, INetworkMessageSet<R>
		where R : IRehydrationFactory {

		new IGossipWorkflowTriggerMessage<R> BaseMessage { get; }
	}

	public interface IGossipMessageSet<out T, R> : INetworkMessageSet<T, GossipHeader, R>, IGossipMessageSet<R>
		where T : class, INetworkMessage<R>
		where R : IRehydrationFactory {
	}

	public interface IGossipMessageSet2<T, R> : INetworkMessageSet2<T, GossipHeader, R>, IGossipMessageSet<R>
		where T : class, INetworkMessage<R>
		where R : IRehydrationFactory {
	}

	public interface IGossipMessageRWSet {
		GossipHeader RWBaseHeader { get; set; }
	}

	public abstract class GossipMessageSet<T, R> : NetworkMessageSet<T, GossipHeader, R>, IGossipMessageSet<T, R>, IGossipMessageSet2<T, R>, IGossipMessageRWSet
		where T : class, IGossipWorkflowTriggerMessage<R>
		where R : IRehydrationFactory {

		public GossipHeader RWBaseHeader {
			get => this.Header;
			set => this.Header = value;
		}

		public new GossipHeader BaseHeader => this.Header;

		IGossipWorkflowTriggerMessage IGossipMessageSet.BaseMessage => this.Message;
		public new IGossipWorkflowTriggerMessage<R> BaseMessage => this.Message;

		/// <summary>
		///     If we rehydrated the message from the network, we can store the byte array format, so we can avoid an expensive
		///     deserializa
		/// </summary>
		public ByteArray DeserializedData { get; set; }

		public bool HasDeserializedData => this.DeserializedData != null;
		public IGossipMessageMetadata MessageMetadata { get; set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Header);
			hashNodeList.Add(this.Message);
			hashNodeList.Add(this.MessageMetadata);

			return hashNodeList;
		}

		public virtual Enums.PeerTypeSupport MinimumNodeTypeSupport => Enums.PeerTypeSupport.GossipBasic;

		protected override void DehydrateContents(IDataDehydrator dehydrator) {

			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.MessageMetadata == null);

			this.MessageMetadata?.Dehydrate(dehydrator);
		}

		protected override void RehydrateContents(IDataRehydrator dr, R rehydrationFactory) {

			base.RehydrateContents(dr, rehydrationFactory);

			bool isNMetadataNull = dr.ReadBool();

			if(!isNMetadataNull) {
				this.MessageMetadata = new GossipMessageMetadata();
				this.MessageMetadata.Rehydrate(dr);
			}
		}
	}
}