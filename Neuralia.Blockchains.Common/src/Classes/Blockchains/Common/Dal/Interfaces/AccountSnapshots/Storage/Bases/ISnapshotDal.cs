using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases {
	public interface ISnapshotDal : IDalInterfaceBase {
	}

	public interface ISnapshotDal<SNAPSHOT_CONTEXT> : ISnapshotDal
		where SNAPSHOT_CONTEXT : ISnapshotContext {
	}
}