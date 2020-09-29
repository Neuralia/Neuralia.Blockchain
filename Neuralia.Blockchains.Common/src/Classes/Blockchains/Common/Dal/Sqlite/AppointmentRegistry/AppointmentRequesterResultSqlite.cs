using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry {

	public interface IIAppointmentRequesterResultSqlite : IAppointmentRequesterResult {
		
	}
	
	
	public class AppointmentRequesterResultSqlite: IIAppointmentRequesterResultSqlite {

		public int Id { get; set; }
		public long Index { get; set; }
		public DateTime Appointment { get; set; }
		public DateTime? RequestedCode { get; set; }
		public byte[] ValidatorCode { get; set; }
		public int SecretCode { get; set; }

		public DateTime? Trigger { get; set; }
		public DateTime? Completed { get; set; }
		public byte[] Results { get; set; }
		public bool Valid { get; set; }
		public bool Sent { get; set; }
	}
}