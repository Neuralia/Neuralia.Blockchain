#if NET5_0
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST {
	
	[Route("appointments-test")]
	[ApiController, RequestSizeLimit(AppointmentValidatorController.MAX_REQUEST_SIZE)]
	public class AppointmentValidatorTestController : Controller {

		private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates;
		public AppointmentValidatorTestController(ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates) {
			this.appointmentValidatorDelegates = appointmentValidatorDelegates;
		}

		[HttpGet("test"), RequestSizeLimit(AppointmentValidatorController.MAX_REQUEST_SIZE), Produces("text/plain")]
		[ProducesResponseType(400)]
		public IActionResult Test() {

			NLog.Default.Information("HTTP REST Validator ping message received. sending pong.");
			
			return this.Ok("1");
		}
	}
}
#endif