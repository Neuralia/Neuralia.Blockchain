using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.PeerEntries {

	public interface IAppointmentPeerEntryBaseSqlite : IAppointmentPeerEntryBase {
		
	}
	
	public abstract class AppointmentPeerEntryBaseSqlite : IAppointmentPeerEntryBaseSqlite {

		public Guid RequesterId { get; set; }
		public DateTime Appointment { get; set; }
		public Guid MessageUuid { get; set; }
	}
}