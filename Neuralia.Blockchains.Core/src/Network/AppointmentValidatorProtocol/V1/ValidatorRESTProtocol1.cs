using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using RestSharp;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1 {
	public class ValidatorRESTProtocol1 : IValidatorProtocol {

		private BlockchainType blockchainType;
		private readonly int port;
		public ValidatorRESTProtocol1(BlockchainType blockchainType, int port = GlobalsService.DEFAULT_VALIDATOR_HTTP_PORT, int timeout = 10000) {
			this.port = port;
			this.Initialize(blockchainType, null, timeout);
		}
		
		public void Initialize(BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate = null, int timeout = 10000) {
			this.blockchainType = blockchainType;
		}

		public Task<bool> HandleServerExchange(ValidatorConnectionSet connectionSet, ByteArray operationBytes, CancellationToken? ct = null) {
			throw new NotImplementedException();
		}

		private string GetUrl(IPAddress address) {
			return new UriBuilder("http", address.ToString(), this.port).Uri.ToString();
		}

		public async Task<(SafeArrayHandle validatorCode, bool hasConnected)> RequestCodeTranslation(DateTime appointment, int index, SafeArrayHandle validatorCode, IPAddress address, int? port = null) {

			bool hasConnected = false;
			SafeArrayHandle resultValidatorCode = null;
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("chainId", this.blockchainType.Value);
			parameters.Add("appointment", appointment.Ticks);
			parameters.Add("index", index);
			parameters.Add("validatorCode", validatorCode.ToBase64());

			IRestResponse result = await restUtility.Post(GetUrl(address), "appointments/handle-code-translation", parameters).ConfigureAwait(false);

			hasConnected = result.StatusCode != 0;
			// ok, check the result
			if(result.StatusCode == HttpStatusCode.OK) {

				if(!string.IsNullOrWhiteSpace(result.Content)) {
					resultValidatorCode = SafeArrayHandle.FromBase64(result.Content);
				}
			}

			return (resultValidatorCode, hasConnected);
		}
		
		public async Task<(int secretCodeL2, bool hasConnected)> TriggerSession(DateTime appointment, int index, int code, IPAddress address, int? port = null) {
			bool hasConnected = false;
			int secretCodeL2 = 0;
			
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("chainId", this.blockchainType.Value);
			parameters.Add("appointment", appointment.Ticks);
			parameters.Add("index", index);
			parameters.Add("secretCode", code);

			IRestResponse result = await restUtility.Post(GetUrl(address), "appointments/handle-trigger-session", parameters).ConfigureAwait(false);

			hasConnected = result.StatusCode != 0;
			// ok, check the result
			if(result.StatusCode == HttpStatusCode.OK) {

				if(!string.IsNullOrWhiteSpace(result.Content)) {
					secretCodeL2 = int.Parse(result.Content);
				}
			}

			return (secretCodeL2, hasConnected);
		}

		public async Task<(bool completed, bool hasConnected)> RecordPuzzleCompleted(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, IPAddress address, int? port = null) {
			bool hasConnected = false;
			bool success = false;
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("chainId", this.blockchainType.Value);
			parameters.Add("appointment", appointment.Ticks);
			parameters.Add("index", index);

			var ordered = results.OrderBy(e => e.Key);
			parameters.Add("resultKeys", JsonSerializer.Serialize(ordered.Select(e => (int)e.Key).ToArray()));
			parameters.Add("results", JsonSerializer.Serialize(ordered.Select(e => e.Value.ToBase64()).ToArray()));

			IRestResponse result = await restUtility.Post(GetUrl(address), "appointments/handle-puzzle-completed", parameters).ConfigureAwait(false);

			hasConnected = result.StatusCode != 0;
			// ok, check the result
			if(result.StatusCode == HttpStatusCode.OK) {

				if(!string.IsNullOrWhiteSpace(result.Content)) {
					success = bool.Parse(result.Content);
				}
			}

			return (success, hasConnected);
		}

		public async Task<(bool completed, bool hasConnected)> RecordTHSCompleted(DateTime appointment, int index, SafeArrayHandle thsResults, IPAddress address, int? port = null) {
			bool hasConnected = false;
			bool success = false;
			RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);

			Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("chainId", this.blockchainType.Value);
			parameters.Add("appointment", appointment.Ticks);
			parameters.Add("index", index);
			parameters.Add("results", thsResults.ToBase64());

			IRestResponse result = await restUtility.Post(GetUrl(address), "appointments/handle-ths-completed", parameters).ConfigureAwait(false);

			hasConnected = result.StatusCode != 0;
			// ok, check the result
			if(result.StatusCode == HttpStatusCode.OK) {

				if(!string.IsNullOrWhiteSpace(result.Content)) {
					success = bool.Parse(result.Content);
				}
			}

			return (success, hasConnected);
		}
	}
}