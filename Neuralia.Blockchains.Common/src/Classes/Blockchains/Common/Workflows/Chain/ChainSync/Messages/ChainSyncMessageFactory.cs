using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Block;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Digest;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Messages;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Workflows;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages {

	public static class ChainSyncMessageFactoryIds {
		public const ushort TRIGGER_ID = 1;
		public const ushort SERVER_TRIGGER_REPLY = 2;
		public const ushort FINISH_SYNC = 3;
		public const ushort REQUEST_BLOCK = 4;
		public const ushort SEND_BLOCK = 5;
		public const ushort REQUEST_BLOCK_INFO = 6;
		public const ushort SEND_BLOCK_INFO = 7;
		public const ushort REQUEST_BLOCK_SLICE_HASHES = 8;
		public const ushort SEND_BLOCK_SLICE_HASHES = 9;

		public const ushort REQUEST_DIGEST = 10;
		public const ushort SEND_DIGEST = 11;
		public const ushort REQUEST_DIGEST_FILE = 12;
		public const ushort SEND_DIGEST_FILE = 13;
		public const ushort REQUEST_DIGEST_INFO = 14;
		public const ushort SEND_DIGEST_INFO = 15;
	}

	public interface IChainSyncMessageFactory {
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> Rehydrate(SafeArrayHandle data, TargettedHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory);

		/// <summary>
		///     this is the client side trigger method, when we build a brand new one
		/// </summary>
		/// <param name="workflowCorrelationId"></param>
		/// <returns></returns>
		IBlockchainTriggerMessageSet CreateSyncWorkflowTriggerSet(uint workflowCorrelationId);

		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowTriggerServerReplySet(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowFinishSyncSet(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlock(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlockInfo(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlock(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlockInfo(TargettedHeader triggerHeader = null);

		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigest(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigest(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigestFile(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigestFile(TargettedHeader triggerHeader = null);

		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigestInfo(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigestInfo(TargettedHeader triggerHeader = null);

		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlockSliceHashes(TargettedHeader triggerHeader = null);
		ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlockSliceHashes(TargettedHeader triggerHeader = null);
	}

	public abstract class ChainSyncMessageFactory<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY, CLOSE_CONNECTION, REQUEST_BLOCK, SEND_BLOCK, REQUEST_BLOCK_INFO, SEND_BLOCK_INFO, REQUEST_DIGEST, SEND_DIGEST, REQUEST_DIGEST_FILE, SEND_DIGEST_FILE, REQUEST_DIGEST_INFO, SEND_DIGEST_INFO, REQUEST_BLOCK_SLICE_HASHES, SEND_BLOCK_SLICE_HASHES> : ChainMessageFactory, IChainSyncMessageFactory
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger, new()
		where SERVER_TRIGGER_REPLY : ServerTriggerReply, new()
		where CLOSE_CONNECTION : FinishSync, new()
		where REQUEST_BLOCK : ClientRequestBlock, new()
		where SEND_BLOCK : ServerSendBlock, new()
		where REQUEST_BLOCK_INFO : ClientRequestBlockInfo, new()
		where SEND_BLOCK_INFO : ServerSendBlockInfo, new()
		where REQUEST_DIGEST : ClientRequestDigest, new()
		where SEND_DIGEST : ServerSendDigest, new()
		where REQUEST_DIGEST_FILE : ClientRequestDigestFile, new()
		where SEND_DIGEST_FILE : ServerSendDigestFile, new()
		where REQUEST_DIGEST_INFO : ClientRequestDigestInfo, new()
		where SEND_DIGEST_INFO : ServerSendDigestInfo, new()
		where REQUEST_BLOCK_SLICE_HASHES : ClientRequestBlockSliceHashes, new()
		where SEND_BLOCK_SLICE_HASHES : ServerRequestBlockSliceHashes, new() {

		public ChainSyncMessageFactory(IMainChainMessageFactory mainChainMessageFactory, BlockchainServiceSet serviceSet) : base(mainChainMessageFactory, serviceSet) {
		}

		public override ITargettedMessageSet<IBlockchainEventsRehydrationFactory> Rehydrate(SafeArrayHandle data, TargettedHeader header, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			using IDataRehydrator dr = DataSerializationFactory.CreateRehydrator(data);

			using SafeArrayHandle messageBytes = NetworkMessageSet.ExtractMessageBytes(dr);
			NetworkMessageSet.ResetAfterHeader(dr);

			using IDataRehydrator messageRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);

			ITargettedMessageSet<IBlockchainEventsRehydrationFactory> messageSet = null;

			try {
				if(data?.Length == 0) {
					throw new ApplicationException("null message");
				}

				short workflowType = 0;
				ComponentVersion<SimpleUShort> version = null;

				messageRehydrator.Peek(rehydrator => {
					workflowType = rehydrator.ReadShort();

					if(workflowType != WorkflowIDs.CHAIN_SYNC) {
						throw new ApplicationException("Invalid workflow type");
					}

					version = rehydrator.Rehydrate<ComponentVersion<SimpleUShort>>();
				});

				switch(version.Type.Value) {
					case ChainSyncMessageFactoryIds.TRIGGER_ID:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowTriggerSet(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SERVER_TRIGGER_REPLY:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowTriggerServerReplySet(header);
						}

						break;

					case ChainSyncMessageFactoryIds.FINISH_SYNC:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowFinishSyncSet(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_BLOCK:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestBlock(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_BLOCK:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendBlock(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_BLOCK_INFO:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestBlockInfo(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_BLOCK_INFO:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendBlockInfo(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_BLOCK_SLICE_HASHES:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestBlockSliceHashes(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_BLOCK_SLICE_HASHES:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendBlockSliceHashes(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_DIGEST:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestDigest(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_DIGEST:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendDigest(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_DIGEST_FILE:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestDigestFile(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_DIGEST_FILE:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendDigestFile(header);
						}

						break;

					case ChainSyncMessageFactoryIds.REQUEST_DIGEST_INFO:

						if(version == (1, 0)) {
							messageSet = this.CreateSyncWorkflowRequestDigestInfo(header);
						}

						break;

					case ChainSyncMessageFactoryIds.SEND_DIGEST_INFO:

						if(version == (1, 0)) {
							messageSet = this.CreateServerSendDigestInfo(header);
						}

						break;

					default:

						throw new ApplicationException("invalid message type");
				}

				if(messageSet?.BaseMessage == null) {
					throw new ApplicationException("Invalid message type or major");
				}

				messageSet.Header = header; // set the header explicitely
				messageSet.RehydrateRest(dr, rehydrationFactory);
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Invalid data sent");
			}

			return messageSet;

		}

	#region Explicit Creation methods

		/// <summary>
		///     this is the client side trigger method, when we build a brand new one
		/// </summary>
		/// <param name="workflowCorrelationId"></param>
		/// <returns></returns>
		public IBlockchainTriggerMessageSet CreateSyncWorkflowTriggerSet(uint workflowCorrelationId) {
			BlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> messageSet = this.mainChainMessageFactory.CreateTriggerMessageSet<CHAIN_SYNC_TRIGGER>(workflowCorrelationId);

			return messageSet;
		}

		private IBlockchainTriggerMessageSet CreateSyncWorkflowTriggerSet(TargettedHeader triggerHeader = null) {
			BlockchainTriggerMessageSet<CHAIN_SYNC_TRIGGER> messageSet = this.mainChainMessageFactory.CreateTriggerMessageSet<CHAIN_SYNC_TRIGGER>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowTriggerServerReplySet(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SERVER_TRIGGER_REPLY> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SERVER_TRIGGER_REPLY>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowFinishSyncSet(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<CLOSE_CONNECTION> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<CLOSE_CONNECTION>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlock(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_BLOCK> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_BLOCK>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlock(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_BLOCK> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_BLOCK>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlockInfo(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_BLOCK_INFO> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_BLOCK_INFO>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlockInfo(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_BLOCK_INFO> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_BLOCK_INFO>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigest(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_DIGEST> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_DIGEST>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigest(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_DIGEST> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_DIGEST>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigestFile(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_DIGEST_FILE> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_DIGEST_FILE>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigestFile(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_DIGEST_FILE> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_DIGEST_FILE>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestDigestInfo(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_DIGEST_INFO> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_DIGEST_INFO>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendDigestInfo(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_DIGEST_INFO> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_DIGEST_INFO>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateSyncWorkflowRequestBlockSliceHashes(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<REQUEST_BLOCK_SLICE_HASHES> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<REQUEST_BLOCK_SLICE_HASHES>(triggerHeader);

			return messageSet;
		}

		public ITargettedMessageSet<IBlockchainEventsRehydrationFactory> CreateServerSendBlockSliceHashes(TargettedHeader triggerHeader = null) {
			BlockchainTargettedMessageSet<SEND_BLOCK_SLICE_HASHES> messageSet = this.mainChainMessageFactory.CreateTargettedMessageSet<SEND_BLOCK_SLICE_HASHES>(triggerHeader);

			return messageSet;
		}

	#endregion

	}
}