using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {

	public interface IBlockchainGossipMessageSet : IGossipMessageSet<IBlockchainEventsRehydrationFactory> {
		new IBlockchainGossipWorkflowTriggerMessage BaseMessage { get; }
	}

	public interface IBlockchainGossipMessageSet<out T, out EVENT_ENVELOPE_TYPE> : IGossipMessageSet<T, IBlockchainEventsRehydrationFactory>, IBlockchainGossipMessageSet
		where T : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IEnvelope {
	}

	public interface IBlockchainGossipMessageSet2<T, EVENT_ENVELOPE_TYPE> : IGossipMessageSet2<T, IBlockchainEventsRehydrationFactory>, IBlockchainGossipMessageSet
		where T : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IEnvelope {
	}

	public interface IBlockchainGossipMessageRWSet : IGossipMessageRWSet {
	}

	/// <summary>
	///     The base class for all chain gossip messages.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="EVENT_ENVELOPE_TYPE"></typeparam>
	/// <typeparam name="TRANSACTION_DETAILS"></typeparam>
	public abstract class BlockchainGossipMessageSet<T, EVENT_ENVELOPE_TYPE> : GossipMessageSet<T, IBlockchainEventsRehydrationFactory>, IBlockchainGossipMessageSet<T, EVENT_ENVELOPE_TYPE>, IBlockchainGossipMessageSet2<T, EVENT_ENVELOPE_TYPE>, IBlockchainGossipMessageRWSet
		where T : class, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : IEnvelope {

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}

		public override Enums.GossipSupportTypes MinimumNodeGossipSupport => Enums.GossipSupportTypes.Full;

		public new IBlockchainGossipWorkflowTriggerMessage BaseMessage => (IBlockchainGossipWorkflowTriggerMessage) base.BaseMessage;
	}
}