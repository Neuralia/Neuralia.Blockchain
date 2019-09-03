using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.PeerRegistry;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.PeerRegistry {
	public interface IPeerRegistrySqliteDal : ISqliteDal<IPeerRegistrySqliteContext>, IPeerRegistryDal {
	}

	public class PeerRegistrySqliteDal : SqliteDal<PeerRegistrySqliteContext>, IPeerRegistrySqliteDal {
		public PeerRegistrySqliteDal(string folderPath, ServiceSet serviceSet, IDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, st => (PeerRegistrySqliteContext) chainDalCreationFactory.CreatePeerRegistryContext(st), serializationType) {
		}
	}
}