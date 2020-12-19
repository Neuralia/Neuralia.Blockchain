using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages {

	
	public interface IAppointmentVerificationConfirmationGossipMessageSqlite : IAppointmentGossipMessageBaseSqlite, IAppointmentVerificationConfirmationGossipMessage {
		
	}
	
	public class AppointmentVerificationConfirmationGossipMessageSqlite : AppointmentGossipMessageBaseSqlite, IAppointmentVerificationConfirmationGossipMessageSqlite {


	}
}