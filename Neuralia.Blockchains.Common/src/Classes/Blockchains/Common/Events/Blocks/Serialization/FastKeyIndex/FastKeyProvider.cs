using System;
using System.IO;

using System.Threading.Tasks;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Nito.AsyncEx.Synchronous;
using Serilog;
using Zio;
using Zio.FileSystems;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.FastKeyIndex {
	/// <summary>
	///     a class to handle access to the fast key index
	/// </summary>
	public class FastKeyProvider {

		public const int PAGE_SIZE = 100000;
		public const int TRANSACTION_KEY_SIZE = 128; // + 2 bytes, one for xmss tree height, the second for the hash bits
		public const int TRANSACTION_ENTRY_SIZE = TRANSACTION_KEY_SIZE + 2; // + 2 bytes, one for xmss tree height, the second for the hash bits
		public const int MESSAGE_KEY_SIZE = 64;
		public const int MESSAGE_ENTRY_SIZE = MESSAGE_KEY_SIZE + 2;
		public readonly int ACCOUNT_ENTRY_SIZE;

		private readonly ChainConfigurations.FastKeyTypes enabledKeyTypes;
		
		protected readonly string folder;

		public FastKeyProvider(string folder, ChainConfigurations.FastKeyTypes enabledKeyTypes) {
			
			this.folder = folder;
			this.enabledKeyTypes = enabledKeyTypes;

			this.ACCOUNT_ENTRY_SIZE = 0;

			if(this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
				this.ACCOUNT_ENTRY_SIZE += TRANSACTION_ENTRY_SIZE;
			}

			if(this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
				this.ACCOUNT_ENTRY_SIZE += MESSAGE_ENTRY_SIZE;
			}
		}

		private void TestKeyValidity(byte ordinal) {
			if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID && !this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
				throw new ApplicationException("Transaction keys are not enabled in this fastkey provider");
			}

			if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID && !this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
				throw new ApplicationException("Message keys are not enabled in this fastkey provider");
			}
		}

		private string GetBasePath() {
			long adjustedAccountId = this.AdjustAccountId(new AccountId(Constants.FIRST_PUBLIC_ACCOUNT_NUMBER, Enums.AccountTypes.Standard));
			
			int page = this.GetPage(adjustedAccountId);
			
			return this.GetKeyFileName(page);
		}

		public bool Test() {

			string path = this.GetBasePath();
			try {
				using var fileSystem = FileSystemWrapper.CreatePhysical();
				bool result = fileSystem.FileExists(path);
				Log.Verbose($"testing for existance of fast key provider file at path {path}. File '{(result?"":"does not")} exist'");

				return result;

			} catch(Exception ex) {
				Log.Error(ex, $"Failed to test for existance of fast key provider file at path {path}");
			}

			return false;
		}

		public void EnsureBaseFileExists(FileSystemWrapper fileSystem) {

			string baseFileName = this.GetBasePath();
			if(!fileSystem.FileExists(baseFileName)) {

				FileExtensions.EnsureFileExists(baseFileName, fileSystem);

				// ok, write a raw file
				using(Stream fs = fileSystem.CreateFile(baseFileName)) {
					
				}
			}
		}
		public async Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)> LoadKeyFileAsync(AccountId accountId, byte ordinal, FileSystemWrapper fileSystem = null) {

			this.TestKeyValidity(ordinal);
			if(fileSystem == null) {
				fileSystem = FileSystemWrapper.CreatePhysical();
			}
			long adjustedAccountId = this.AdjustAccountId(accountId);

			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page);

			if(!fileSystem.FileExists(fileName) || fileSystem.GetFileLength(fileName) == 0) {
				return default;
			}

			SafeArrayHandle results = await FileExtensions.ReadBytesAsync(fileName, byteOffsets + offset, size, fileSystem).ConfigureAwait(false);

			ByteArray keySimpleBytes = ByteArray.Create(this.GetKeySize(ordinal));
			results.Entry.Slice(2).CopyTo(keySimpleBytes.Span);

			return (keySimpleBytes, results[0], (Enums.KeyHashBits)results[1]);
		}
		
		public Task<(SafeArrayHandle keyBytes, byte treeheight, Enums.KeyHashBits hashBits)> LoadKeyFile(AccountId accountId, byte ordinal, FileSystemWrapper fileSystem = null) {

			return this.LoadKeyFileAsync(accountId, ordinal, fileSystem);
		}

		private int GetKeySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_KEY_SIZE : MESSAGE_KEY_SIZE;
		}

		private int GetEntrySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_ENTRY_SIZE : MESSAGE_ENTRY_SIZE;
		}

		public async Task WriteKey(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashBits hashBits, byte ordinal, FileSystemWrapper fileSystem = null) {

			this.TestKeyValidity(ordinal);

			if(ordinal != GlobalsService.TRANSACTION_KEY_ORDINAL_ID && ordinal != GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				throw new ApplicationException("Invalid key ordinal");
			}

			if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID && key.Length != TRANSACTION_KEY_SIZE) {
				throw new ApplicationException("Invalid key size");
			}

			if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID && key.Length != MESSAGE_KEY_SIZE) {
				throw new ApplicationException("Invalid key size");
			}

			if(fileSystem == null) {
				fileSystem = FileSystemWrapper.CreatePhysical();
			}

			long adjustedAccountId = this.AdjustAccountId(accountId);
			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page);

			long dataLength = PAGE_SIZE * this.ACCOUNT_ENTRY_SIZE;

			if(!fileSystem.FileExists(fileName) || fileSystem.GetFileLength(fileName) < dataLength) {

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
			dataEntry[0] = treeHeight;
			dataEntry[1] = (byte)hashBits;
			key.Span.CopyTo(dataEntry.AsSpan().Slice(2, keySize));

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
		private long AdjustAccountId(AccountId accountId) {
			long adjustedAccountId = accountId.SequenceId - Constants.FIRST_PUBLIC_ACCOUNT_NUMBER;

			if(adjustedAccountId < 0) {
				// we dotn save moderator keys here
				throw new InvalidDataException($"Moderator keys can not be serialized in the {nameof(FastKeyProvider)}.");
			}

			return adjustedAccountId;
		}

		private string GetKeyFileName(int page) {
			return Path.Combine(this.folder, $"keys.{page}.index");
		}

		private (int offset, int size) GetKeyByteOffset(byte ordinal) {
			if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				return (0, TRANSACTION_ENTRY_SIZE);
			}

			if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {

				int offset = 0;

				if(this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
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
			return (int) (accountId - page * PAGE_SIZE);
		}

		private long GetPageByteOffset(int pageOffset) {
			return pageOffset * this.ACCOUNT_ENTRY_SIZE;
		}
	}
}