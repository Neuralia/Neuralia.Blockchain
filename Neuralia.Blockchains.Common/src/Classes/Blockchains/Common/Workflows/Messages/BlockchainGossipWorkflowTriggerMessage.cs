using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages {

	public interface IBlockchainGossipWorkflowTriggerMessage : IGossipWorkflowTriggerMessage {
		IEnvelope BaseEnvelope { get; }
	}

	public interface IBlockchainGossipWorkflowTriggerMessage<out EVENT_ENVELOPE_TYPE> : IGossipWorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, IBlockchainGossipWorkflowTriggerMessage
		where EVENT_ENVELOPE_TYPE : IEnvelope {

		EVENT_ENVELOPE_TYPE Envelope { get; }
	}

	/// <summary>
	///     base class for gossip messages. they have the ability to hash themselves
	/// </summary>
	/// <remarks>
	///     Gossip messages are special messages that will be sent virally in the p2p network. In order to force players to
	///     play nice,
	///     each gossip message MUST be identified and Signed by this account. in order to achieve this, we force these
	///     messages to ALWAYS
	///     contain a transaction. these always have an account and a signature, which ensures the
	///     message is itself
	///     identified. Peers will only relay gosisp messages that
	///     are valid. any invalid message will die, and mark the account (or IP) as ill intended.
	/// </remarks>
	public abstract class BlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE> : GossipWorkflowTriggerMessage<IBlockchainEventsRehydrationFactory>, ITreeHashable, IBlockchainGossipWorkflowTriggerMessage<EVENT_ENVELOPE_TYPE>
		where EVENT_ENVELOPE_TYPE : class, IEnvelope {

		public override void Dehydrate(IDataDehydrator dr) {
			base.Dehydrate(dr);

			if(this.Envelope == null) {
				throw new ApplicationException("The envelope MUST be set and valid for a gossip message to be valid");
			}

			dr.WriteRawArray(this.Envelope.DehydrateEnvelope());
		}

		/// <summary>
		///     This method allows us to rehydrate in 2 steps. First the message and the transaction
		///     components.
		///     later, in the right chain context, we can rehydrate the transaction itself
		/// </summary>
		/// <param name="dr"></param>
		public override void Rehydrate(IDataRehydrator dr, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			base.Rehydrate(dr, rehydrationFactory);

			this.Envelope = rehydrationFactory.RehydrateEnvelope<EVENT_ENVELOPE_TYPE>(dr.ReadArrayToEnd());
		}

		public IEnvelope BaseEnvelope => this.Envelope;

		/// <summary>
		///     The intermediary components that allow us to defer the rehydration of the transaction
		/// </summary>
		public EVENT_ENVELOPE_TYPE Envelope { get; set; }

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.Envelope.GetStructuresArray());

			return nodesList;
		}

		protected override sealed ComponentVersion<SimpleUShort> SetIdentity() {
			;

			// make sure it is always this value since gossip workflows ONLY have one message. there is no exchange
			(ushort major, ushort minor) gossipVersions = this.SetGossipIdentity();

			return (GossipWorkflowTriggerMessageConstants.GOSSIP_MESSAGE_TRIGGER, gossipVersions.major, gossipVersions.minor);
		}

		/// <summary>
		///     in here, set the workflow type and the message version
		/// </summary>
		protected abstract (ushort major, ushort minor) SetGossipIdentity();
	}
}