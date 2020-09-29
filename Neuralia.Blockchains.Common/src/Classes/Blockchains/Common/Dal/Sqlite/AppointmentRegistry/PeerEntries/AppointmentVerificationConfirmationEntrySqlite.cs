using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.PeerEntries {

	
	public interface IAppointmentVerificationConfirmationEntrySqlite : IAppointmentPeerEntryBaseSqlite, IAppointmentVerificationConfirmationEntry {
		
	}
	
	public class AppointmentVerificationConfirmationEntrySqlite : AppointmentPeerEntryBaseSqlite, IAppointmentVerificationConfirmationEntrySqlite {
		
	}
}