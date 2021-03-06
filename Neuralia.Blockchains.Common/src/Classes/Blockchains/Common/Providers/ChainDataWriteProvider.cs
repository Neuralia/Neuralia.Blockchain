using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainDataWriteProvider : IChainDataLoadProvider {
		void ClearBlocksCache();
		void SerializeBlock(IDehydratedBlock dehydratedBlock);
		void SerializeBlockchainMessage(IDehydratedBlockchainMessage dehydratedBlockchainMessage);
		Task CacheAppointmentMessage(Guid messageId, SafeArrayHandle envelope);
		Task ClearCachedAppointmentMessages(List<Guid> messageIds);
		
		void SaveDigestChannelDescription(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor);

		void WriteDigestFile(DigestChannelSet digestChannelSet, DigestChannelType channelId, int indexId, int fileId, uint partIndex, SafeArrayHandle data);
		void UpdateCurrentDigest(int digestId, long blockHeight);
		DigestChannelSet RecreateDigestChannelSet(int digestId, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor);
		void UpdateCurrentDigest(IBlockchainDigest digest);
		void SaveDigestHeader(int digestId, SafeArrayHandle digestHeader);
		Task SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType BackupHashType, byte ordinal, LockContext lockContext = null);

		Task CacheUnvalidatedBlockGossipMessage(IBlockEnvelope unvalidatedBlockEnvelope, long xxHash);
		Task ClearCachedUnvalidatedBlockGossipMessage(long blockId);
		Task WriteGlobalWalletKeyIndexCacheFile(Guid fileName, SafeArrayHandle bytes);
		void TruncateBlockFileSizes(long blockId, Dictionary<string, long> fileSizes);

		void EnsureKeyDictionaryIndex();

		Task RunTransactionalActions(List<Func<LockContext, Task>> serializationActions, SerializationTransactionProcessor serializationTransactionProcessor);
		Task SaveCachedTHSState(THSState state, string key);
		void ClearCachedTHSState(string key);
		Task CachePreviousBlockXmssKeySignaturePathCache();
	}

	public interface IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IChainDataWriteProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     The main provider for writing chain events to disk. This class is meant to be used by the serialization manager
	///     ONLY. NOT thread safe.
	/// </summary>
	public abstract class ChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		private readonly RecursiveAsyncLock blockLocker = new RecursiveAsyncLock();
		private readonly RecursiveAsyncLock digestLocker = new RecursiveAsyncLock();
		private readonly RecursiveAsyncLock gossipCacheLocker = new RecursiveAsyncLock();
		private readonly RecursiveAsyncLock messageLocker = new RecursiveAsyncLock();

		//TODO: review ALL locks to optimize. I did this quickly and its surely not optimal
		private readonly RecursiveAsyncLock transactionalLocker = new RecursiveAsyncLock();

		protected ChainDataWriteProvider(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator) {

		}

		private IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataLoadProvider => this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase;

		protected new IBlockchainEventSerializationFalReadWrite BlockchainEventSerializationFal => (IBlockchainEventSerializationFalReadWrite) base.BlockchainEventSerializationFal;

		public void ClearBlocksCache() {
			this.blocksCache.Clear();
		}
		
		public void SerializeBlock(IDehydratedBlock dehydratedBlock) {

			using(this.blockLocker.Lock()) {
				BlockChainConfigurations configuration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				if(!BlockchainUtilities.UsesBlocks(configuration.BlockSavingMode)) {
					// we dotn save the blocks
					return;
				}

				//1. ok, there we go. first step, lets make sure this block is not already serialized

				(long index, long startingBlockId, long endingBlockId) index = this.FindBlockIndex(dehydratedBlock.BlockId.Value);

				//2. now, we will determine where it should physically go, and insert it in the block index

				// we are ready to insert the block index entry

				bool inserted = false;

				try {
					inserted = this.BlockchainEventSerializationFal.InsertBlockEntry(dehydratedBlock.BlockId.Value, index, dehydratedBlock.GetEssentialDataChannels(), dehydratedBlock.RehydratedBlock.MasterOffsets);
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, $"Failed to insert the block id {dehydratedBlock.BlockId} into the blockchain filesystem");

					throw;
				}

				if(inserted) {
					this.CentralCoordinator.Log.Information($"Block id {dehydratedBlock.BlockId} has been successfully inserted in the blockchain filesystem.");
				} else {
					this.CentralCoordinator.Log.Information($"Block id {dehydratedBlock.BlockId} already existed in the blockchain filesystem. Nothing changed.");
				}

				// ok, our block is saved!

				// store in memory for quick access when required
				this.CacheBlock(dehydratedBlock.RehydratedBlock, dehydratedBlock.GetEssentialDataChannels(), dehydratedBlock);
			}
		}

		public void SerializeBlockchainMessage(IDehydratedBlockchainMessage dehydratedBlockchainMessage) {

			using(this.messageLocker.Lock()) {
				//TODO: make this method atomic!!!
				// get our message bytes
				SafeArrayHandle messageBytes = dehydratedBlockchainMessage.Dehydrate();

				Guid uuid = dehydratedBlockchainMessage.RehydratedEvent.Uuid;

				// ok, now we create our new entry. lets create the directory structure and blocks file if it does not exist
				string messagesFile = this.GetMessagesFile(uuid);
				string messagesIndexFile = this.GetMessagesIndexFile(uuid);

				this.BlockchainEventSerializationFal.EnsureFileExists(messagesFile);
				this.BlockchainEventSerializationFal.EnsureFileExists(messagesIndexFile);

				long? messagesFileSize = this.ChainDataLoadProvider.GetMessagesFileSize(uuid);

				if(!messagesFileSize.HasValue) {
					throw new ApplicationException("Messages file did not exist");
				}

				// we are ready to insert the block index entry
				this.BlockchainEventSerializationFal.InsertMessagesIndexEntry(messagesIndexFile, uuid, messagesFileSize.Value, messageBytes.Length);

				this.BlockchainEventSerializationFal.InsertMessagesEntry(messagesFile, messageBytes);
			}
		}

		public Task CacheAppointmentMessage(Guid messageId, SafeArrayHandle envelope) {
			
			string filePath = this.GetAppointmentMessageFilePath(messageId);

			FileExtensions.EnsureDirectoryStructure(this.GetAppointmentMessagesCacheFolderPath());
			
			if(this.fileSystem.FileExists(filePath)){
				return Task.CompletedTask;
			}
			
			return this.fileSystem.WriteAllBytesAsync(filePath, envelope.ToExactByteArray());
		}

		public Task ClearCachedAppointmentMessages(List<Guid> messageIds) {
			FileExtensions.EnsureDirectoryStructure(this.GetAppointmentMessagesCacheFolderPath());

			foreach(var messageId in messageIds) {
				string filePath = this.GetAppointmentMessageFilePath(messageId);
				
				if(this.fileSystem.FileExists(filePath)){
					try {
						this.fileSystem.DeleteFile(filePath);
					} catch(Exception ex) {
						Log.Error(ex, $"failed to clear cached appointment message with Id {messageId}");
					}
				}
			}
			return Task.CompletedTask;
		}

		public void SaveDigestChannelDescription(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor) {

			using(this.digestLocker.Lock()) {
				this.BlockchainEventSerializationFal.SaveDigestChannelDescription(this.GetDigestsScopedFolderPath(digestId), blockchainDigestDescriptor);
			}
		}

		public void UpdateCurrentDigest(IBlockchainDigest digest) {
			this.UpdateCurrentDigest(digest.DigestId, digest.BlockId.Value);
		}

		public void UpdateCurrentDigest(int digestId, long blockHeight) {

			using(this.digestLocker.Lock()) {
				bool deletePreviousBlocks = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration().NodeShareType().PartialBlocks;
				(long index, long startingBlockId, long endingBlockId)? blockGroupIndex = null;

				if(deletePreviousBlocks) {
					blockGroupIndex = this.FindBlockIndex(blockHeight);
				}

				this.BlockchainEventSerializationFal.UpdateCurrentDigest(blockHeight, this.GetDigestsScopedFolderPath(digestId), deletePreviousBlocks, blockGroupIndex);
			}
		}

		public DigestChannelSet RecreateDigestChannelSet(int digestId, BlockchainDigestSimpleChannelSetDescriptor blockchainDigestDescriptor) {
			return this.BlockchainEventSerializationFal.RecreateDigestChannelSet(this.GetDigestsScopedFolderPath(digestId), blockchainDigestDescriptor);
		}

		public void WriteDigestFile(DigestChannelSet digestChannelSet, DigestChannelType channelId, int indexId, int fileId, uint partIndex, SafeArrayHandle data) {
			this.BlockchainEventSerializationFal.WriteDigestFile(digestChannelSet, channelId, indexId, fileId, partIndex, data);
		}

		public void SaveDigestHeader(int digestId, SafeArrayHandle digestHeader) {

			using(this.digestLocker.Lock()) {
				this.BlockchainEventSerializationFal.SaveDigestHeader(this.GetDigestsHeaderFilePath(digestId), digestHeader);
			}
		}

		public void EnsureKeyDictionaryIndex() {

			using(this.transactionalLocker.Lock()) {
				if(this.KeyDictionaryEnabled(GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || this.KeyDictionaryEnabled(GlobalsService.MESSAGE_KEY_ORDINAL_ID)) {
					this.BlockchainEventSerializationFal.EnsureKeyDictionaryIndex();
				}
			}
		}

		/// <summary>
		///     trucate the files to the length we had recorded to restore them.
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="fileSizes"></param>
		public void TruncateBlockFileSizes(long blockId, Dictionary<string, long> fileSizes) {

			using(this.blockLocker.Lock()) {
				(long index, long startingBlockId, long endingBlockId) index = this.FindBlockIndex(blockId);

				string folderPath = this.BlockchainEventSerializationFal.GetBlockPath(index.index);

				foreach(KeyValuePair<string, long> entry in fileSizes) {

					string filename = Path.Combine(folderPath, entry.Key);

					if(entry.Value <= 0) {
						if(this.centralCoordinator.FileSystem.FileExists(filename)) {
							this.centralCoordinator.FileSystem.DeleteFile(filename);
						}
					} else {
						if(this.centralCoordinator.FileSystem.FileExists(filename) && (this.centralCoordinator.FileSystem.GetFileLength(filename) != entry.Value)) {
							using Stream fileStream = this.centralCoordinator.FileSystem.OpenFile(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);

							fileStream.SetLength(entry.Value);

						}
					}
				}

				// now any file not in the list must be deleted
				if(this.centralCoordinator.FileSystem.DirectoryExists(folderPath)) {

					// select files that were actually existing with data
					List<string> snapshotFiles = fileSizes.Where(f => f.Value > 0).Select(f => f.Key).ToList();

					foreach(string entry in this.centralCoordinator.FileSystem.EnumerateFiles(folderPath).Select(Path.GetFileName).Where(f => !snapshotFiles.Contains(f))) {
						string fullPath = Path.Combine(folderPath, entry);

						if(this.centralCoordinator.FileSystem.FileExists(fullPath)) {
							this.centralCoordinator.FileSystem.DeleteFile(fullPath);
						}
					}
				}
			}
		}
		
		public Task SaveCachedTHSState(THSState state, string key) {
			
			string cachePath = this.GetTHSCachePath();

			FileExtensions.EnsureDirectoryStructure(cachePath);

			string filename = Path.Combine(cachePath, key.CleanInvalidFileNameCharacters());
			
			return this.fileSystem.WriteAllTextAsync(filename, System.Text.Json.JsonSerializer.Serialize(state));
		}
		
		public void ClearCachedTHSState(string key) {
			try {
				string filename = Path.Combine(this.GetTHSCachePath(), key.CleanInvalidFileNameCharacters());

				if(this.fileSystem.FileExists(filename)) {
					this.fileSystem.DeleteFile(filename);
				}
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to clear THS cache entry");
			}
		}

		public Task CachePreviousBlockXmssKeySignaturePathCache() {

			var bytes = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockXmssKeySignaturePathCache;

			if(bytes != null) {
				FileExtensions.EnsureDirectoryStructure(this.GetGeneralCachePath(), this.CentralCoordinator.FileSystem);
				
				this.CentralCoordinator.FileSystem.WriteAllBytes(this.GetLastBlockXmssKeySignaturePathCachePath(), bytes);
			}
			
			return Task.CompletedTask;
		}
	#region imports from Serialization Service

		public async Task SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, Enums.KeyHashType hashType, Enums.KeyHashType backupBitSize, byte ordinal, LockContext lockContext = null) {

			using(await this.transactionalLocker.LockAsync(lockContext).ConfigureAwait(false)) {

				async Task Action() {
					using(key) {
						await this.BlockchainEventSerializationFal.SaveAccountKeyIndex(accountId, key, treeHeight, hashType, backupBitSize, ordinal).ConfigureAwait(false);
					}
				}

				if(this.serializationTransactionProcessor != null) {

					(SafeArrayHandle keyBytes, byte treeheight, byte noncesExponent, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)? keyData = await this.ChainDataLoadProvider.LoadAccountKeyFromIndex(accountId, ordinal).ConfigureAwait(false);
					SerializationKeyDictionaryOperations undoOperation = null;

					// we undo if we had a previous key. otherwise, leave it there as junk
					if(keyData.HasValue && (keyData.Value != default)) {
						undoOperation = new SerializationKeyDictionaryOperations(this);
						undoOperation.AccountId = accountId;
						undoOperation.Ordinal = ordinal;
						undoOperation.Key.Entry = keyData.Value.keyBytes.Entry;
						undoOperation.TreeHeight = keyData.Value.treeheight;
						undoOperation.NoncesExponent = keyData.Value.noncesExponent;

						undoOperation.HashType = keyData.Value.hashType;
						undoOperation.BackupHashType = keyData.Value.backupHashType;
					}

					this.serializationTransactionProcessor.AddOperation(Action, undoOperation);
				} else {
					await Action().ConfigureAwait(false);
				}

			}

		}

	#endregion

	#region gossip message cache

		/// <summary>
		///     Here we take a block message we can't validate yet and potentially cache it if we can use it later.
		/// </summary>
		/// <param name="blockEnvelope"></param>
		public virtual async Task CacheUnvalidatedBlockGossipMessage(IBlockEnvelope unvalidatedBlockEnvelope, long xxHash) {

			using(await gossipCacheLocker.LockAsync().ConfigureAwait(false)) {
				string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
				IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

				bool result = await messageRegistryDal.CacheUnvalidatedBlockGossipMessage(unvalidatedBlockEnvelope.BlockId, xxHash).ConfigureAwait(false);

				if(result) {
					// ok, let's save the file

					string folderPath = this.GetBlocksGossipCacheFolderPath();

					FileExtensions.EnsureDirectoryStructure(folderPath, this.centralCoordinator.FileSystem);

					string completeFile = this.GetUnvalidatedBlockGossipMessageFullFileName(unvalidatedBlockEnvelope.BlockId, xxHash);

					if(!this.centralCoordinator.FileSystem.FileExists(completeFile)) {
						FileExtensions.WriteAllBytes(completeFile, unvalidatedBlockEnvelope.DehydrateEnvelope(), this.centralCoordinator.FileSystem);
					}
				}
			}
		}

		public async Task ClearCachedUnvalidatedBlockGossipMessage(long blockId) {

			using(await gossipCacheLocker.LockAsync().ConfigureAwait(false)) {
				string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();
				IMessageRegistryDal messageRegistryDal = this.centralCoordinator.BlockchainServiceSet.DataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

				string folderPath = this.GetBlocksGossipCacheFolderPath();
				FileExtensions.EnsureDirectoryStructure(folderPath, this.centralCoordinator.FileSystem);

				List<(long blockId, long xxHash)> deletedEntries = await messageRegistryDal.RemoveCachedUnvalidatedBlockGossipMessages(blockId).ConfigureAwait(false);

				// delete the files
				foreach((long blockId, long xxHash) entry in deletedEntries) {
					try {
						string completeFile = this.GetUnvalidatedBlockGossipMessageFullFileName(entry.blockId, entry.xxHash);

						if(this.centralCoordinator.FileSystem.FileExists(completeFile)) {
							this.centralCoordinator.FileSystem.DeleteFile(completeFile);
						}
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, $"Failed to delete a cached gossip block message file for block Id {entry.blockId}");
					}
				}
			}
		}

		public Task WriteGlobalWalletKeyIndexCacheFile(Guid fileName, SafeArrayHandle bytes) {

			FileExtensions.EnsureDirectoryStructure(GetWalletKeyIndexCachePath(), this.centralCoordinator.FileSystem);

			string file = this.GetLocalWalletKeyIndexCacheFileName(fileName);

			return FileExtensions.WriteAllBytesAsync(file, bytes.Span, CentralCoordinator.FileSystem);
		}

	#endregion

	#region Simple transactional System

		protected SerializationTransactionProcessor serializationTransactionProcessor;

		/// <summary>
		///     This method will run a series of operations using our very simple transactional system.
		/// </summary>
		/// <param name="serializationActions"></param>
		/// <param name="taskRoutingContext"></param>
		public async Task RunTransactionalActions(List<Func<LockContext, Task>> serializationActions, SerializationTransactionProcessor serializationTransactionProcessor) {

			using(LockHandle handle = await this.transactionalLocker.LockAsync().ConfigureAwait(false)) {

				this.BeginTransaction(serializationTransactionProcessor);

				try {
					foreach(Func<LockContext, Task> action in serializationActions.Where(a => a != null)) {
						try {
							await action(handle).ConfigureAwait(false);
						} catch(Exception ex) {
							// ok, here we can log the error, but we simply let errors go here. if any transaction causes a bug, we let it pass instead of crashing anything
							this.CentralCoordinator.Log.Error(ex, "Failed to run serialization action. Report this to the administrators but otherwise, here we ignore it and continue.");
						}
					}

					await this.CommitTransaction().ConfigureAwait(false);
				} catch(Exception ex) {
					this.RollbackTransaction();

					throw;
				}
			}
		}

		/// <summary>
		///     This is a very simple transactional system for Key dictionary. It should be improved in the future to something more
		///     robust
		/// </summary>
		protected void BeginTransaction(SerializationTransactionProcessor serializationTransactionProcessor) {

			this.serializationTransactionProcessor = serializationTransactionProcessor;
		}

		protected async Task CommitTransaction() {
			if(this.serializationTransactionProcessor != null) {
				await this.serializationTransactionProcessor.Apply().ConfigureAwait(false);
			}

			this.serializationTransactionProcessor = null;
		}

		protected void RollbackTransaction() {

			try {
				this.serializationTransactionProcessor?.Rollback();
			} finally {
				this.serializationTransactionProcessor = null;
			}
		}

	#endregion

	}
}