using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Neuralia.Blockchains.Core.Services {
	public interface IDIService {
		T GetService<T>(BlockchainType chainType)
			where T : class;

		T GetService<T>()
			where T : class;

		void AddServiceProvider(BlockchainType chainType, IServiceProvider serviceProvider);
		List<IServiceProvider> GetServiceProviders();
	}

	public class DIService : IDIService {

		//TODO: replace this with a dictionary and key with chainType. this allows to scope the DI per chain, will help with unit testing
		private static volatile IDIService instance;
		private static readonly object syncRoot = new object();

		private readonly Dictionary<BlockchainType, List<IServiceProvider>> chainProviders = new Dictionary<BlockchainType, List<IServiceProvider>>();

		private DIService() {
		}

		public static void Reset() {
			instance = null;
		}
		
		public static IDIService Instance {
			get {
				if(instance == null) {
					lock(syncRoot) {
						if(instance == null) {
							instance = new DIService();
						}
					}
				}

				return instance;
			}
		}

		public void AddServiceProvider(BlockchainType chainType, IServiceProvider serviceProvider) {
			if(!this.chainProviders.ContainsKey(chainType)) {
				this.chainProviders.Add(chainType, new List<IServiceProvider>());
			}

			this.chainProviders[chainType].Add(serviceProvider);
		}

		public T GetService<T>(BlockchainType chainType)
			where T : class {
			try {
				if(!this.chainProviders.ContainsKey(chainType)) {
					return null;
				}

				if(this.chainProviders.ContainsKey(chainType)) {
					foreach(IServiceProvider serviceProvider in this.chainProviders[chainType]) {
						T result = serviceProvider.GetService<T>();

						if(result != null) {
							return result;
						}
					}
				}

				return null;
			} catch {
				Log.Warning($"Failed to find service {typeof(T).FullName} for chain {chainType}");
			}

			return null;
		}

		public T GetService<T>()
			where T : class {

			return this.GetService<T>(BlockchainTypes.Instance.None);

		}

		public List<IServiceProvider> GetServiceProviders() {

			if(!this.chainProviders.ContainsKey(BlockchainTypes.Instance.None)) {
				return null;
			}

			return this.chainProviders[BlockchainTypes.Instance.None];
		}
	}
}