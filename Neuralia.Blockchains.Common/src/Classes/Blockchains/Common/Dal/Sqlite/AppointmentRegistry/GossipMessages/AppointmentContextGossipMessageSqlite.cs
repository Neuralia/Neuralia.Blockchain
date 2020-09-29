using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages {

	public interface IAppointmentContextGossipMessageSqlite : IAppointmentGossipMessageBaseSqlite, IAppointmentContextGossipMessage {
		
	}
	
	public class AppointmentContextGossipMessageSqlite : AppointmentGossipMessageBaseSqlite, IAppointmentContextGossipMessageSqlite {

		public long Start { get; set; }
		public long End { get; set; }
	}
}