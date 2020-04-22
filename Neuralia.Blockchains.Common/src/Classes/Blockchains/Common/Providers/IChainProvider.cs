using System.ComponentModel;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IChainProvider{

		public Task Initialize(LockContext lockContext);

		public Task PostInitialize();
	}
	
	public class ChainProvider : IChainProvider{

		public virtual Task Initialize(LockContext lockContext) {

			return Task.CompletedTask;
		}

		public virtual Task PostInitialize(){
			return Task.CompletedTask;
		}
	}
}