using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface ISerializationManager : IManagerBase {

		(ChannelsEntries<int> sizes, SafeArrayHandle hash)? GetBlockSizeAndHash(BlockId blockId);
		ChannelsEntries<SafeArrayHandle> LoadBlockSlice(BlockId blockId, ChannelsEntries<(int offset, int length)> offsets);
		(List<int> sliceHashes, int hash)? BuildBlockSliceHashes(BlockId blockId, List<ChannelsEntries<(int offset, int length)>> slices);
		
		SafeArrayHandle LoadBlockHeaderSlice(BlockId blockId, int offset, int length);
		SafeArrayHandle LoadBlockContentsSlice(BlockId blockId, int offset, int length);

		SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length);

		ChannelsEntries<int> GetBlockSize(BlockId blockId);
		int? GetBlockHeaderSize(BlockId blockId);
		int? GetBlockContentsSize(BlockId blockId);

		void SerializeBlock(IDehydratedBlock dehydratedBlock);
		void SerializeBlockchainMessage(IDehydratedBlockchainMessage dehydratedBlockchainMessage);
		ChannelsEntries<SafeArrayHandle> LoadBlockData(BlockId blockId);
		SafeArrayHandle LoadBlockHash(BlockId blockId);
		IKeyedTransaction LoadKeyedTransaction(KeyAddress keyAddress);

		Dictionary<AccountId, SafeArrayHandle> LoadKeys(List<KeyAddress> keyAddresses);
		Dictionary<AccountId, ICryptographicKey> LoadFullKeys(List<KeyAddress> keyAddresses);

		Dictionary<AccountId, T> LoadFullKeys<T>(List<KeyAddress> keyAddresses)
			where T : class, ICryptographicKey;

		SafeArrayHandle LoadKey(KeyAddress keyAddress);
		ICryptographicKey LoadFullKey(KeyAddress keyAddress);

		T LoadFullKey<T>(KeyAddress keyAddress)
			where T : class, ICryptographicKey;

		SafeArrayHandle LoadDigestKey(KeyAddress keyAddress);
		SafeArrayHandle LoadDigestKey(AccountId accountId, byte ordinal);

		IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType);
		IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId);
		IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId);

		List<IStandardAccountKeysDigestChannelCard> LoadDigestAccountKeyCards(long accountId);

		List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards();
		IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id);
		bool CheckRegistryMessageInCache(long messagexxHash, bool validated);

		IBlock LoadBlock(BlockId blockId);
		string LoadBlockJson(BlockId blockId);

		T LoadBlock<T>(BlockId blockId)
			where T : class, IBlock;

		IBlockchainDigest LoadDigestHeader(int digestId);
		SafeArrayHandle LoadDigestHeaderArchiveData(int digestId, int offset, int length);
		SafeArrayHandle LoadDigestHeaderArchiveData(int digestId);

		T LoadDigestHeader<T>(int digestId)
			where T : class, IBlockchainDigest;

		int GetDigestHeaderSize(int digestId);
		ValidatingDigestChannelSet CreateValidationDigestChannelSet(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor);

		void SaveDigestChannelDescription(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor);

		void UpdateCurrentDigest(IBlockchainDigest digest);

		void SaveDigestHeader(int digestId, SafeArrayHandle digestHeader);
		void SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, byte hashBits, byte ordinal);

		void CacheUnvalidatedBlockGossipMessage(IBlockEnvelope blockEnvelope, long xxHash);
		List<(IBlockEnvelope envelope, long xxHash)> GetCachedUnvalidatedBlockGossipMessage(long blockId);
		void ClearCachedUnvalidatedBlockGossipMessage(long blockId);

		void RunTransactionalActions(List<Action<ISerializationManager, TaskRoutingContext>> serializationActions, SerializationTransactionProcessor serializationTransactionProcessor, TaskRoutingContext taskRoutingContext);
	}

	public interface ISerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IManagerBase<SerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISerializationManager
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataWriteProvider { get; }
	}

	public abstract class SerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ManagerBase<SerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ISerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly IDataAccessService dataAccessService;

		protected readonly IGuidService guidService;

		protected readonly ITimeService timeService;

		private IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainDataWriteProvider;

		public SerializationManager(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, 1) {
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.dataAccessService = centralCoordinator.BlockchainServiceSet.DataAccessService;
			this.centralCoordinator = centralCoordinator;
		}

		private IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataLoadProvider => this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase;

		public IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataWriteProvider => (IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>) this.ChainDataLoadProvider;

		public void SerializeBlock(IDehydratedBlock dehydratedBlock) {

			this.ChainDataWriteProvider.SerializeBlock(dehydratedBlock);
		}

		public void SerializeBlockchainMessage(IDehydratedBlockchainMessage dehydratedBlockchainMessage) {
			this.ChainDataWriteProvider.SerializeBlockchainMessage(dehydratedBlockchainMessage);
		}

		public ChannelsEntries<SafeArrayHandle> LoadBlockData(BlockId blockId) {

			return this.ChainDataLoadProvider.LoadBlockData(blockId.Value);
		}

		public IBlock LoadBlock(BlockId blockId) {

			return this.ChainDataLoadProvider.LoadBlock(blockId.Value);
		}

		public T LoadBlock<T>(BlockId blockId)
			where T : class, IBlock {

			return this.ChainDataLoadProvider.LoadBlock<T>(blockId.Value);
		}

		/// <summary>
		///     Load a block and serialize it to a json string
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public string LoadBlockJson(BlockId blockId) {
			IBlock block = this.LoadBlock(blockId);

			if(block == null) {
				return "";
			}

			return JsonUtils.SerializeJsonSerializable(block);
		}

		public IBlockchainDigest LoadDigestHeader(int digestId) {
			return this.ChainDataLoadProvider.LoadDigestHeader(digestId);
		}

		public SafeArrayHandle LoadDigestHeaderArchiveData(int digestId, int offset, int length) {
			return this.ChainDataLoadProvider.LoadDigestHeaderArchiveData(digestId, offset, length);
		}

		public SafeArrayHandle LoadDigestHeaderArchiveData(int digestId) {
			return this.ChainDataLoadProvider.LoadDigestHeaderArchiveData(digestId);
		}

		public T LoadDigestHeader<T>(int digestId)
			where T : class, IBlockchainDigest {
			return this.ChainDataLoadProvider.LoadDigestHeader<T>(digestId);
		}

		public int GetDigestHeaderSize(int digestId) {
			return this.ChainDataLoadProvider.GetDigestHeaderSize(digestId);
		}

		public ValidatingDigestChannelSet CreateValidationDigestChannelSet(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor) {
			return this.ChainDataLoadProvider.CreateValidationDigestChannelSet(digestId, blockchainDigestDescriptor);
		}

		public void SaveDigestChannelDescription(int digestId, BlockchainDigestDescriptor blockchainDigestDescriptor) {
			this.ChainDataWriteProvider.SaveDigestChannelDescription(digestId, blockchainDigestDescriptor);
		}

		/// <summary>
		///     here we update the active digest, so we delete everything before it, if we should
		/// </summary>
		/// <param name="digestId"></param>
		public void UpdateCurrentDigest(IBlockchainDigest digest) {
			this.ChainDataWriteProvider.UpdateCurrentDigest(digest.DigestId, digest.BlockId.Value);
		}

		public void SaveDigestHeader(int digestId, SafeArrayHandle digestHeader) {
			this.ChainDataWriteProvider.SaveDigestHeader(digestId, digestHeader);
		}

		/// <summary>
		///     a special optimization method to query the size and hash in a single call
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public (ChannelsEntries<int> sizes, SafeArrayHandle hash)? GetBlockSizeAndHash(BlockId blockId) {

			(int offset, int length) hashOffsets = BlockHeader.GetBlockHashOffsets(blockId);

			return this.ChainDataLoadProvider.LoadBlockSizeAndHash(blockId.Value, hashOffsets.offset, hashOffsets.length);
		}

		public ChannelsEntries<int> GetBlockSize(BlockId blockId) {
			return this.ChainDataLoadProvider.LoadBlockSize(blockId.Value);
		}

		public int? GetBlockHeaderSize(BlockId blockId) {

			return this.ChainDataLoadProvider.LoadBlockWholeHeaderSize(blockId.Value);
		}

		public int? GetBlockContentsSize(BlockId blockId) {

			return this.ChainDataLoadProvider.LoadBlockContentsSize(blockId.Value);
		}

		public SafeArrayHandle LoadBlockHeaderSlice(BlockId blockId, int offset, int length) {

			return this.ChainDataLoadProvider.LoadBlockPartialHighHeaderData(blockId.Value, offset, offset);
		}

		public SafeArrayHandle LoadBlockContentsSlice(BlockId blockId, int offset, int length) {

			return this.ChainDataLoadProvider.LoadBlockPartialContentsData(blockId.Value, offset, offset);
		}

		public SafeArrayHandle LoadDigestFile(DigestChannelType channelId, int indexId, int fileId, uint partIndex, long offset, int length) {
			return this.ChainDataLoadProvider.LoadDigestFile(channelId, indexId, fileId, partIndex, offset, length);
		}

		public (List<int> sliceHashes, int hash)? BuildBlockSliceHashes(BlockId blockId, List<ChannelsEntries<(int offset, int length)>> slices) {

			List<int> sliceHashes = new List<int>();
			HashNodeList topNodes = new HashNodeList();
			
			foreach(var slice in slices) {
				var sliceInfo = this.ChainDataLoadProvider.LoadBlockPartialData(blockId.Value, slice);

				List<SafeArrayHandle> datas = new List<SafeArrayHandle>();
				sliceInfo.RunForAll((flag, data) => {
					datas.Add(data);
				});
				
				int sliceHash = HashingUtils.GenerateBlockDataSliceHash(datas);
				sliceHashes.Add(sliceHash);
				topNodes.Add(sliceHash);
			}
			 
			return (sliceHashes, HashingUtils.HashxxTree32(topNodes));
		}
		
		public ChannelsEntries<SafeArrayHandle> LoadBlockSlice(BlockId blockId, ChannelsEntries<(int offset, int length)> offsets) {

			return this.ChainDataLoadProvider.LoadBlockPartialData(blockId.Value, offsets);
		}

		public SafeArrayHandle LoadBlockHash(BlockId blockId) {
			IBlock cachedBlock = this.ChainDataLoadProvider.GetCachedBlock(blockId.Value);

			if(cachedBlock != null) {
				return cachedBlock.Hash;
			}

			(int offset, int length) hashOffsets = BlockHeader.GetBlockHashOffsets(blockId);

			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockPartialTransactionBytes(blockId.Value, hashOffsets.offset, hashOffsets.length);
		}

		public IKeyedTransaction LoadKeyedTransaction(KeyAddress keyAddress) {
			IBlock cachedBlock = this.ChainDataLoadProvider.GetCachedBlock(keyAddress.AnnouncementBlockId.Value);

			if(cachedBlock != null) {
				IKeyedTransaction transaction = cachedBlock.ConfirmedKeyedTransactions.SingleOrDefault(t => t.TransactionId == keyAddress.DeclarationTransactionId);

				if(transaction != null) {
					return transaction;
				}
			}

			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadKeyedTransaction(keyAddress);
		}

		public Dictionary<AccountId, SafeArrayHandle> LoadKeys(List<KeyAddress> keyAddresses) {
			var accountKeys = new Dictionary<AccountId, SafeArrayHandle>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadKey(keyAddress));
			}

			return accountKeys;
		}

		public Dictionary<AccountId, ICryptographicKey> LoadFullKeys(List<KeyAddress> keyAddresses) {
			var accountKeys = new Dictionary<AccountId, ICryptographicKey>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadFullKey(keyAddress));
			}

			return accountKeys;
		}

		public Dictionary<AccountId, T> LoadFullKeys<T>(List<KeyAddress> keyAddresses)
			where T : class, ICryptographicKey {
			var accountKeys = new Dictionary<AccountId, T>();

			foreach(KeyAddress keyAddress in keyAddresses) {
				accountKeys.Add(keyAddress.DeclarationTransactionId.Account, this.LoadFullKey<T>(keyAddress));
			}

			return accountKeys;
		}

		public SafeArrayHandle LoadKey(KeyAddress keyAddress) {

			bool digestScope = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockWithinDigest(keyAddress.AnnouncementBlockId.Value);

			if(digestScope) {
				return this.LoadDigestKey(keyAddress);
			}

			//TODO: loading the entire keyset is not very efficient if we want only one key. optimize
			// lets load from the block
			IKeyedTransaction keyedTransaction = this.LoadKeyedTransaction(keyAddress);

			ICryptographicKey key = keyedTransaction.Keyset.Keys[keyAddress.OrdinalId];
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			key.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public ICryptographicKey LoadFullKey(KeyAddress keyAddress) {

			return this.LoadFullKey<ICryptographicKey>(keyAddress);
		}

		public T LoadFullKey<T>(KeyAddress keyAddress)
			where T : class, ICryptographicKey {

			bool digestScope = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockWithinDigest(keyAddress.AnnouncementBlockId.Value);

			if(digestScope) {
				SafeArrayHandle keyBytes = this.LoadDigestKey(keyAddress);

				return KeyFactory.RehydrateKey<T>(DataSerializationFactory.CreateRehydrator(keyBytes));
			}

			//TODO: loading the entire keyset is not very efficient if we want only one key. optimize
			// lets load from the block
			IKeyedTransaction keyedTransaction = this.LoadKeyedTransaction(keyAddress);

			return (T) keyedTransaction.Keyset.Keys[keyAddress.OrdinalId];
		}

		public SafeArrayHandle LoadDigestKey(KeyAddress keyAddress) {
			return this.LoadDigestKey(keyAddress.DeclarationTransactionId.Account, keyAddress.OrdinalId);
		}

		public IAccountSnapshotDigestChannelCard LoadDigestAccount(long accountSequenceId, Enums.AccountTypes accountType) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestAccount(accountSequenceId, accountType);
		}

		public IStandardAccountSnapshotDigestChannelCard LoadDigestStandardAccount(long accountId) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccount(accountId);
		}

		public IJointAccountSnapshotDigestChannelCard LoadDigestJointAccount(long accountId) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestJointAccount(accountId);
		}

		public SafeArrayHandle LoadDigestKey(AccountId accountId, byte ordinal) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestKey(accountId, ordinal);
		}

		public List<IStandardAccountKeysDigestChannelCard> LoadDigestAccountKeyCards(long accountId) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestStandardAccountKeyCards(accountId);
		}

		public List<IAccreditationCertificateDigestChannelCard> LoadDigestAccreditationCertificateCards() {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestAccreditationCertificateCards();
		}

		public IAccreditationCertificateDigestChannelCard LoadDigestAccreditationCertificateCard(int id) {
			return this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadDigestAccreditationCertificateCard(id);
		}

		public void SaveAccountKeyIndex(AccountId accountId, SafeArrayHandle key, byte treeHeight, byte hashBits, byte ordinal) {

			void Action() {
				this.ChainDataWriteProvider.SaveAccountKeyIndex(accountId, key, treeHeight, hashBits, ordinal);
			}

			if(this.serializationTransactionProcessor != null) {

				var keyData = this.ChainDataLoadProvider.LoadAccountKeyFromIndex(accountId, ordinal);
				SerializationFastKeysOperations undoOperation = null;
				
				// we undo if we had a previous key. otherwise, leave it there as junk
				if(keyData.HasValue && keyData.Value != default) {
					undoOperation = new SerializationFastKeysOperations(this, this.ChainDataWriteProvider);
					undoOperation.AccountId = accountId;
					undoOperation.Ordinal = ordinal;
					undoOperation.Key.Entry = keyData.Value.keyBytes.Entry;
					undoOperation.TreeHeight = keyData.Value.treeheight;
					undoOperation.HashBits = keyData.Value.hashBits;

				}

				this.serializationTransactionProcessor.AddOperation(Action, undoOperation);
			} else {
				Action();
			}

		}

	#region Simple transactional System

		protected SerializationTransactionProcessor serializationTransactionProcessor;

		/// <summary>
		///     This method will run a series of operations using our very simple transactional system.
		/// </summary>
		/// <param name="serializationActions"></param>
		/// <param name="taskRoutingContext"></param>
		public void RunTransactionalActions(List<Action<ISerializationManager, TaskRoutingContext>> serializationActions, SerializationTransactionProcessor serializationTransactionProcessor, TaskRoutingContext taskRoutingContext) {

			this.BeginTransaction(serializationTransactionProcessor);

			try {
				foreach(var action in serializationActions.Where(a => a != null)) {
					action(this, taskRoutingContext);
				}

				this.CommitTransaction();
			} catch(Exception ex) {
				this.RollbackTransaction();

				throw;
			}
		}

		/// <summary>
		///     This is a very simple transactional system for fast keys. It should be improved in the future to something more
		///     robust
		/// </summary>
		protected void BeginTransaction(SerializationTransactionProcessor serializationTransactionProcessor) {


			this.serializationTransactionProcessor = serializationTransactionProcessor;
		}

		protected void CommitTransaction() {
			this.serializationTransactionProcessor?.Apply();
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

	#region Message Registry

		public bool CheckRegistryMessageInCache(long messagexxHash, bool validated) {
			string walletPath = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath();

			//TODO: must revise this below. caused by refactoring

			IMessageRegistryDal messageRegistryDal = this.dataAccessService.CreateMessageRegistryDal(walletPath, this.centralCoordinator.BlockchainServiceSet);

			return messageRegistryDal.CheckMessageInCache(messagexxHash, validated);
		}

		/// <summary>
		///     Here we take a block message we can't validate yet and potentially cache it if we can use it later.
		/// </summary>
		/// <param name="blockEnvelope"></param>
		public virtual void CacheUnvalidatedBlockGossipMessage(IBlockEnvelope unvalidatedBlockEnvelope, long xxHash) {

			Log.Verbose($"Caching unvalidated gossip message for potential block Id {unvalidatedBlockEnvelope.BlockId}");

			this.ChainDataWriteProvider.CacheUnvalidatedBlockGossipMessage(unvalidatedBlockEnvelope, xxHash);
		}

		public List<(IBlockEnvelope envelope, long xxHash)> GetCachedUnvalidatedBlockGossipMessage(long blockId) {
			return this.ChainDataLoadProvider.GetCachedUnvalidatedBlockGossipMessage(blockId);
		}

		public void ClearCachedUnvalidatedBlockGossipMessage(long blockId) {

			this.ChainDataWriteProvider.ClearCachedUnvalidatedBlockGossipMessage(blockId);
		}

	#endregion

	}
}