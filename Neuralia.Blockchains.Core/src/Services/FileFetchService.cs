using System;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Services {

	public interface IFileFetchService {
		Task<(SafeArrayHandle sha2, SafeArrayHandle sha3)?> FetchGenesisHash(string hashuri, string chainWalletPat, string name);
		Task<(SafeArrayHandle sha2, SafeArrayHandle sha3)> FetchDigestHash(string hashuri, string chainWalletPath, int digestId);
		Task<Guid?> FetchSuperkeyConfirmationUuid(string hashuri, long blockId);

		Task<SafeArrayHandle> FetchBlockPublicHash(string hashuri, long blockId);
	}

	public class FileFetchService : IFileFetchService {
		private readonly ChainConfigurations chainConfiguration;

		private readonly IGlobalsService globalsService;

		private readonly IHttpService httpService;

		public FileFetchService(IHttpService httpService, IGlobalsService globalsService) {
			this.httpService = httpService;
			this.globalsService = globalsService;
		}

		public async Task<Guid?> FetchSuperkeyConfirmationUuid(string hashuri, long blockId) {
			string confirmationName = $"confirmation-{blockId}.conf";

			SafeArrayHandle result = await this.httpService.Download(Combine(hashuri, "/confirmations/", confirmationName).ToLower()).ConfigureAwait(false);

			TypeSerializer.Deserialize(result.Span, out Guid confirmation);

			return confirmation;
		}

		public async Task<SafeArrayHandle> FetchBlockPublicHash(string hashuri, long blockId) {

			string hashName = $"block-{blockId}.hash";
			SafeArrayHandle result = await this.httpService.Download(Combine(hashuri, "/hashes/", hashName).ToLower()).ConfigureAwait(false);

			return result;
		}

		/// <summary>
		///     here we extract the genesisModeratorAccountPresentation transaction hashes form the
		///     files
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public async Task<(SafeArrayHandle sha2, SafeArrayHandle sha3)?> FetchGenesisHash(string hashuri, string genesisPath, string filename) {

			string hashName = $"{filename.CapitalizeFirstLetter()}.hash";
			FileExtensions.EnsureDirectoryStructure(genesisPath);
			string filepath = Path.Combine(genesisPath, hashName);

			if(!File.Exists(filepath)) {

				string hashUri = Combine(hashuri.ToLower(), hashName);
				NLog.Default.Information($"Downloading genesis hash from {hashUri}");
				await this.httpService.Download(hashUri, filepath).ConfigureAwait(false);
			}

			if(!File.Exists(filepath)) {
				return null;
			}

			SafeArrayHandle data = SafeArrayHandle.WrapAndOwn(await File.ReadAllBytesAsync(filepath).ConfigureAwait(false));

			if((data == null) || data.IsCleared) {
				throw new ApplicationException("Failed to obtain genesis verification hash.");
			}

			return HashingUtils.ExtractCombinedDualHash(data);
		}

		public async Task<(SafeArrayHandle sha2, SafeArrayHandle sha3)> FetchDigestHash(string hashuri, string digestHashPath, int digestId) {

			string hashName = $"digest-{digestId}.hash";
			FileExtensions.EnsureDirectoryStructure(digestHashPath);
			string filepath = Path.Combine(digestHashPath, hashName);

			if(!File.Exists(filepath)) {

				await this.httpService.Download(Combine(hashuri, "/hashes/", hashName).ToLower(), filepath).ConfigureAwait(false);
			}

			if(!File.Exists(filepath)) {
				return default;
			}

			SafeArrayHandle data = SafeArrayHandle.WrapAndOwn(await File.ReadAllBytesAsync(filepath).ConfigureAwait(false));

			if((data == null) || data.IsCleared) {
				throw new ApplicationException("Failed to obtain digest verification hash.");
			}

			return HashingUtils.ExtractCombinedDualHash(data);
		}

		public static string Combine(string basepath, string path, string path2) {
			return Combine(basepath, Combine(path, path2));
		}

		public static string Combine(string uri1, string uri2) {
			uri1 = uri1.TrimEnd('/');
			uri2 = uri2.TrimStart('/');

			return $"{uri1}/{uri2}";
		}
	}
}