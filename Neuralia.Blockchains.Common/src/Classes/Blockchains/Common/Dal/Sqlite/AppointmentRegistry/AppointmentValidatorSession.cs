using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry {
	
	public interface IAppointmentValidatorSessionSqlite : IAppointmentValidatorSession {
		
	}
	
	public class AppointmentValidatorSessionSqlite : IAppointmentValidatorSessionSqlite{

		public int Id { get; set; }
		public DateTime Appointment { get; set; }
		public int Window { get; set; }
		public Guid ValidatorHash { get; set; }
		public string Indices { get; set; }
		public string SecretCodes { get; set; }
		public DateTime Dispatch { get; set; }
	}
}