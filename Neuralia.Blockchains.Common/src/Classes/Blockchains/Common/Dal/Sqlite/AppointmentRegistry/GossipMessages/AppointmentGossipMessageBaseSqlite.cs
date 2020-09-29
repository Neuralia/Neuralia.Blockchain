using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages {
	public interface IAppointmentGossipMessageBaseSqlite : IAppointmentGossipMessageBase {
		
	}
	
	public abstract class AppointmentGossipMessageBaseSqlite : IAppointmentGossipMessageBaseSqlite{

		public DateTime Appointment { get; set; }
		public Guid MessageUuid { get; set; }
	}
}