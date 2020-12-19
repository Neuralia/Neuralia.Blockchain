using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections {

	public interface IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		IElectionProcessor InstantiateProcessor(BlockElectionDistillate blockElectionDistillate, CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider);
		IElectionProcessor InstantiateProcessor(ElectedCandidateResultDistillate electedCandidateResultDistillate, CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider);
	}

	public abstract class ElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IElectionProcessorFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public IElectionProcessor InstantiateProcessor(BlockElectionDistillate blockElectionDistillate, CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider) {

			return this.InstantiateProcessor(blockElectionDistillate.blockType, blockElectionDistillate.ElectionContext.Version, centralCoordinator, chainEventPoolProvider);
		}

		public IElectionProcessor InstantiateProcessor(ElectedCandidateResultDistillate electedCandidateResultDistillate, CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider) {
			return this.InstantiateProcessor(electedCandidateResultDistillate.MatureBlockType, electedCandidateResultDistillate.MatureElectionContextVersion, centralCoordinator, chainEventPoolProvider);
		}

		protected IElectionProcessor InstantiateProcessor(ComponentVersion<BlockType> blockType, ComponentVersion electionContextVersion, CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider) {
			if(blockType == (1, 0)) {
				if(electionContextVersion == (1, 0)) {
					return this.GetElectionProcessorV1(centralCoordinator, chainEventPoolProvider);
				}
			}

			throw new ApplicationException("Invalid election block versions");
		}

		protected abstract IElectionProcessor GetElectionProcessorV1(CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider);
	}
}