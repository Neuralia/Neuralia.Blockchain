using System;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Services {

	public interface IFileFetchService {
		(IByteArray sha2, IByteArray sha3) FetchGenesisHash(string chainWalletPat, string name);
		(IByteArray sha2, IByteArray sha3) FetchDigestHash(string chainWalletPath, int digestId);
		Guid? FetchSuperkeyConfirmationUuid(long blockId);

		IByteArray FetchBlockPublicHash(long blockId);
	}

	public class FileFetchService : IFileFetchService {

		private readonly IGlobalsService globalsService;

		private readonly IHttpService httpService;

		public FileFetchService(IHttpService httpService, IGlobalsService globalsService) {
			this.httpService = httpService;
			this.globalsService = globalsService;
		}

		public Guid? FetchSuperkeyConfirmationUuid(long blockId) {
			string confirmationName = $"confirmation-{blockId}.conf";

			IByteArray result = this.httpService.Download(("https://hash.neuralium.com/confirmations/" + confirmationName).ToLower());

			TypeSerializer.Deserialize(result.Span, out Guid confirmation);

			return confirmation;
		}

		public IByteArray FetchBlockPublicHash(long blockId) {

			string hashName = $"block-{blockId}.hash";
			IByteArray result = this.httpService.Download(("https://hash.neuralium.com/hashes/" + hashName).ToLower());

			return result;
		}

		/// <summary>
		///     here we extract the genesisModeratorAccountPresentation transaction hashes form the
		///     files
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public (IByteArray sha2, IByteArray sha3) FetchGenesisHash(string genesisPath, string filename) {

			string hashName = $"{filename}.hash";
			FileExtensions.EnsureDirectoryStructure(genesisPath, new FileSystem());
			string filepath = Path.Combine(genesisPath, hashName);

			if(!File.Exists(filepath)) {
				this.httpService.Download(("https://hash.neuralium.com/" + hashName).ToLower(), filepath);
			}

			if(!File.Exists(filepath)) {
				return default;
			}

			var data = File.ReadAllBytes(filepath);

			if((data == null) || (data.Length == 0)) {
				throw new ApplicationException("Failed to obtain genesis verification hash.");
			}

			return HashingUtils.ExtractCombinedDualHash((ByteArray) data);
		}

		public (IByteArray sha2, IByteArray sha3) FetchDigestHash(string digestHashPath, int digestId) {

			string hashName = $"digest-{digestId}.hash";
			FileExtensions.EnsureDirectoryStructure(digestHashPath, new FileSystem());
			string filepath = Path.Combine(digestHashPath, hashName);

			if(!File.Exists(filepath)) {

				this.httpService.Download(("https://hash.neuralium.com/" + hashName).ToLower(), filepath);
			}

			if(!File.Exists(filepath)) {
				return default;
			}

			var data = File.ReadAllBytes(filepath);

			return HashingUtils.ExtractCombinedDualHash((ByteArray) data);
		}
	}
}