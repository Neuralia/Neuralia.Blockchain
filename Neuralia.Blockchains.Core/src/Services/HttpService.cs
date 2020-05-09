using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Core.Services {
	public interface IHttpService {
		Task Download(string requestUri, string filename);
		Task<SafeArrayHandle> Download(string requestUri);
	}

	public class HttpService : IHttpService {

		private const int LARGE_BUFFER_SIZE = 4096;

		private readonly AppSettingsBase appSettingsBase;

		private IWebProxy proxy;

		public HttpService(AppSettingsBase appSettings) {
			this.appSettingsBase = appSettings;
		}

		public Task Download(string requestUri, string filename) {
			if(requestUri == null) {
				throw new ArgumentNullException(nameof(requestUri));
			}

			return this.Download(new Uri(requestUri), filename);
		}

		public Task<SafeArrayHandle> Download(string requestUri) {
			if(requestUri == null) {
				throw new ArgumentNullException(nameof(requestUri));
			}

			return this.Download(new Uri(requestUri));
		}

		private IWebProxy GetProxy() {

			if(this.proxy == null) {
				this.proxy = new WebProxy {
					Address = new Uri($"{this.appSettingsBase.ProxySettings.Host}:{this.appSettingsBase.ProxySettings.Port}"), BypassProxyOnLocal = true, UseDefaultCredentials = false,

					Credentials = new NetworkCredential(this.appSettingsBase.ProxySettings.User, this.appSettingsBase.ProxySettings.Password)
				};
			}

			return this.proxy;
		}

		private async Task Download(Uri requestUri, string filename) {
			if(filename == null) {
				throw new ArgumentNullException(nameof(filename));
			}

			await using(FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, LARGE_BUFFER_SIZE, true)) {

				await Download(requestUri, fileStream).ConfigureAwait(false);
			}
		}

		private async Task<SafeArrayHandle> Download(Uri requestUri) {

			await using(MemoryStream memoryStream = new MemoryStream()) {

				await Download(requestUri, memoryStream).ConfigureAwait(false);

				return ByteArray.WrapAndOwn(memoryStream.ToArray());
			}
		}

		private async Task Download(Uri requestUri, Stream outputStream) {

			try {
				await Repeater.RepeatAsync(async () => {

					HttpClientHandler httpClientHandler = new HttpClientHandler();

					if(appSettingsBase.ProxySettings != null) {
						httpClientHandler.Proxy = GetProxy();
						httpClientHandler.PreAuthenticate = true;
						httpClientHandler.UseDefaultCredentials = true;
					}

					using(HttpClient httpClient = new HttpClient(httpClientHandler, true)) {
						long startPosition = outputStream.Position;
						using(HttpResponseMessage response = await httpClient.GetAsync(requestUri).ConfigureAwait(false)) {
							await using(Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) {
								stream.CopyTo(outputStream);
								stream.Flush();
							}
						}
						long endPosition = outputStream.Position;

						if(startPosition == endPosition) {
							throw new ApplicationException("Empty genesis hash data");
						}
					}
				}).ConfigureAwait(false);

				return;
			} catch(Exception ex) {
				Log.Error(ex, "Failed to download the genesis hash. We will retry in legacy mode");
			}
			
			// try legacy
			try {
				await Repeater.RepeatAsync(async () => {
					using var net = new WebClient();
					var data = await net.DownloadDataTaskAsync(requestUri).ConfigureAwait(false);

					if(data == null || data.Length == 0) {
						throw new ApplicationException("Empty genesis hash data");
					}
					outputStream.Write(data); 
				}).ConfigureAwait(false);
			}
			catch(Exception ex) {
				Log.Error(ex, "Failed to download the genesis hash using legacy mode");
			}
		}
	}
}