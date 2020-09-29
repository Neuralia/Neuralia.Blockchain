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

		SafeArrayHandle MessageBytes { get; set; }

		new GossipHeader BaseHeader { get; }

		IGossipWorkflowTriggerMessage BaseMessage { get; }

		SafeArrayHandle DeserializedData { get; set; }

		bool HasDeserializedData { get; }

		IGossipMessageMetadata MessageMetadata { get; set; }

		Enums.GossipSupportTypes MinimumNodeGossipSupport { get; }
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
		public SafeArrayHandle DeserializedData { get; set; }

		public bool HasDeserializedData => this.DeserializedData != null;
		public IGossipMessageMetadata MessageMetadata { get; set; }
		
		/// <summary>
		/// here we cache the message bytes, usually for hashing
		/// </summary>
		public SafeArrayHandle MessageBytes { get; set; }

		public virtual HashNodeList GetStructuresArray() {

			if(this.MessageBytes == null || this.MessageBytes.IsZero) {
				// lets make sure the message bytes were acquired. if we dont have them, we have to dehydrate the message
				this.DehydrateMessageForBytes();
			}
			
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Header);
			// for gossip messages, we hash the message bytes, and not the rehydrated content. this is the same data, but we dont need to explicitly rehydrate. allows us to verify earlier in the process
			hashNodeList.Add(this.MessageBytes);
			hashNodeList.Add(this.MessageMetadata);

			return hashNodeList;
		}

		public virtual Enums.GossipSupportTypes MinimumNodeGossipSupport => Enums.GossipSupportTypes.Basic;

		public void ResetCachedMessageBytes() {
			this.MessageBytes?.Dispose();
			this.MessageBytes = null;
		}
		
		protected override void DehydrateContents(IDataDehydrator dehydrator) {

			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.MessageMetadata == null);

			this.MessageMetadata?.Dehydrate(dehydrator);
		}

		private void DehydrateMessageForBytes() {
			if(this.MessageBytes == null || this.MessageBytes.IsZero) {
				this.MessageBytes = base.DehydrateMessage();
			}
		}
		protected override SafeArrayHandle DehydrateMessage() {
			this.DehydrateMessageForBytes();

			return this.MessageBytes.Branch();
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