#if NET5_0
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST {
	
	[Route("appointments")]
	[ApiController, RequestSizeLimit(MAX_REQUEST_SIZE)]
	public class AppointmentValidatorController : Controller {

		public const int MAX_REQUEST_SIZE = 700;
		private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates;
		private readonly IActionContextAccessor actionContextAccessor;
		public AppointmentValidatorController(IActionContextAccessor actionContextAccessor, ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates) {
			this.actionContextAccessor = actionContextAccessor;
			this.appointmentValidatorDelegates = appointmentValidatorDelegates;
		}

		protected virtual bool CheckShouldDisconnect(IPAddress address) {
			if(!GlobalSettings.ApplicationSettings.EnableAppointmentValidatorIPMarshall) {
				return false;
			}

			return IPMarshall.ValidationInstance.RequestIncomingConnectionClearance(address) == false;
		}
		
		[HttpPost("handle-code-translation"), Produces("text/plain"), RequestSizeLimit(MAX_REQUEST_SIZE)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> HandleWebCodeTranslationWorkflow([FromForm] HandleCodeTranslationWorkflowModel model) {

			IPAddress remoteIpAddress = this.actionContextAccessor.ActionContext.HttpContext.Connection.RemoteIpAddress;

			if(this.CheckShouldDisconnect(remoteIpAddress)) {
				return this.BadRequest();
			}
			if(model.ChainId == 0 || model.Appointment == 0 || model.Index == 0 || string.IsNullOrWhiteSpace(model.ValidatorCode)) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				return this.BadRequest();
			}
			
			if(!this.appointmentValidatorDelegates.TryGetValue(model.ChainId, out var validatorDelegate) || validatorDelegate == null) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				return this.BadRequest();
			}
			
			string returnResult = "";
			bool operationValid = false;
			bool operationNull = false;
			
			ValidatorProtocol1.CodeTranslationRequestOperation operation = new ValidatorProtocol1.CodeTranslationRequestOperation();

			try {

				operation.Appointment = new DateTime(model.Appointment, DateTimeKind.Utc);
				operation.Index = model.Index;
				operation.ValidatorCode = SafeArrayHandle.FromBase64(model.ValidatorCode);
			
				ValidatorProtocol1.CodeTranslationResponseOperation resultOperation = null;

				(resultOperation, operationValid) = await validatorDelegate.HandleCodeTranslationWorkflow(operation).ConfigureAwait(false);

				operationNull = resultOperation == null;
			
				if(!operationNull) {
					returnResult = resultOperation.ValidatorCode.ToBase64();
				}

			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return this.Ok(returnResult);
		}
		
		[HttpPost("handle-trigger-session"), Produces("text/plain"), RequestSizeLimit(MAX_REQUEST_SIZE)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> HandleWebTriggerSessionWorkflow([FromForm] HandleTriggerSessionWorkflowModel model) {

			IPAddress remoteIpAddress = this.actionContextAccessor.ActionContext.HttpContext.Connection.RemoteIpAddress;
			if(this.CheckShouldDisconnect(remoteIpAddress)) {
				return this.BadRequest();
			}
			
			if(model.ChainId == 0 || model.Appointment == 0 || model.Index == 0 || model.SecretCode == 0) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTriggerSessionWorkflow)}");
				return this.BadRequest();
			}
			if(!this.appointmentValidatorDelegates.TryGetValue(model.ChainId, out var validatorDelegate) || validatorDelegate == null) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTriggerSessionWorkflow)}");
				return this.BadRequest();
			}
			
			string returnResult = "";
			bool operationValid = false;
			bool operationNull = false;
			
			ValidatorProtocol1.TriggerSessionOperation operation = new ValidatorProtocol1.TriggerSessionOperation();

			try {
				
				operation.Appointment = new DateTime(model.Appointment, DateTimeKind.Utc);
				operation.Index = model.Index;
				operation.SecretCode = model.SecretCode;
			
				ValidatorProtocol1.TriggerSessionResponseOperation resultOperation = null;

				(resultOperation, operationValid) = await validatorDelegate.HandleTriggerSessionWorkflow(operation).ConfigureAwait(false);

				operationNull = resultOperation == null;
			
				if(!operationNull) {
					returnResult = resultOperation.SecretCodeL2.ToString();
				}
			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTriggerSessionWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return this.Ok(returnResult);
		}
		
		[HttpPost("handle-puzzle-completed"), Produces("text/plain"), RequestSizeLimit(MAX_REQUEST_SIZE)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> HandleWebPuzzleCompletedWorkflow([FromForm] HandlePuzzleCompletedWorkflowModel model) {

			IPAddress remoteIpAddress = this.actionContextAccessor.ActionContext.HttpContext.Connection.RemoteIpAddress;
			if(this.CheckShouldDisconnect(remoteIpAddress)) {
				return this.BadRequest();
			}
			
			if(model.ChainId == 0 || model.Appointment == 0 || model.Index == 0 || string.IsNullOrWhiteSpace(model.ResultKeys) || string.IsNullOrWhiteSpace(model.Results)) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebPuzzleCompletedWorkflow)}");
				return this.BadRequest();
			}
			
			var resultKeys = JsonSerializer.Deserialize<int[]>(model.ResultKeys);
			var results = JsonSerializer.Deserialize<string[]>(model.Results);

			if(resultKeys.Length == 0 || results.Length == 0 || resultKeys.Length != results.Length || results.Any(e => string.IsNullOrWhiteSpace(e))) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebPuzzleCompletedWorkflow)}");
				return this.BadRequest();
			}
			if(!this.appointmentValidatorDelegates.TryGetValue(model.ChainId, out var validatorDelegate) || validatorDelegate == null) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebPuzzleCompletedWorkflow)}");
				return this.BadRequest();
			}

			string returnResult = "";
			bool operationValid = false;
			bool operationNull = false;
			
			ValidatorProtocol1.PuzzleCompletedOperation operation = new ValidatorProtocol1.PuzzleCompletedOperation();

			try {
				
				operation.Appointment = new DateTime(model.Appointment, DateTimeKind.Utc);
				operation.Index = model.Index;

				for(int i = 0; i < resultKeys.Length; i++) {
					operation.Results.Add((Enums.AppointmentsResultTypes)resultKeys[i], SafeArrayHandle.FromBase64(results[i]));
				}
			
				ValidatorProtocol1.PuzzleCompletedResponseOperation resultOperation = null;

				(resultOperation, operationValid) = await validatorDelegate.HandlePuzzleCompletedWorkflow(operation).ConfigureAwait(false);

				operationNull = resultOperation == null;
			
				if(!operationNull) {
					returnResult = resultOperation.Result.ToString();
				}


			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebPuzzleCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return this.Ok(returnResult);
		}
		
		[HttpPost("handle-ths-completed"), Produces("text/plain"), RequestSizeLimit(MAX_REQUEST_SIZE)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> HandleWebTHSCompletedWorkflow([FromForm] HandleTHSCompletedWorkflowModel model) {

			IPAddress remoteIpAddress = this.actionContextAccessor.ActionContext.HttpContext.Connection.RemoteIpAddress;
			
			if(this.CheckShouldDisconnect(remoteIpAddress)) {
				return this.BadRequest();
			}
			
			if(model.ChainId == 0 || model.Appointment == 0 || model.Index == 0 || string.IsNullOrWhiteSpace(model.Results)) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTHSCompletedWorkflow)}");
				return this.BadRequest();
			}
			if(!this.appointmentValidatorDelegates.TryGetValue(model.ChainId, out var validatorDelegate) || validatorDelegate == null) {
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTHSCompletedWorkflow)}");
				return this.BadRequest();
			}
			
			string returnResult = "";
			bool operationValid = false;
			bool operationNull = false;
			
			ValidatorProtocol1.THSCompletedOperation operation = new ValidatorProtocol1.THSCompletedOperation();

			try {
				
				operation.Appointment = new DateTime(model.Appointment, DateTimeKind.Utc);
				operation.Index = model.Index;
				operation.THSResults.Entry = SafeArrayHandle.FromBase64(model.Results).Entry;
			
				ValidatorProtocol1.THSCompletedResponseOperation resultOperation = null;
				
				(resultOperation, operationValid) = await validatorDelegate.HandleTHSCompletedWorkflow(operation).ConfigureAwait(false);

				operationNull = resultOperation == null;
			
				if(!operationNull) {
					returnResult = resultOperation.Result.ToString();
				}



			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleWebCodeTranslationWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				ValidatorProtocol1.BanForADay(remoteIpAddress, $"failed in {nameof(this.HandleWebTHSCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return this.Ok(returnResult);
		}
	}
}
#endif