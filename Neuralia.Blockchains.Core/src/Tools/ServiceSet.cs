using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Core.Tools {

	public class ServiceSet {

		protected readonly BlockchainType chainType;

		public ServiceSet(BlockchainType chainType) {
			this.chainType = chainType;
		}

		public BlockchainType ChainType => this.chainType;

		public ITimeService TimeService => this.GetService<ITimeService>();
		public IGuidService GuidService => this.GetService<IGuidService>();
		public IGlobalsService GlobalsService => this.GetService<IGlobalsService>();

		public IHttpService HttpService => this.GetService<IHttpService>();
		public IFileFetchService FileFetchService => this.GetService<IFileFetchService>();

		public IDataAccessService DataAccessService => this.GetService<IDataAccessService>();

		public IInstantiationService InstantiationService => this.GetService<IInstantiationService>();

		public IPortMappingService PortMappingService => this.GetService<IPortMappingService>();
		
		public T GetService<T>()
			where T : class {
			return DIService.Instance.GetService<T>(this.chainType);
		}
	}

	/// <summary>
	///     A simple class to hold all the services we may need scopped to the chain
	/// </summary>
	public class ServiceSet<R> : ServiceSet
		where R : IRehydrationFactory {

		public ServiceSet(BlockchainType chainType) : base(chainType) {

		}

		public new IInstantiationService<R> InstantiationService => this.GetService<IInstantiationService>() as IInstantiationService<R>;
	}
}