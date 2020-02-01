using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Core.Services {

	public interface IFileFetchService {
		(SafeArrayHandle sha2, SafeArrayHandle sha3)? FetchGenesisHash(string hashuri, string chainWalletPat, string name);
		(SafeArrayHandle sha2, SafeArrayHandle sha3) FetchDigestHash(string hashuri, string chainWalletPath, int digestId);
		Guid? FetchSuperkeyConfirmationUuid(string hashuri, long blockId);

		SafeArrayHandle FetchBlockPublicHash(string hashuri, long blockId);
	}

	public class FileFetchService : IFileFetchService {

		private readonly IGlobalsService globalsService;

		private readonly IHttpService httpService;
		private readonly ChainConfigurations chainConfiguration;
		
		public FileFetchService(IHttpService httpService, IGlobalsService globalsService) {
			this.httpService = httpService;
			this.globalsService = globalsService;
		}

		public static string Combine( string basepath, string path, string path2) {
			return Combine(basepath, Combine(path, path2));
		}
		
		public static string Combine(string uri1, string uri2)
		{
			uri1 = uri1.TrimEnd('/');
			uri2 = uri2.TrimStart('/');
			return $"{uri1}/{uri2}";
		}
		
		public Guid? FetchSuperkeyConfirmationUuid(string hashuri, long blockId) {
			string confirmationName = $"confirmation-{blockId}.conf";

			SafeArrayHandle result = this.httpService.Download((Combine(hashuri, "/confirmations/" ,confirmationName)).ToLower());

			TypeSerializer.Deserialize(result.Span, out Guid confirmation);

			return confirmation;
		}

		public SafeArrayHandle FetchBlockPublicHash(string hashuri, long blockId) {

			string hashName = $"block-{blockId}.hash";
			SafeArrayHandle result = this.httpService.Download((Combine(hashuri, "/hashes/", hashName)).ToLower());

			return result;
		}

		/// <summary>
		///     here we extract the genesisModeratorAccountPresentation transaction hashes form the
		///     files
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public (SafeArrayHandle sha2, SafeArrayHandle sha3)? FetchGenesisHash(string hashuri, string genesisPath, string filename) {

			string hashName = $"{filename.CapitallizeFirstLetter()}.hash";
			FileExtensions.EnsureDirectoryStructure(genesisPath, new FileSystem());
			string filepath = Path.Combine(genesisPath, hashName);

			if(!File.Exists(filepath)) {
				
				string hashUri = Combine(hashuri.ToLower(), hashName);
				Log.Information($"Downloading genesis hash from {hashUri}");
				this.httpService.Download(hashUri, filepath);
			}

			if(!File.Exists(filepath)) {
				return null;
			}

			var data = ByteArray.WrapAndOwn(File.ReadAllBytes(filepath));

			if(data == null || data.IsCleared) {
				throw new ApplicationException("Failed to obtain genesis verification hash.");
			}
			return HashingUtils.ExtractCombinedDualHash(data);
		}

		public (SafeArrayHandle sha2, SafeArrayHandle sha3) FetchDigestHash(string hashuri, string digestHashPath, int digestId) {

			string hashName = $"digest-{digestId}.hash";
			FileExtensions.EnsureDirectoryStructure(digestHashPath, new FileSystem());
			string filepath = Path.Combine(digestHashPath, hashName);

			if(!File.Exists(filepath)) {

				this.httpService.Download((Combine(hashuri, "/hashes/", hashName)).ToLower(), filepath);
			}

			if(!File.Exists(filepath)) {
				return default;
			}

			var data = ByteArray.WrapAndOwn(File.ReadAllBytes(filepath));

			if(data == null || data.IsCleared) {
				throw new ApplicationException("Failed to obtain digest verification hash.");
			}
			return HashingUtils.ExtractCombinedDualHash(data);
		}
	}
}