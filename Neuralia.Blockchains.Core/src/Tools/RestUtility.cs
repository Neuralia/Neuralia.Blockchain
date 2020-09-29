using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using RestSharp;

namespace Neuralia.Blockchains.Core.Tools {
	public interface IRestUtility
	{
		Task<IRestResponse> Put(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null);
		Task<IRestResponse> Post(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null);
		Task<IRestResponse> Get(string url, string action);
	}

	public class RestUtility : IRestUtility {
		
		public enum Modes {
			None,
			FormData,
			XwwwFormUrlencoded
		}

		private readonly AppSettingsBase appSettingsBase;

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

		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
		
		public Task<IRestResponse> Put(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			return this.PerformCall(url, action, Method.PUT, parameters, files);

		}

		public Task<IRestResponse> Post(string url, string action, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			return this.PerformCall(url, action, Method.POST, parameters, files);
		}

		public Task<IRestResponse> Get(string url, string action) {

			return this.PerformCall(url, action, Method.GET, null);
		}

		private Task<IRestResponse> PerformCall(string url, string action, Method method, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			RestClient client = new RestClient(url);
			client.FollowRedirects = true;

			RestRequest request = new RestRequest(action, method);

			request.AddHeader("Cache-control", "no-cache");

			if(this.mode == Modes.FormData) {
				request.AlwaysMultipartFormData = true;
			} else if(this.mode == Modes.XwwwFormUrlencoded) {
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
				foreach((string key, byte[] value) in files) {
					request.AddFile(key, value, key, "application/octet-stream");
				}
			}

			request.Timeout = (int)this.Timeout.TotalMilliseconds;

			return client.ExecuteAsync(request);

		}
	}
}