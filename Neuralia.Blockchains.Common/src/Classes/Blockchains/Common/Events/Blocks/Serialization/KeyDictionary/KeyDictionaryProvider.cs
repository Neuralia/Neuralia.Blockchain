using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.KeyDictionary {
	/// <summary>
	///     a class to handle access to the Key dictionary index
	/// </summary>
	public class KeyDictionaryProvider {

		public const int METADATA_SIZE = 1; // + 2 bytes, one for xmss tree height, the one for the hash types

		public const int PAGE_SIZE = 100000;
		public const int TRANSACTION_KEY_SIZE = XMSSPublicKey.PUBLIC_KEY_SIZE_256;
		public const int TRANSACTION_ENTRY_SIZE = TRANSACTION_KEY_SIZE + METADATA_SIZE; 
		public const int MESSAGE_KEY_SIZE = XMSSPublicKey.PUBLIC_KEY_SIZE_256;
		public const int MESSAGE_ENTRY_SIZE = MESSAGE_KEY_SIZE + METADATA_SIZE;
		public readonly int ACCOUNT_ENTRY_SIZE;

		private byte BIT_4_MASK = 0xF;
		private byte BIT_2_MASK = 0x3;
		private readonly ChainConfigurations.KeyDictionaryTypes enabledKeyTypes;

		protected readonly string folder;
		protected readonly ICentralCoordinator centralCoordinator;

		public KeyDictionaryProvider(string folder, ChainConfigurations.KeyDictionaryTypes enabledKeyTypes, ICentralCoordinator centralCoordinator) {

			this.centralCoordinator = centralCoordinator;
			this.folder = folder;
			this.enabledKeyTypes = enabledKeyTypes;

			this.ACCOUNT_ENTRY_SIZE = 0;

			if(this.enabledKeyTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) {
				this.ACCOUNT_ENTRY_SIZE += TRANSACTION_ENTRY_SIZE;
			}

			if(this.enabledKeyTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages)) {
				this.ACCOUNT_ENTRY_SIZE += MESSAGE_ENTRY_SIZE;
			}
		}

		private void TestKeyValidity(byte ordinal, AccountId accountId) {
			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && !this.enabledKeyTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) {
				throw new ApplicationException("Transaction keys are not enabled in this fastkey provider");
			}

			if((ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && !this.enabledKeyTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Messages)) {
				throw new ApplicationException("Message keys are not enabled in this fastkey provider");
			}

			if(!accountId.IsStandard) {
				throw new ApplicationException("Only standard account types supported");
			}
		}

		private string GetBasePath(Enums.AccountTypes accountType) {
			long adjustedAccountId = this.AdjustAccountId(Constants.FIRST_PUBLIC_ACCOUNT_NUMBER);

			int page = this.GetPage(adjustedAccountId);

			return this.GetKeyFileName(page, accountType);
		}

		public bool Test() {

			string path = this.GetBasePath(Enums.AccountTypes.User);

			try {
				using FileSystemWrapper fileSystem = FileSystemWrapper.CreatePhysical();
				bool result = fileSystem.FileExists(path);
				this.centralCoordinator.Log.Verbose($"testing for existance of Key dictionary provider file at path {path}. File '{(result ? "" : "does not")} exist'");

				return result;

			} catch(Exception ex) {
				this.centralCoordinator.Log.Error(ex, $"Failed to test for existance of Key dictionary provider file at path {path}");
			}

			return false;
		}

		public void EnsureBaseFileExists(FileSystemWrapper fileSystem, Enums.AccountTypes accountType) {

			string baseFileName = this.GetBasePath(accountType);

			if(!fileSystem.FileExists(baseFileName)) {

				FileExtensions.EnsureFileExists(baseFileName, fileSystem);

				// ok, write a raw file
				fileSystem.CreateEmptyFile(baseFileName);
			}
		}

		public async Task<List<(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)>> LoadKeyDictionary(List<(AccountId accountId, byte ordinal)> accountIdKeys, LockContext lockContext, FileSystemWrapper fileSystem = null) {
			this.centralCoordinator.Log.Verbose($"Key dictionary provider: loading keys for {accountIdKeys.Count} accounts");
			var result = new List<(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)>();
			
			if(fileSystem == null) {
				fileSystem = FileSystemWrapper.CreatePhysical();
			}
			
			//TODO: can be made parallel
			foreach(var entry in accountIdKeys) {

				var key = await this.LoadKeyFileAsync(entry.accountId, entry.ordinal, fileSystem).ConfigureAwait(false);

				if(key.HasValue && key.Value != default) {
					result.Add((entry.accountId, key.Value.keyBytes, key.Value.treeHeight, key.Value.hashType, key.Value.backupHashType));
				}
			}

			this.centralCoordinator.Log.Verbose($"Key dictionary provider: loaded {result.Count} keys");
			
			return result;
		}
		
		public async Task<(SafeArrayHandle keyBytes, byte treeHeight, byte noncesExponent, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)?> LoadKeyFileAsync(AccountId accountId, byte ordinal, FileSystemWrapper fileSystem = null) {

			this.TestKeyValidity(ordinal, accountId);

			if(fileSystem == null) {
				fileSystem = FileSystemWrapper.CreatePhysical();
			}

			long adjustedAccountId = this.AdjustAccountId(accountId.SequenceId);

			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page, accountId.AccountType);
			
			if(!fileSystem.FileExists(fileName) || (fileSystem.GetFileLength(fileName) == 0)) {
				
				this.centralCoordinator.Log.Debug($"Key dictionary provider: could not load key from file {fileName}. File either does not exist, or is size 0.");
				
				return null;
			}
			
			SafeArrayHandle results = await FileExtensions.ReadBytesAsync(fileName, byteOffsets + offset, size, fileSystem).ConfigureAwait(false);

			SafeArrayHandle keySimpleBytes = SafeArrayHandle.Create(this.GetKeySize(ordinal));
			results.Entry.Slice(METADATA_SIZE).CopyTo(keySimpleBytes.Span);

			(byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType) = this.ExpandHashTypes(results[0]);
			
			byte noncesExponent = 0;
			if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				noncesExponent = WalletProvider.TRANSACTION_KEY_NONCES_EXPONENT;
			}
			else if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				noncesExponent = WalletProvider.MESSAGE_KEY_NONCES_EXPONENT;
			}
			
			return (keySimpleBytes, treeHeight, noncesExponent, hashType, backupHashType);
		}

		private int GetKeySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_KEY_SIZE : MESSAGE_KEY_SIZE;
		}

		private int GetEntrySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_ENTRY_SIZE : MESSAGE_ENTRY_SIZE;
		}

		public async Task WriteKey(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, byte ordinal, FileSystemWrapper fileSystem = null) {

			this.TestKeyValidity(ordinal, accountId);

			if((ordinal != GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && (ordinal != GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
				throw new ApplicationException("Invalid key ordinal");
			}

			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && (key.Length != TRANSACTION_KEY_SIZE)) {
				throw new ApplicationException("Invalid key size");
			}

			if((ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && (key.Length != MESSAGE_KEY_SIZE)) {
				throw new ApplicationException("Invalid key size");
			}

			if(fileSystem == null) {
				fileSystem = FileSystemWrapper.CreatePhysical();
			}

			long adjustedAccountId = this.AdjustAccountId(accountId.SequenceId);
			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page, accountId.AccountType);

			long dataLength = PAGE_SIZE * this.ACCOUNT_ENTRY_SIZE;

			if(!fileSystem.FileExists(fileName) || (fileSystem.GetFileLength(fileName) < dataLength)) {

				FileExtensions.EnsureFileExists(fileName, fileSystem);

				// ok, write a raw file
				await using(Stream fs = fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Write, FileShare.Write)) {

					fs.Seek(dataLength - 1, SeekOrigin.Begin);
					await fs.WriteAsync(new byte[] {0}, 0, 1).ConfigureAwait(false);
				}
			}

			int entrySize = this.GetEntrySize(ordinal);
			int keySize = this.GetKeySize(ordinal);

			byte[] dataEntry = new byte[entrySize];
			dataEntry[0] = this.CompressHashTypes(treeHeight, hashType, backupHashType);
			
			key.Span.CopyTo(dataEntry.AsSpan().Slice(METADATA_SIZE, keySize));

			await using(Stream fs = fileSystem.OpenFile(fileName, FileMode.Open, FileAccess.Write, FileShare.Write)) {

				fs.Seek((int) (byteOffsets + offset), SeekOrigin.Begin);
				await fs.WriteAsync(dataEntry, 0, dataEntry.Length).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Ensure we adjust for the accounts offset
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		private long AdjustAccountId(long accountSequenceId) {
			long adjustedAccountId = accountSequenceId - Constants.FIRST_PUBLIC_ACCOUNT_NUMBER;

			if(adjustedAccountId < 0) {
				// we dont save moderator keys here
				throw new InvalidDataException($"Moderator keys can not be serialized in the {nameof(KeyDictionaryProvider)}.");
			}

			return adjustedAccountId;
		}

		private string GetKeyFileName(int page, Enums.AccountTypes accountType) {
			return Path.Combine(Path.Combine(this.folder, accountType.ToString().ToLower()), $"keys.{page}.index");
		}

		private (int offset, int size) GetKeyByteOffset(byte ordinal) {
			if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				return (0, TRANSACTION_ENTRY_SIZE);
			}

			if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {

				int offset = 0;

				if(this.enabledKeyTypes.HasFlag(ChainConfigurations.KeyDictionaryTypes.Transactions)) {
					offset = TRANSACTION_ENTRY_SIZE;
				}

				return (offset, MESSAGE_ENTRY_SIZE);
			}

			throw new ApplicationException();
		}

		private int GetPage(long accountId) {
			return (int) accountId / PAGE_SIZE;
		}

		private int GetPageOffset(long accountId, int page) {
			return (int) (accountId - (page * PAGE_SIZE));
		}

		private long GetPageByteOffset(int pageOffset) {
			return pageOffset * this.ACCOUNT_ENTRY_SIZE;
		}
		
		/// <summary>
		/// compress the values into a single byte
		/// </summary>
		/// <param name="treeHeight"></param>
		/// <param name="hashType"></param>
		/// <param name="backupHashType"></param>
		/// <returns></returns>
		private byte CompressHashTypes(byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType) {
			
			byte value = (byte)((treeHeight - WalletProvider.MINIMAL_XMSS_KEY_HEIGHT) & BIT_4_MASK);

			byte ConvertHashType(Enums.KeyHashType type) {

				switch(type) {
					case Enums.KeyHashType.SHA2_256:

						return 0;
					case Enums.KeyHashType.SHA3_256:

						return 1;
					case Enums.KeyHashType.SHA2_512:

						return 2;
					case Enums.KeyHashType.SHA3_512:

						return 3;
				}
				throw new ArgumentException("Unsupported hash type.");
			}

			value |= (byte) ((ConvertHashType(hashType) & BIT_2_MASK) << 4);
			value |= (byte) ((ConvertHashType(backupHashType) & BIT_2_MASK) << 6);

			return value;
		}
		
		/// <summary>
		/// expand the compressed types into the full values
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		private (byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType) ExpandHashTypes(byte value) {

			byte treeHeight = (byte)((value & BIT_4_MASK) + WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);

			Enums.KeyHashType ConvertHashType(byte type) {

				switch(type) {
					case 0:

						return Enums.KeyHashType.SHA2_256;
					case 1:

						return Enums.KeyHashType.SHA3_256;
					case 2:

						return Enums.KeyHashType.SHA2_512;
					case 3:

						return Enums.KeyHashType.SHA3_512;
				}
				throw new ArgumentException("Unsupported hash type.");
			}

			Enums.KeyHashType hashType = ConvertHashType((byte)((value >> 4) & BIT_2_MASK));
			Enums.KeyHashType backupHashType = ConvertHashType((byte)((value >> 6) & BIT_2_MASK));

			return (treeHeight, hashType, backupHashType);
		}
	}
}