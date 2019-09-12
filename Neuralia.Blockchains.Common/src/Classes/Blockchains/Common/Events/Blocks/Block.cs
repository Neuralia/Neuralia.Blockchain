using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks {

	public interface IBlockHeader : IBlockchainEvent<IDehydratedBlock, IBlockchainEventsRehydrationFactory, BlockType> {

		IByteArray Hash { get; set; }

		BlockId BlockId { get; set; }

		TransactionTimestamp Timestamp { get; set; }

		AdaptiveShort1_2 Lifespan { get; set; }

		DateTime FullTimestamp { get; set; }

		BlockSignatureSet SignatureSet { get; set; }

		List<IKeyedTransaction> ConfirmedKeyedTransactions { get; }
		List<(int offset, int lengt)> KeyedOffsets { get; }

		void BuildKeyedOffsets();
	}

	public interface IBlock : IBlockHeader {

		List<ITransaction> ConfirmedTransactions { get; }
		List<RejectedTransaction> RejectedTransactions { get; }

		List<IFinalElectionResults> FinalElectionResults { get; }
		List<IIntermediaryElectionResults> IntermediaryElectionResults { get; }

		HashNodeList GetStructuresArray(IByteArray previousBlockHash);

		List<TransactionId> GetAllTransactions();
		List<(TransactionId TransactionId, int index)> GetAllIndexedTransactions();
		Dictionary<int, TransactionId> GetAllIndexedTransactionsDictionary();
		Dictionary<TransactionId, ITransaction> GetAllConfirmedTransactions();
	}

	public abstract class BlockHeader : BlockchainEvent<IDehydratedBlock, DehydratedBlock, IBlockchainEventsRehydrationFactory, BlockType>, IBlockHeader {

		/// <summary>
		///     sha3 512 size.
		/// </summary>
		public const int BLOCK_HASH_BYTE_SIZE = 64;

		/// <summary>
		///     This is the actual date time of the timestamp once adjusted to chain inception
		/// </summary>
		public DateTime FullTimestamp { get; set; }

		public IByteArray Hash { get; set; } = new ByteArray();

		// header
		public BlockId BlockId { get; set; } = BlockId.NullBlockId;

		/// <summary>
		///     The timestamp since chain inception
		/// </summary>
		public TransactionTimestamp Timestamp { get; set; } = new TransactionTimestamp();

		// envelope
		public BlockSignatureSet SignatureSet { get; set; } = new BlockSignatureSet();

		/// <summary>
		///     amount of time in increments of 10 seconds in which we should be expecting the next block.
		///     0 means infinite.
		/// </summary>
		public AdaptiveShort1_2 Lifespan { get; set; } = new AdaptiveShort1_2();

		public List<IKeyedTransaction> ConfirmedKeyedTransactions { get; } = new List<IKeyedTransaction>();

		public override HashNodeList GetStructuresArray() {
			throw new NotImplementedException("Blocks do not implement this version of the structures array.");
		}

		public virtual HashNodeList GetStructuresArray(IByteArray previousBlockHash) {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BlockId.GetStructuresArray());

			nodeList.Add(previousBlockHash);

			nodeList.Add(this.Timestamp);
			nodeList.Add(this.Lifespan);

			nodeList.Add(this.ConfirmedKeyedTransactions.Count);
			nodeList.Add(this.ConfirmedKeyedTransactions.OrderBy(t => t.TransactionId));

			return nodeList;
		}

		/// <summary>
		///     This is a very special method for the moderator. we need to know the offsets of the hash in the block. So, this
		///     method MUST match the block headers. of the block header structure changes, this one must too!
		/// </summary>
		/// <param name="blockId"></param>
		/// <returns></returns>
		public static (int offset, int length) GetBlockHashOffsets(BlockId blockId) {

			// the 3 type, major and minor + the small array size of 1 byte
			return (3 + 1, BLOCK_HASH_BYTE_SIZE);
		}

		public override bool Equals(object obj) {
			if(obj is IBlockHeader blockHeadrer) {
				return blockHeadrer.BlockId == this.BlockId;
			}

			return base.Equals(obj);
		}

		public override int GetHashCode() {
			return this.BlockId.GetHashCode();
		}

		public override string ToString() {
			return this.BlockId.ToString();
		}

	#region Serialization

		public List<(int offset, int lengt)> KeyedOffsets { get; private set; }

		public void BuildKeyedOffsets() {
			this.KeyedOffsets = new List<(int offset, int lengt)>();
		}

		public static (ComponentVersion<BlockType> version, IByteArray hash, BlockId blockId) RehydrateHeaderEssentials(IDataRehydrator rehydratorHeader) {
			var rehydratedVersion = rehydratorHeader.Rehydrate<ComponentVersion<BlockType>>();

			IByteArray hash = rehydratorHeader.ReadSmallArray();

			BlockId blockId = new BlockId();
			blockId.Rehydrate(rehydratorHeader);

			return (rehydratedVersion, hash, blockId);
		}

		public override void Rehydrate(IDehydratedBlock dehydratedBlock, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			GzipCompression compressor = null;

			List<IByteArray> toReturn = new List<IByteArray>();
			var channelRehydrators = dehydratedBlock.GetEssentialDataChannels().ConvertAll((band, data) => {

				// make sure we dotn return the data here, its used by dehydratedBlock. it would cause a serious issue.
				IByteArray bytes = data;

				bool compressed = false;

				// decompress if we should
				if(rehydrationFactory.CompressedBlockchainChannels.HasFlag(band)) {
					if(compressor == null) {
						compressor = new GzipCompression();
					}

					bytes = compressor.Decompress(data);
					toReturn.Add(bytes);
					compressed = true;
				}

				IDataRehydrator results = DataSerializationFactory.CreateRehydrator(bytes);
				
				return results;
			});

			IDataRehydrator rehydratorHeader = channelRehydrators.HighHeaderData;

			var essentialHeader = RehydrateHeaderEssentials(rehydratorHeader);
			this.Version.EnsureEqual(essentialHeader.version);

			this.Hash = essentialHeader.hash;
			this.BlockId = essentialHeader.blockId;

			this.Timestamp.Rehydrate(rehydratorHeader);
			this.Lifespan.Rehydrate(rehydratorHeader);

			this.SignatureSet.Rehydrate(rehydratorHeader);

			// timestamp baseline
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydratorHeader);
			long timestampBaseline = adaptiveLong.Value;

			adaptiveLong.Rehydrate(rehydratorHeader);
			int count = (ushort) adaptiveLong.Value;

			for(int i = 0; i < count; i++) {

				// now the keyed transaction's starting address
				int offset = rehydratorHeader.Offset;

				// keyed transactions have their own independent rehydrator array which contains only the header (body)
				IByteArray keyedBytes = rehydratorHeader.ReadNonNullableArray();
				IDataRehydrator keyedRehydrator = DataSerializationFactory.CreateRehydrator(keyedBytes);

				DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
				dehydratedTransaction.Rehydrate(keyedRehydrator);

				IKeyedTransaction keyedTransaction = rehydrationFactory.CreateKeyedTransaction(dehydratedTransaction);
				keyedTransaction.Rehydrate(dehydratedTransaction, rehydrationFactory);

				int nextOffset = rehydratorHeader.Offset;

				// and give it its address
				this.KeyedOffsets?.Add((offset, nextOffset - offset));

				this.ConfirmedKeyedTransactions.Add(keyedTransaction);
			}

			this.Rehydrate(channelRehydrators, timestampBaseline, rehydrationFactory);

			this.PrepareRehydrated(rehydrationFactory);

			foreach(var entry in toReturn) {
				entry.Return();
			}
		}

		protected virtual void PrepareRehydrated(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			rehydrationFactory.PrepareBlockHeader(this);
		}

		protected virtual void Rehydrate(ChannelsEntries<IDataRehydrator> channelRehydrators, long timestampBaseline, IBlockchainEventsRehydrationFactory rehydrationFactory) {

		}

		protected ITransaction RehydrateTransaction(ChannelsEntries<IDataRehydrator> dataChannels, IBlockchainEventsRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp) {
			DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

			dehydratedTransaction.Rehydrate(dataChannels, accountId, timestamp);

			return dehydratedTransaction.RehydrateTransaction(rehydrationFactory, accountId, timestamp);
		}

		protected ITransaction RehydrateTransaction(ChannelsEntries<IDataRehydrator> dataChannels, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			return this.RehydrateTransaction(dataChannels, rehydrationFactory, null, null);
		}

		public override sealed IDehydratedBlock Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels) {
			// do nothing here, we really never dehydrate a block
			throw new NotImplementedException();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("BlockId", this.BlockId);
			jsonDeserializer.SetProperty("Hash", this.Hash);
			jsonDeserializer.SetProperty("Timestamp", this.Timestamp);
			jsonDeserializer.SetProperty("FullTimestamp", this.FullTimestamp.ToUniversalTime().ToString("o"));
			jsonDeserializer.SetProperty("Lifespan", this.Lifespan);
			jsonDeserializer.SetProperty("SignatureSet", this.SignatureSet);

			jsonDeserializer.SetArray("ConfirmedKeyedTransactions", this.ConfirmedKeyedTransactions);

		}

	#endregion

	}

	public abstract class Block : BlockHeader, IBlock {

		public List<ITransaction> ConfirmedTransactions { get; } = new List<ITransaction>();
		public List<RejectedTransaction> RejectedTransactions { get; } = new List<RejectedTransaction>();

		public List<IFinalElectionResults> FinalElectionResults { get; } = new List<IFinalElectionResults>();
		public List<IIntermediaryElectionResults> IntermediaryElectionResults { get; } = new List<IIntermediaryElectionResults>();

		public override HashNodeList GetStructuresArray(IByteArray previousBlockHash) {
			HashNodeList nodeList = base.GetStructuresArray(previousBlockHash);

			nodeList.Add(this.ConfirmedTransactions.Count);
			nodeList.Add(this.ConfirmedTransactions.OrderBy(t => t.TransactionId));

			nodeList.Add(this.RejectedTransactions.Count);
			nodeList.Add(this.RejectedTransactions.OrderBy(t => t.TransactionId));

			nodeList.Add(this.IntermediaryElectionResults.Count);
			nodeList.Add(this.IntermediaryElectionResults.OrderByDescending(t => t.BlockOffset));

			nodeList.Add(this.FinalElectionResults.Count);
			nodeList.Add(this.FinalElectionResults.OrderByDescending(t => t.BlockOffset));

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("ConfirmedTransactions", this.ConfirmedTransactions);
			jsonDeserializer.SetArray("RejectedTransactions", this.RejectedTransactions.Cast<IJsonSerializable>());

			jsonDeserializer.SetArray("IntermediaryElectionResults", this.IntermediaryElectionResults);
			jsonDeserializer.SetArray("FinalElectionResults", this.FinalElectionResults);
		}

		public List<TransactionId> GetAllTransactions() {
			var transactions = this.ConfirmedKeyedTransactions.Select(t => t.TransactionId.ToTransactionId()).ToList();
			transactions.AddRange(this.ConfirmedTransactions.Select(t => t.TransactionId.ToTransactionId()));
			transactions.AddRange(this.RejectedTransactions.Select(t => t.TransactionId.ToTransactionId()));

			return transactions;
		}

		public List<(TransactionId TransactionId, int index)> GetAllIndexedTransactions() {
			var transactionIndexes = this.ConfirmedKeyedTransactions.Select((t, index) => (t.TransactionId.ToTransactionId(), index)).ToList();
			int count = transactionIndexes.Count;
			transactionIndexes.AddRange(this.ConfirmedTransactions.Select((t, index) => (t.TransactionId.ToTransactionId(), count + index)));
			count = transactionIndexes.Count;
			transactionIndexes.AddRange(this.RejectedTransactions.Select((t, index) => (TransactionId: t.TransactionId.ToTransactionId(), count + index)));

			return transactionIndexes;
		}

		public Dictionary<int, TransactionId> GetAllIndexedTransactionsDictionary() {
			return this.GetAllIndexedTransactions().ToDictionary(t => t.index, t => t.TransactionId);
		}

		public Dictionary<TransactionId, ITransaction> GetAllConfirmedTransactions() {

			var results = this.ConfirmedTransactions.ToDictionary(t => t.TransactionId.ToTransactionId(), t => t);

			foreach(IKeyedTransaction t in this.ConfirmedKeyedTransactions) {
				results.Add(t.TransactionId.ToTransactionId(), t);
			}

			return results;
		}

	#region Serialization

		protected override sealed void Rehydrate(ChannelsEntries<IDataRehydrator> channelRehydrators, long timestampBaseline, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			IDataRehydrator rehydratorHeader = channelRehydrators.LowHeaderData;

			bool anyConfirmedTransactions = rehydratorHeader.ReadBool();

			if(anyConfirmedTransactions) {
				var confirmedTransactionSet = new List<BlockAccountSerializationSet>();

				RepeatableOffsetCalculator timestampsCalculator = new RepeatableOffsetCalculator(timestampBaseline);
				var confirmedTransactionResultSet = new List<BlockAccountSerializationSet>();

				var rparameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

				int currentGroupCount = 0;
				Enums.AccountTypes currentAccountType = Enums.AccountTypes.Standard;

				rparameters.InitializeGroup = (groupIndex, groupCount, accountType) => {
					currentGroupCount = groupCount;
					currentAccountType = accountType;
					timestampsCalculator.Reset(timestampBaseline);
				};

				rparameters.RehydrateExtraData = (accountId, offset, index, dh) => {
					timestampsCalculator.Reset(timestampBaseline);
					BlockAccountSerializationSet serializationSet = new BlockAccountSerializationSet(currentAccountType);

					serializationSet.Rehydrate(accountId, rehydratorHeader, channelRehydrators, (dataChannels, accountId2, timestamp) => this.RehydrateTransaction(dataChannels, rehydrationFactory, accountId2, timestamp), timestampsCalculator);

					confirmedTransactionSet.Add(serializationSet);
				};

				AccountIdGroupSerializer.Rehydrate(rehydratorHeader, false, rparameters);

				// thats it, add our ordered transactions
				this.ConfirmedTransactions.AddRange(confirmedTransactionSet.SelectMany(ts => ts.Transactions.Select(t => t.transaction)).OrderBy(t => t.TransactionId));
			}

			this.RejectedTransactions.Clear();
			rehydratorHeader.ReadRehydratableArray(this.RejectedTransactions);

			this.RehydrateElectionResults(rehydratorHeader, rehydrationFactory);

			this.RehydrateBody(rehydratorHeader, rehydrationFactory);

			this.RehydrateDataChannels(channelRehydrators, rehydrationFactory);

		}

		protected virtual void RehydrateBody(IDataRehydrator rehydratorHeader, IBlockchainEventsRehydrationFactory rehydrationFactory) {

		}

		protected void RehydrateElectionResults(IDataRehydrator rehydratorHeader, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			// now build the transaction indexes
			var transactionIndexesTree = this.GetAllIndexedTransactionsDictionary();

			int count = rehydratorHeader.ReadByte();

			IBlockComponentsRehydrationFactory blockComponentRehydrationFactory = rehydrationFactory.CreateBlockComponentsRehydrationFactory();
			IElectionResultsRehydrator electionResultsRehydrator = blockComponentRehydrationFactory.CreateElectionResultsRehydrator();

			if(count != 0) {
				transactionIndexesTree = transactionIndexesTree ?? this.GetAllIndexedTransactionsDictionary();

				for(byte i = 0; i < count; i++) {

					this.IntermediaryElectionResults.Add(electionResultsRehydrator.RehydrateIntermediateResults(rehydratorHeader, transactionIndexesTree));
				}
			}

			count = rehydratorHeader.ReadByte();

			if(count != 0) {
				transactionIndexesTree = transactionIndexesTree ?? this.GetAllIndexedTransactionsDictionary();

				for(byte i = 0; i < count; i++) {
					this.FinalElectionResults.Add(electionResultsRehydrator.RehydrateFinalResults(rehydratorHeader, transactionIndexesTree));
				}
			}

		}

		protected virtual void RehydrateDataChannels(ChannelsEntries<IDataRehydrator> dataChannels, IBlockchainEventsRehydrationFactory rehydrationFactory) {

		}

		protected override void PrepareRehydrated(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			rehydrationFactory.PrepareBlock(this);
		}

	#endregion

	}
}