using Neuralia.Blockchains.Core.DataAccess;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.Services {
	public interface IDataAccessService {
		IDalCreationFactory DalCreationFactory { get; }

		IMessageRegistryDal CreateMessageRegistryDal(string folderPath, ServiceSet serviceSet);
	}

	/// <summary>
	///     the main interface for all database accesses.
	/// </summary>
	public class DataAccessService : IDataAccessService {

		protected readonly IDalCreationFactory dalCreationFactory;

		private IMessageRegistryDal messageRegistryDal;

		public DataAccessService() {
			this.dalCreationFactory = this.CreateDalCreationFactory();

		}

		public virtual IMessageRegistryDal CreateMessageRegistryDal(string folderPath, ServiceSet serviceSet) {
			if(this.messageRegistryDal == null) {
				this.messageRegistryDal = this.dalCreationFactory.CreateMessageRegistryDAL(folderPath, serviceSet);
			}

			return this.messageRegistryDal;
		}

		public IDalCreationFactory DalCreationFactory => this.dalCreationFactory;

		protected virtual IDalCreationFactory CreateDalCreationFactory() {
			return new DalCreationFactory();
		}
	}
}