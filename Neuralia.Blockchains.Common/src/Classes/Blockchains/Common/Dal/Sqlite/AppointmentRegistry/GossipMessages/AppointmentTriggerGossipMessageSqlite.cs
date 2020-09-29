using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages {

	
	public interface IAppointmentTriggerGossipMessageSqlite : IAppointmentGossipMessageBaseSqlite, IAppointmentTriggerGossipMessage {
		
	}
	
	public class AppointmentTriggerGossipMessageSqlite : AppointmentGossipMessageBaseSqlite, IAppointmentTriggerGossipMessageSqlite {


	}
}