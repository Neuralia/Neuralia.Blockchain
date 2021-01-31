using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx;
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

		public class RestParameterSet<T> {
			public Dictionary<string, object> parameters;
			public Dictionary<string, byte[]> files;
			public Func<string, T> transform;
			public int tries = 6;
		}

		public Task<(bool success, T result)> PerformSecureGet<T>(string url, string action, RestParameterSet<T> restParameterSet) {
			return this.PerformSecureCall(url, action, Method.GET, restParameterSet);
		}
		
		public Task<(bool success, T result)> PerformSecurePost<T>(string url, string action, RestParameterSet<T> restParameterSet) {
			return this.PerformSecureCall(url, action, Method.POST, restParameterSet);
		}
		
		public Task<(bool success, T result)> PerformSecurePut<T>(string url, string action, RestParameterSet<T> restParameterSet) {
			return this.PerformSecureCall(url, action, Method.PUT, restParameterSet);
		}

		public async Task<(bool success, T result)> PerformSecureCall<T>(string url, string action, Method method, RestParameterSet<T> restParameterSet) {

			AsyncManualResetEventSlim manualResetEventSlim = null;

			try {
				int tries = 0;
				int limit = restParameterSet.tries - 1;
				do {
					try {
						IRestResponse webResult = await this.PerformCall(url, action, method, restParameterSet.parameters, restParameterSet.files).ConfigureAwait(false);

						// ok, check the result
						if(webResult.StatusCode == HttpStatusCode.OK) {

							if(string.IsNullOrWhiteSpace(webResult.Content)) {
								return (true, default);
							}

							if(restParameterSet.transform == null) {

								if(webResult.Content is T stringResult) {
									return (true, stringResult);
								}

								throw new ApplicationException("Invalid format");
							}
							
							return (true, restParameterSet.transform(webResult.Content));
						} 
						else if(webResult.StatusCode == HttpStatusCode.NoContent) {
							return (true, default);
						}
						else if(webResult.StatusCode == HttpStatusCode.Forbidden) {

							if(manualResetEventSlim == null) {
								manualResetEventSlim = new AsyncManualResetEventSlim();
							}

							manualResetEventSlim.Reset();
							// ok, lets wait until rate limiting is passed
							await manualResetEventSlim.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
						}
						
					} catch(Exception ex) {
						if(tries == limit) {
							throw;
						}
					}

					tries++;
				} while(tries == limit);

				throw new ApplicationException($"Failed rest call for {action}");
			} finally {
				manualResetEventSlim?.Dispose();
			}
		}
		
		private Task<IRestResponse> PerformCall(string url, string action, Method method, Dictionary<string, object> parameters, Dictionary<string, byte[]> files = null) {

			RestClient client = new RestClient(url);
			RestRequest request = new RestRequest(action, method);
			if (this.appSettingsBase?.ProxySettings != null)
            {
				client.Proxy = this.GetProxy();
				client.PreAuthenticate = true;
				request.UseDefaultCredentials = true;
			}
			client.FollowRedirects = true;

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

		private IWebProxy proxy;
		private IWebProxy GetProxy()
		{

			if (this.proxy == null)
			{
				this.proxy = new WebProxy
				{
					Address = new Uri($"{this.appSettingsBase.ProxySettings.Host}:{this.appSettingsBase.ProxySettings.Port}"),
					BypassProxyOnLocal = true,
					UseDefaultCredentials = false,

					Credentials = new NetworkCredential(this.appSettingsBase.ProxySettings.User, this.appSettingsBase.ProxySettings.Password)
				};
			}

			return this.proxy;
		}
	}
}