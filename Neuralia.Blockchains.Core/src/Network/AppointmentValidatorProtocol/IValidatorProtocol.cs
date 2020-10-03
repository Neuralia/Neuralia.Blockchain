using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public interface IValidatorProtocol {
		Task HandleServerExchange(ITcpValidatorConnection connection, CancellationToken ct);

		Task<SafeArrayHandle> RequestCodeTranslation(DateTime appointment, int index, SafeArrayHandle validatorCode, IPAddress address, int? port = null);
		Task<int>             TriggerSession(DateTime appointment, int index, int code, IPAddress address, int? port = null);
		Task<bool>            CompleteSession(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results , IPAddress address, int? port = null);
	}

}