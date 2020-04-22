using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest;
using Neuralia.Blockchains.Core.P2p.Workflows.MessageGroupManifest.Messages.V1;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Gossip.Metadata {
	public class MessageGroupGossipMetadataAnalyser<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IMessageGroupGossipMetadataAnalyser
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> centralCoordinator;

		public MessageGroupGossipMetadataAnalyser(CentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
		}

		public bool AnalyzeGossipMessageInfo(IGossipGroupMessageInfo gossipGroupMessageInfo) {
			if(gossipGroupMessageInfo?.GossipMessageMetadata == null) {
				// we dotn participate, so lets take the message
				return true;
			}

			if(gossipGroupMessageInfo.GossipMessageMetadata?.GossipMessageMetadataDetails is BlockGossipMessageMetadataDetails blockGossipMessageMetadataDetails) {

				if(GlobalSettings.ApplicationSettings.SynclessMode) {

					// mobile apps do not take blocks
					return false;
				}

				// ok, its a block gossip message. lets ensure the block is not too far away from our needs, otherwise we will just reject it	
				long currentBlockHeight = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight;
				int blockGossipCacheProximityLevel = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.BlockGossipCacheProximityLevel;

				var accept =  blockGossipMessageMetadataDetails.BlockId > currentBlockHeight && blockGossipMessageMetadataDetails.BlockId <= currentBlockHeight + blockGossipCacheProximityLevel;

				if (accept){
					// ok, here we decide to accept the block. because we dont want the sync to ALSO download the block, we will give it some time to complete by gossip.
					// here we attempt to acquire a lock, but we still accept it, because even if we have it, we want to propagate the message
					this.centralCoordinator.ChainComponentProvider.BlockchainProviderBase.AttemptLockBlockDownload(blockGossipMessageMetadataDetails.BlockId);
				}
				return accept;
			}
			else if(gossipGroupMessageInfo.GossipMessageMetadata?.GossipMessageMetadataDetails is TransactionGossipMessageMetadataDetails transactionGossipMessageMetadataDetails) {

				if(!transactionGossipMessageMetadataDetails.IsPresentation == false) {
					// its a regular transcation, jsut take it
					return true;
				}
				
				if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.AllowGossipPresentations) {
					// its a regular transcation, jsut take it
					return true;
				}
				
				// ok, its a presentation transaction. accept it only if we really want to
				return this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.AllowGossipPresentations;
			}
			
			return true;
		}
	}
}