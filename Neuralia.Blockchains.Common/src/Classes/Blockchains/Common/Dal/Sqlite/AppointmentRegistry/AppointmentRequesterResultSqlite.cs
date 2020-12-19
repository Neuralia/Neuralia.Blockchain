using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry {

	public interface IIAppointmentRequesterResultSqlite : IAppointmentRequesterResult {
		
	}
	
	
	public class AppointmentRequesterResultSqlite: IIAppointmentRequesterResultSqlite {

		public int Id { get; set; }
		public int Index { get; set; }
		public DateTime Appointment { get; set; }
		public DateTime? RequestedCodeCompleted { get; set; }
		public byte[] ValidatorCode { get; set; }
		public int SecretCode { get; set; }

		public DateTime? TriggerCompleted { get; set; }
		public DateTime? PuzzleCompleted { get; set; }
		public DateTime? THSCompleted { get; set; }
		
		public byte[] PuzzleResults { get; set; }
		public byte[] THSResults { get; set; }
		
		public bool Valid { get; set; }
		public bool Sent { get; set; }
	}
}