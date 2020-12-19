using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages {

	
	public interface IAppointmentRequestConfirmationGossipMessageSqlite : IAppointmentGossipMessageBaseSqlite , IAppointmentRequestConfirmationGossipMessage{
		
	}
	
	public class AppointmentRequestConfirmationGossipMessageSqlite : AppointmentGossipMessageBaseSqlite, IAppointmentRequestConfirmationGossipMessageSqlite {


	}
}