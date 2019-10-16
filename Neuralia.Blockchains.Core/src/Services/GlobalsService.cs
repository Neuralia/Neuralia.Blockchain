using System.Collections.Generic;
using System.IO;
using Microsoft.IO;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;

namespace Neuralia.Blockchains.Core.Services {
	public interface IAppRemote {
		void Shutdown();
	}

	public interface IGlobalsService {
		List<string> HardcodedNodes { get; }
		Dictionary<BlockchainType, GlobalsService.ChainSupport> SupportedChains { get; }
		string GetSystemFilesDirectoryPath();
		string GetSystemStorageDirectoryPath();
		void AddSupportedChain(BlockchainType blockchainType, string name, bool enabled);
	}

	public sealed class GlobalsService : IGlobalsService {

		/// <summary>
		///     this is the default port our listener listens to
		/// </summary>
		public const int DEFAULT_PORT = 33888;

		/// <summary>
		///     this is the default port our listener listens to
		/// </summary>
		public const int DEFAULT_RPC_PORT = 12033;

		/// <summary>
		///     The size in bytes of public keys in our system. XMSS keys of course
		/// </summary>
		public const int KEY_BYTE_SIZE = 64;

		/// <summary>
		///     The size in bytes of hashes in our system. Sha3 hash of course
		/// </summary>
		public const int HASH_BYTE_SIZE = 64;

		public const string COMMON_DIRECTORY_NAME = "common";
		public const string TRANSACTION_KEY_NAME = "TransactionKey";
		public const string MESSAGE_KEY_NAME = "MessageKey";
		public const string CHANGE_KEY_NAME = "ChangeKey";
		public const string SUPER_KEY_NAME = "SuperKey";

		public const byte TRANSACTION_KEY_ORDINAL_ID = 1;
		public const byte MESSAGE_KEY_ORDINAL_ID = 2;
		public const byte CHANGE_KEY_ORDINAL_ID = 3;
		public const byte SUPER_KEY_ORDINAL_ID = 4;

		public const byte MODERATOR_COMMUNICATIONS_KEY_ID = 1;
		public const byte MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID = 2;
		public const byte MODERATOR_BLOCKS_KEY_XMSSMT_ID = 3;
		public const byte MODERATOR_BLOCKS_CHANGE_KEY_ID = 4;
		public const byte MODERATOR_DIGEST_BLOCKS_KEY_ID = 5;
		public const byte MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID = 6;
		public const byte MODERATOR_BINARY_KEY_ID = 7;
		public const byte MODERATOR_SUPER_CHANGE_KEY_ID = 8;
		public const byte MODERATOR_PTAH_KEY_ID = 9;

		public const string TOKEN_CHAIN_NAME = "neuralium";

		/// <summary>
		///     The maximum amount of solutions allowed to be sent in POW transactions
		/// </summary>
		public const byte POW_MAX_SOLUTIONS = 3;

		public static readonly ushort POW_DIFFICULTY = (ushort) HashDifficultyUtils.DEFAULT_256_DIFFICULTY;
		
		
		/// <summary>
		/// The delay in minutes after which we will update our mining registration
		/// </summary>
		public const int UPDATE_MINING_REGISTRATION_DELAY = 60;
		
		/// <summary>
		/// The aboslute timeout of a mining registration since the last update
		/// </summary>
		public const int MINING_REGISTRATION_TIMEOUT = UPDATE_MINING_REGISTRATION_DELAY * 2;
		
		/// <summary>
		/// The delay between each query for mining status
		/// </summary>
		public const int UPDATE_MINING_STATUS_DELAY = 10;
		
		/// <summary>
		/// How long to wait before the first mining status query after registration
		/// </summary>
		public const int UPDATE_MINING_STATUS_START_DELAY = 3;

		//http://www.philosophicalgeek.com/2015/02/06/announcing-microsoft-io-recycablememorystream/
		public static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager;

		private static string systemFilesPath;

		static GlobalsService() {
			RecyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
		}

		public static IAppRemote AppRemote { get; set; }

		public void AddSupportedChain(BlockchainType blockchainType, string name, bool enabled) {
			if(!this.SupportedChains.ContainsKey(blockchainType)) {
				this.SupportedChains.Add(blockchainType, new ChainSupport {Name = name, Enabled = enabled});
			}
		}

		/// <summary>
		///     Here we store information about the supported chains and their status
		/// </summary>
		public Dictionary<BlockchainType, ChainSupport> SupportedChains { get; } = new Dictionary<BlockchainType, ChainSupport>();

		/// <summary>
		///     A list of hardcoded nodes. helps with the initialization of the app
		/// </summary>
		/// <returns></returns>
		public List<string> HardcodedNodes { get; } = new List<string>();

		public string GetSystemFilesDirectoryPath() {
			return GetGeneralSystemFilesDirectoryPath();
		}

		public static string GetGeneralSystemFilesDirectoryPath() {
			if(string.IsNullOrWhiteSpace(systemFilesPath)) {
				systemFilesPath = GlobalSettings.ApplicationSettings.SystemFilesPath;

				// use the standard home path
				if(string.IsNullOrWhiteSpace(systemFilesPath)) {
					systemFilesPath = FileUtilities.GetSystemFilesPath();
				}
			}

			return systemFilesPath;
		}
		
		public string GetSystemStorageDirectoryPath() {
			return Path.Combine(this.GetSystemFilesDirectoryPath(), COMMON_DIRECTORY_NAME);
		}

		public class ChainSupport {
			public bool Enabled;
			public string Name;
			public bool Started;
		}
	}
}