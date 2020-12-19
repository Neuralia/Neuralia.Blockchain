using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.PeerEntries {
	
	
	public interface IAppointmentResponseEntrySqlite : IAppointmentPeerEntryBaseSqlite, IAppointmentResponseEntry {
		
	}
	
	public class AppointmentResponseEntrySqlite : AppointmentPeerEntryBaseSqlite, IAppointmentResponseEntrySqlite {
		
	}
}