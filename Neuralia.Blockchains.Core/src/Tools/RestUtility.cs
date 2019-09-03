using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using RestSharp;

namespace Neuralia.Blockchains.Core.Tools {
	public class RestUtility {

		private readonly AppSettingsBase appSettingsBase;

		public RestUtility(AppSettingsBase appSettingsBase) {
			this.appSettingsBase = appSettingsBase;

			//#if DEBUG
			//			//TODO: this must ABSOLUTELY be removed for production!!!!!
			//			ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
			//#endif
		}

		public Task<IRestResponse> Put(string url, string action, Dictionary<string, object> parameters) {

			return this.PerformCall(url, action, Method.PUT, parameters);

		}

		public Task<IRestResponse> Post(string url, string action, Dictionary<string, object> parameters) {

			return this.PerformCall(url, action, Method.POST, parameters);
		}

		private Task<IRestResponse> PerformCall(string url, string action, Method method, Dictionary<string, object> parameters) {

			RestClient client = new RestClient(url);

			RestRequest request = new RestRequest(action, method);

			request.AddHeader("Cache-control", "no-cache");
			request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			if(parameters != null) {
				foreach(var entry in parameters) {
					request.AddParameter(entry.Key, entry.Value);
				}
			}

			request.Timeout = 3000; // 3 second

			return client.ExecuteTaskAsync(request);

		}
	}
}