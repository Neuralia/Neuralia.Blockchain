using System;
using System.IO;
using System.IO.Abstractions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;

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

		protected readonly IFileSystem fileSystem;
		protected readonly string folder;

		public FastKeyProvider(string folder, ChainConfigurations.FastKeyTypes enabledKeyTypes, IFileSystem fileSystem) {
			this.fileSystem = fileSystem;
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
			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && !this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Transactions)) {
				throw new ApplicationException("Transaction keys are not enabled in this fastkey provider");
			}

			if((ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && !this.enabledKeyTypes.HasFlag(ChainConfigurations.FastKeyTypes.Messages)) {
				throw new ApplicationException("Message keys are not enabled in this fastkey provider");
			}
		}

		private string GetBasePath() {
			long adjustedAccountId = this.AdjustAccountId(new AccountId(Constants.FIRST_PUBLIC_ACCOUNT_NUMBER, Enums.AccountTypes.Standard));
			
			int page = this.GetPage(adjustedAccountId);
			
			return this.GetKeyFileName(page);
		}

		public bool Test() {
			
			return this.fileSystem.File.Exists(this.GetBasePath());
		}

		public void EnsureBaseFileExists() {
			string baseFileName = this.GetBasePath();
			if(!this.fileSystem.File.Exists(baseFileName)) {

				FileExtensions.EnsureFileExists(baseFileName, this.fileSystem);

				// ok, write a raw file
				using(Stream fs = this.fileSystem.File.Create(baseFileName)) {
					
				}
			}
		}
		public (SafeArrayHandle keyBytes, byte treeheight, byte hashBits) LoadKeyFile(AccountId accountId, byte ordinal) {

			this.TestKeyValidity(ordinal);

			long adjustedAccountId = this.AdjustAccountId(accountId);

			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page);

			if(!this.fileSystem.File.Exists(fileName) || this.fileSystem.FileInfo.FromFileName(fileName).Length == 0) {
				return default;
			}

			SafeArrayHandle results = FileExtensions.ReadBytes(fileName, byteOffsets + offset, size, this.fileSystem);

			ByteArray keySimpleBytes = ByteArray.Create(this.GetKeySize(ordinal));
			results.Entry.Slice(2).CopyTo(keySimpleBytes.Span);

			return (keySimpleBytes, results[0], results[1]);
		}

		private int GetKeySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_KEY_SIZE : MESSAGE_KEY_SIZE;
		}

		private int GetEntrySize(byte ordinal) {
			return ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID ? TRANSACTION_ENTRY_SIZE : MESSAGE_ENTRY_SIZE;
		}

		public void WriteKey(AccountId accountId, SafeArrayHandle key, byte treeHeight, byte hashBits, byte ordinal) {

			this.TestKeyValidity(ordinal);

			if((ordinal != GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && (ordinal != GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
				throw new ApplicationException("Invalid key ordinal");
			}

			if((ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) && (key.Length != TRANSACTION_KEY_SIZE)) {
				throw new ApplicationException("Invalid key size");
			}

			if((ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) && (key.Length != MESSAGE_KEY_SIZE)) {
				throw new ApplicationException("Invalid key size");
			}

			long adjustedAccountId = this.AdjustAccountId(accountId);
			int page = this.GetPage(adjustedAccountId);
			int pageOffset = this.GetPageOffset(adjustedAccountId, page);
			long byteOffsets = this.GetPageByteOffset(pageOffset);

			(int offset, int size) = this.GetKeyByteOffset(ordinal);

			string fileName = this.GetKeyFileName(page);

			long dataLength = PAGE_SIZE * this.ACCOUNT_ENTRY_SIZE;

			if(!this.fileSystem.File.Exists(fileName) || (this.fileSystem.FileInfo.FromFileName(fileName).Length < dataLength)) {

				FileExtensions.EnsureFileExists(fileName, this.fileSystem);

				// ok, write a raw file
				using(Stream fs = this.fileSystem.File.OpenWrite(fileName)) {

					fs.Seek(dataLength - 1, SeekOrigin.Begin);
					fs.WriteByte(0);
				}
			}

			int entrySize = this.GetEntrySize(ordinal);
			int keySize = this.GetKeySize(ordinal);

			Span<byte> dataEntry = stackalloc byte[entrySize];
			dataEntry[0] = treeHeight;
			dataEntry[1] = hashBits;
			key.Span.CopyTo(dataEntry.Slice(2, keySize));

			using(Stream fs = this.fileSystem.File.OpenWrite(fileName)) {

				fs.Seek((int) (byteOffsets + offset), SeekOrigin.Begin);
				fs.Write(dataEntry.ToArray(), 0, dataEntry.Length);
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
			return (int) (accountId - (page * PAGE_SIZE));
		}

		private long GetPageByteOffset(int pageOffset) {
			return pageOffset * this.ACCOUNT_ENTRY_SIZE;
		}
	}
}