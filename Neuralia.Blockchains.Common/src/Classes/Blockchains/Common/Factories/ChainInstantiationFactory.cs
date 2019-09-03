using System;
using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories {

	public interface IChainInstantiationFactory {
	}

	public abstract class ChainInstantiationFactory<CHAIN_CREATION_FACTORY_IMPLEMENTATION, CHAIN_INTERFACE, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, CHAIN_COMPONENT_INJECTION> : IChainInstantiationFactory
		where CHAIN_CREATION_FACTORY_IMPLEMENTATION : IChainInstantiationFactory, new()
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_INTERFACE : IBlockChainInterface
		where CHAIN_COMPONENT_INJECTION : IChainComponentsInjection<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		static ChainInstantiationFactory() {
		}

		public static CHAIN_CREATION_FACTORY_IMPLEMENTATION Instance { get; } = new CHAIN_CREATION_FACTORY_IMPLEMENTATION();

		/// <summary>
		///     The method that will create a new instance of our entire chain!
		/// </summary>
		/// <returns></returns>
		public abstract CHAIN_INTERFACE CreateNewChain(IServiceProvider serviceProvider, ChainRuntimeConfiguration chainRuntimeConfiguration = null, IFileSystem fileSystem = null);

		/// <summary>
		///     the main method which creates our central controller, which in turn will create the rest
		/// </summary>
		/// <returns></returns>
		protected abstract CENTRAL_COORDINATOR CreateCentralCoordinator(BlockchainServiceSet serviceSet, ChainRuntimeConfiguration chainRuntimeConfiguration, IFileSystem fileSystem);

		/// <summary>
		///     This is the method where we create all injected chain components
		/// </summary>
		/// <returns></returns>
		protected abstract CHAIN_COMPONENT_INJECTION CreateChainComponents(CENTRAL_COORDINATOR centralCoordinator, CHAIN_INTERFACE chainInterface);
	}
}