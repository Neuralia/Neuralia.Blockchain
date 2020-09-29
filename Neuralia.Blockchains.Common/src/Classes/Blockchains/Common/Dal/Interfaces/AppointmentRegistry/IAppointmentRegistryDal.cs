using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry {
	public interface IAppointmentRegistryDal : IDalInterfaceBase {

		Task InsertAppointmentContextGossipMessage(Guid messageUuid, DateTime appointment, long start, long end);
		Task<IAppointmentContextGossipMessage> GetAppointmentContext(long requesterIndex, DateTime appointment);
		
		Task InsertAppointmentTriggerGossipMessage(Guid messageUuid, DateTime appointment);
		Task<IAppointmentTriggerGossipMessage> GetAppointmentTrigger();

		
		Task InsertAppointmentRequestConfirmationMessage(List<Guid> requesterIds, Guid messageUuid, DateTime appointment);
		Task<IAppointmentResponseEntry> GetAppointmentRequestConfirmation(Guid requesterId, DateTime appointment);

		Task InsertAppointmentVerificationConfirmationMessage(List<Guid> requesterIds, Guid messageUuid, DateTime appointment);
		Task<IAppointmentVerificationConfirmationEntry> GetAppointmentVerificationConfirmations(Guid requesterId, DateTime appointment);


		Task<IAppointmentValidatorSession> GetAppointmentValidatorSession(DateTime appointment);
		Task<DateTime?> GetInRangeAppointments();
		Task<List<(DateTime appointment, TimeSpan window)>> GetAppointments();

		Task InsertAppointmentValidatorSession(IAppointmentValidatorSession appointmentValidatorSession);
		Task UpdateAppointmentValidatorSession(IAppointmentValidatorSession appointmentValidatorSession);
		
		Task<List<Guid>> ClearExpired();
		
		Task<IAppointmentRequesterResult> GetAppointmentRequesterResult(DateTime appointment, long index);
		Task InsertAppointmentRequesterResult(IAppointmentRequesterResult appointmentRequesterResult);
		Task UpdateAppointmentRequesterResult(IAppointmentRequesterResult appointmentRequesterResult);
		 
		Task<List<IAppointmentRequesterResult>> GetReadyAppointmentRequesterResult();
		Task ClearReadyAppointmentRequesterResult(List<int> ids);
	}
}