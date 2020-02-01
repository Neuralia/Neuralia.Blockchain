using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using RestSharp;

namespace Neuralia.Blockchains.Core.Tools {
	public class RestUtility {

		private readonly AppSettingsBase appSettingsBase;

		public enum Modes {
			FormData, XwwwFormUrlencoded
		}

		private readonly Modes mode;

		public RestUtility(AppSettingsBase appSettingsBase, Modes mode = Modes.FormData) {
			this.appSettingsBase = appSettingsBase;

			this.mode = mode;

			//#if DEBUG
			//			//TODO: this must ABSOLUTELY be removed for production!!!!!
			//			ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
			//#endif
		}
		
		public RestUtility() {

		}

		public Task<IRestResponse> Put(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			return this.PerformCall(url, action, Method.PUT, parameters, files);

		}

		public Task<IRestResponse> Post(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			return this.PerformCall(url, action, Method.POST, parameters, files);
		}

		private Task<IRestResponse> PerformCall(string url, string action, Method method, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			RestClient client = new RestClient(url);
			client.FollowRedirects = true;
			
			RestRequest request = new RestRequest(action, method);
			
			request.AddHeader("Cache-control", "no-cache");
			
			if(this.mode == Modes.FormData) {
				request.AlwaysMultipartFormData = true;
			} else {
				request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
			}
			
			if(parameters != null) {
				foreach((string key, object value) in parameters) {
					if(this.mode == Modes.FormData) {
						request.AddParameter(key, value, "text/plain", ParameterType.GetOrPost);
					} else {
						request.AddParameter(key, value);
					}
					
				}
			}
			
			if(files != null) {
				foreach((string key, var value) in files) {
					request.AddFile(key, value, key, "application/octet-stream");
				}
			}

			request.Timeout = 3000; // 3 second

			return client.ExecuteTaskAsync(request);

		}
	}
}