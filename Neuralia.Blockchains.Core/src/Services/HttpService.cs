using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Services {
	public interface IHttpService {
		void Download(string requestUri, string filename);
		SafeArrayHandle Download(string requestUri);
	}

	public class HttpService : IHttpService {

		private const int LARGE_BUFFER_SIZE = 4096;

		private readonly AppSettingsBase appSettingsBase;

		private IWebProxy proxy;

		public HttpService(AppSettingsBase appSettings) {
			this.appSettingsBase = appSettings;
		}

		public void Download(string requestUri, string filename) {
			if(requestUri == null) {
				throw new ArgumentNullException(nameof(requestUri));
			}

			this.Download(new Uri(requestUri), filename);
		}

		public SafeArrayHandle Download(string requestUri) {
			if(requestUri == null) {
				throw new ArgumentNullException(nameof(requestUri));
			}

			return this.Download(new Uri(requestUri));
		}

		private IWebProxy GetProxy() {

			if(this.proxy == null) {
				this.proxy = new WebProxy {
					Address = new Uri($"{this.appSettingsBase.ProxySettings.Host}:{this.appSettingsBase.ProxySettings.Port}"), BypassProxyOnLocal = true, UseDefaultCredentials = false,

					// *** These creds are given to the proxy server, not the web server ***
					Credentials = new NetworkCredential(this.appSettingsBase.ProxySettings.User, this.appSettingsBase.ProxySettings.Password)
				};
			}

			return this.proxy;
		}

		private void Download(Uri requestUri, string filename) {
			if(filename == null) {
				throw new ArgumentNullException(nameof(filename));
			}

			using(FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, LARGE_BUFFER_SIZE, true)) {

				this.Download(requestUri, fileStream);
			}
		}

		private SafeArrayHandle Download(Uri requestUri) {

			using(MemoryStream memoryStream = new MemoryStream()) {

				this.Download(requestUri, memoryStream);

				return ByteArray.WrapAndOwn( memoryStream.ToArray());
			}
		}

		private void Download(Uri requestUri, Stream outputStream) {

			Repeater.Repeat(() => {

				HttpClientHandler httpClientHandler = new HttpClientHandler();

				if(this.appSettingsBase.ProxySettings != null) {
					httpClientHandler.Proxy = this.GetProxy();
					httpClientHandler.PreAuthenticate = true;
					httpClientHandler.UseDefaultCredentials = true;

					// *** These creds are given to the web server, not the proxy server ***
					//				httpClientHandler.Credentials = new NetworkCredential(
					//					userName: serverUserName,
					//					password: serverPassword);
				}

				using(HttpClient httpClient = new HttpClient(httpClientHandler, true)) {

					using(HttpResponseMessage response = httpClient.GetAsync(requestUri).Result) {
						using(Stream stream = response.Content.ReadAsStreamAsync().Result) {
							stream.CopyTo(outputStream);
							stream.Flush();
						}

					}
				}
			});

		}
	}
}