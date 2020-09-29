using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
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
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks {

	public interface IBlockHeader : IBlockchainEvent<IDehydratedBlock, IBlockchainEventsRehydrationFactory, BlockType> {

		SafeArrayHandle Hash { get; }

		BlockId BlockId { get; set; }

		TransactionTimestamp Timestamp { get; set; }

		AdaptiveShort1_2 Lifespan { get; set; }

		DateTime FullTimestamp { get; set; }

		Enums.BlockHashingModes BlockHashingMode { get; set; }

		SafeArrayHandle ExtendedData { get; set; }

		BlockSignatureSet SignatureSet { get; set; }

		List<IIndexedTransaction> ConfirmedIndexedTransactions { get; }
		List<(int offset, int lengt)> MasterOffsets { get; }

		void BuildIndexedTransactionOffsets();
	}

	public interface IBlock : IBlockHeader {

		List<ITransaction> ConfirmedTransactions { get; }
		List<RejectedTransaction> RejectedTransactions { get; }

		List<IFinalElectionResults> FinalElectionResults { get; }
		List<IIntermediaryElectionResults> IntermediaryElectionResults { get; }

		HashNodeList GetStructuresArray(SafeArrayHandle previousBlockHash);

		List<TransactionId> GetAllTransactions();
		List<(TransactionId TransactionId, int index)> GetAllIndexedTransactions();
		Dictionary<int, TransactionId> GetAllIndexedTransactionsDictionary();
		Dictionary<TransactionId, ITransaction> GetAllConfirmedTransactions();
	}

	[DebuggerDisplay("BlockId: {BlockId}")]
	public abstract class BlockHeader : BlockchainEvent<IDehydratedBlock, DehydratedBlock, IBlockchainEventsRehydrationFactory, BlockType>, IBlockHeader {

		/// <summary>
		///     sha3 512 size.
		/// </summary>
		public const int BLOCK_HASH_BYTE_SIZE = 64;

		/// <summary>
		///     This is the actual date time of the timestamp once adjusted to chain inception
		/// </summary>
		public DateTime FullTimestamp { get; set; }

		public SafeArrayHandle Hash { get; } = SafeArrayHandle.Create();

		// header
		public BlockId BlockId { get; set; } = BlockId.NullBlockId;

		/// <summary>
		///     how the block is hashed
		/// </summary>
		public Enums.BlockHashingModes BlockHashingMode { get; set; } = Enums.BlockHashingModes.Mode1;

		/// <summary>
		///     Space for adhoc extra data if ever needed
		/// </summary>
		public SafeArrayHandle ExtendedData { get; set; }

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

		public List<IIndexedTransaction> ConfirmedIndexedTransactions { get; } = new List<IIndexedTransaction>();

		public override HashNodeList GetStructuresArray() {
			throw new NotImplementedException("Blocks do not implement this version of the structures array.");
		}

		public virtual HashNodeList GetStructuresArray(SafeArrayHandle previousBlockHash) {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.BlockId.GetStructuresArray());

			nodeList.Add(previousBlockHash);

			nodeList.Add(this.Timestamp);
			nodeList.Add(this.Lifespan);

			nodeList.Add(this.BlockHashingMode);
			nodeList.Add(this.ExtendedData);

			nodeList.Add(this.ConfirmedIndexedTransactions.Count);
			nodeList.Add(BlockchainHashingUtils.GenerateTransactionSetNodeList(this.ConfirmedIndexedTransactions));

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

			return false;
		}

		public override int GetHashCode() {
			return this.BlockId.GetHashCode();
		}

		public override string ToString() {
			return this.BlockId.ToString();
		}

	#region Serialization

		public List<(int offset, int lengt)> MasterOffsets { get; private set; }

		public void BuildIndexedTransactionOffsets() {
			this.MasterOffsets = new List<(int offset, int lengt)>();
		}

		public static (ComponentVersion<BlockType> version, SafeArrayHandle hash, BlockId blockId) RehydrateHeaderEssentials(IDataRehydrator rehydratorHeader) {
			ComponentVersion<BlockType> rehydratedVersion = rehydratorHeader.Rehydrate<ComponentVersion<BlockType>>();

			SafeArrayHandle hash = (SafeArrayHandle)rehydratorHeader.ReadSmallArray();

			BlockId blockId = new BlockId();
			blockId.Rehydrate(rehydratorHeader);

			return (rehydratedVersion, hash, blockId);
		}

		public override void Rehydrate(IDehydratedBlock dehydratedBlock, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			BrotliCompression compressor = null;

			List<SafeArrayHandle> toReturn = new List<SafeArrayHandle>();

			ChannelsEntries<IDataRehydrator> channelRehydrators = dehydratedBlock.GetEssentialDataChannels().ConvertAll((band, data) => {

				// make sure we dotn return the data here, its used by dehydratedBlock. it would cause a serious issue.
				SafeArrayHandle bytes = data;

				// decompress if we should
				if(rehydrationFactory.CompressedBlockchainChannels.HasFlag(band) && bytes.HasData) {
					if(compressor == null) {
						compressor = new BrotliCompression();
					}

					bytes = compressor.Decompress(data);
					toReturn.Add(bytes);
				}

				return DataSerializationFactory.CreateRehydrator(bytes);
			});
			
			IDataRehydrator rehydratorHeader = channelRehydrators.HighHeaderData;

			(ComponentVersion<BlockType> version, SafeArrayHandle hash, BlockId blockId) = RehydrateHeaderEssentials(rehydratorHeader);
			this.Version.EnsureEqual(version);

			this.Hash.Entry = hash.Entry;
			this.BlockId = blockId;

			this.Timestamp.Rehydrate(rehydratorHeader);
			this.Lifespan.Rehydrate(rehydratorHeader);

			this.BlockHashingMode = (Enums.BlockHashingModes) rehydratorHeader.ReadByte();
			this.ExtendedData = (SafeArrayHandle)rehydratorHeader.ReadArray();

			this.SignatureSet.Rehydrate(rehydratorHeader);

			// timestamp baseline
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydratorHeader);
			long timestampBaseline = adaptiveLong.Value;

			adaptiveLong.Rehydrate(rehydratorHeader);
			int count = (int) adaptiveLong.Value;

			for(int i = 0; i < count; i++) {

				// now the indexed transaction's starting address
				int offset = rehydratorHeader.Offset;

				// indexed transactions have their own independent rehydrator array which contains only the header (body)
				using SafeArrayHandle keyedBytes = (SafeArrayHandle)rehydratorHeader.ReadNonNullableArray();

				using IDataRehydrator keyedRehydrator = DataSerializationFactory.CreateRehydrator(keyedBytes);

				DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
				dehydratedTransaction.Rehydrate(keyedRehydrator);

				IIndexedTransaction indexedTransaction = rehydrationFactory.CreateIndexedTransaction(dehydratedTransaction);
				indexedTransaction.Rehydrate(dehydratedTransaction, rehydrationFactory);

				int nextOffset = rehydratorHeader.Offset;

				// and give it its address
				this.MasterOffsets?.Add((offset, nextOffset - offset));

				this.ConfirmedIndexedTransactions.Add(indexedTransaction);

			}

			this.Rehydrate(channelRehydrators, timestampBaseline, rehydrationFactory);

			this.PrepareRehydrated(rehydrationFactory);

			foreach(SafeArrayHandle entry in toReturn) {
				entry.Return();
			}

			foreach(IDataRehydrator entry in channelRehydrators.Entries.Values) {
				entry?.Dispose();
			}
		}

		protected virtual void PrepareRehydrated(IBlockRehydrationFactory rehydrationFactory) {
			rehydrationFactory.PrepareBlockHeader(this);
		}

		protected virtual void Rehydrate(ChannelsEntries<IDataRehydrator> channelRehydrators, long timestampBaseline, IBlockchainEventsRehydrationFactory rehydrationFactory) {

		}

		protected ITransaction Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels, IBlockchainEventsRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp) {
			DehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

			dehydratedTransaction.Rehydrate(dataChannels, accountId, timestamp);

			return dehydratedTransaction.Rehydrate(rehydrationFactory, accountId, timestamp);
		}

		protected ITransaction Rehydrate(ChannelsEntries<IDataRehydrator> dataChannels, IBlockchainEventsRehydrationFactory rehydrationFactory) {
			return this.Rehydrate(dataChannels, rehydrationFactory, null, null);
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
			jsonDeserializer.SetProperty("FullTimestamp", this.FullTimestamp);
			jsonDeserializer.SetProperty("Lifespan", this.Lifespan);
			jsonDeserializer.SetProperty("SignatureSet", this.SignatureSet);

			jsonDeserializer.SetArray("ConfirmedIndexedTransactions", this.ConfirmedIndexedTransactions);
		}

	#endregion

	}

	[DebuggerDisplay("BlockId: {BlockId}")]
	public abstract class Block : BlockHeader, IBlock {

		public List<ITransaction> ConfirmedTransactions { get; } = new List<ITransaction>();
		public List<RejectedTransaction> RejectedTransactions { get; } = new List<RejectedTransaction>();

		public List<IFinalElectionResults> FinalElectionResults { get; } = new List<IFinalElectionResults>();
		public List<IIntermediaryElectionResults> IntermediaryElectionResults { get; } = new List<IIntermediaryElectionResults>();

		public override HashNodeList GetStructuresArray(SafeArrayHandle previousBlockHash) {
			HashNodeList nodeList = base.GetStructuresArray(previousBlockHash);

			nodeList.Add(this.ConfirmedTransactions.Count);
			nodeList.Add(BlockchainHashingUtils.GenerateTransactionSetNodeList(this.ConfirmedTransactions));

			nodeList.Add(this.RejectedTransactions.Count);
			nodeList.Add(BlockchainHashingUtils.GenerateRejectedTransactionSetNodeList(this.RejectedTransactions));

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
			List<TransactionId> transactions = this.ConfirmedIndexedTransactions.Select(t => t.TransactionId).ToList();
			transactions.AddRange(this.ConfirmedTransactions.Select(t => t.TransactionId));
			transactions.AddRange(this.RejectedTransactions.Select(t => t.TransactionId));

			return transactions;
		}

		public List<(TransactionId TransactionId, int index)> GetAllIndexedTransactions() {
			List<(TransactionId TransactionId, int index)> transactionIndexes = this.ConfirmedIndexedTransactions.Select((t, index) => (t.TransactionId, index)).ToList();
			int count = transactionIndexes.Count;
			transactionIndexes.AddRange(this.ConfirmedTransactions.Select((t, index) => (t.TransactionId, count + index)));
			count = transactionIndexes.Count;
			transactionIndexes.AddRange(this.RejectedTransactions.Select((t, index) => (t.TransactionId, count + index)));

			return transactionIndexes;
		}

		public Dictionary<int, TransactionId> GetAllIndexedTransactionsDictionary() {
			return this.GetAllIndexedTransactions().ToDictionary(t => t.index, t => t.TransactionId);
		}

		public Dictionary<TransactionId, ITransaction> GetAllConfirmedTransactions() {

			Dictionary<TransactionId, ITransaction> results = this.ConfirmedTransactions.ToDictionary(t => t.TransactionId, t => t);

			foreach(IIndexedTransaction t in this.ConfirmedIndexedTransactions) {
				results.Add(t.TransactionId, t);
			}

			return results;
		}

	#region Serialization

		protected override sealed void Rehydrate(ChannelsEntries<IDataRehydrator> channelRehydrators, long timestampBaseline, IBlockchainEventsRehydrationFactory rehydrationFactory) {

			IDataRehydrator rehydratorHeader = channelRehydrators.LowHeaderData;

			bool anyConfirmedTransactions = rehydratorHeader.ReadBool();

			if(anyConfirmedTransactions) {
				List<BlockAccountSerializationSet> confirmedTransactionSet = new List<BlockAccountSerializationSet>();

				RepeatableLongOffsetCalculator timestampsCalculator = new RepeatableLongOffsetCalculator(timestampBaseline);
				List<BlockAccountSerializationSet> confirmedTransactionResultSet = new List<BlockAccountSerializationSet>();

				AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> parameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

				Enums.AccountTypes currentAccountType = Enums.AccountTypes.Unknown;

				parameters.InitializeGroup = (groupIndex, groupCount, accountType) => {
					currentAccountType = accountType;
					timestampsCalculator.Reset(timestampBaseline);
				};

				parameters.RehydrateExtraData = (accountId, offset, index, totalIndex, dh) => {
					timestampsCalculator.Reset(timestampBaseline);
					BlockAccountSerializationSet serializationSet = new BlockAccountSerializationSet(currentAccountType);

					serializationSet.Rehydrate(accountId, rehydratorHeader, channelRehydrators, (dataChannels, accountId2, timestamp) => this.Rehydrate(dataChannels, rehydrationFactory, accountId2, timestamp), timestampsCalculator);

					confirmedTransactionSet.Add(serializationSet);
				};

				AccountIdGroupSerializer.Rehydrate(rehydratorHeader, false, parameters);

				// thats it, add our ordered transactions
				this.ConfirmedTransactions.AddRange(confirmedTransactionSet.SelectMany(ts => ts.Transactions.Select(t => t.transaction)).OrderBy(t => t.TransactionId));
			}

			this.RejectedTransactions.Clear();
			rehydratorHeader.ReadRehydratableArray(this.RejectedTransactions);

			this.RehydrateElectionResults(rehydratorHeader, rehydrationFactory);

			this.RehydrateBody(rehydratorHeader, rehydrationFactory);

			this.RehydrateDataChannels(channelRehydrators, rehydrationFactory);

		}

		protected virtual void RehydrateBody(IDataRehydrator rehydratorHeader, IBlockRehydrationFactory rehydrationFactory) {

		}

		private void RehydrateElectionResults(IDataRehydrator rehydratorHeader, IBlockRehydrationFactory rehydrationFactory) {

			// now build the transaction indexes
			Dictionary<int, TransactionId> transactionIndexesTree = this.GetAllIndexedTransactionsDictionary();

			int count = rehydratorHeader.ReadByte();

			IBlockComponentsRehydrationFactory blockComponentRehydrationFactory = rehydrationFactory.CreateBlockComponentsRehydrationFactory();
			IElectionResultsRehydrator electionResultsRehydrator = blockComponentRehydrationFactory.CreateElectionResultsRehydrator();

			if(count != 0) {
				transactionIndexesTree ??= this.GetAllIndexedTransactionsDictionary();

				for(byte i = 0; i < count; i++) {

					this.IntermediaryElectionResults.Add(electionResultsRehydrator.RehydrateIntermediateResults(rehydratorHeader, transactionIndexesTree));
				}
			}

			count = rehydratorHeader.ReadByte();

			if(count != 0) {
				transactionIndexesTree ??= this.GetAllIndexedTransactionsDictionary();

				for(byte i = 0; i < count; i++) {
					this.FinalElectionResults.Add(electionResultsRehydrator.RehydrateFinalResults(rehydratorHeader, transactionIndexesTree));
				}
			}

		}

		protected virtual void RehydrateDataChannels(ChannelsEntries<IDataRehydrator> dataChannels, IBlockRehydrationFactory rehydrationFactory) {

		}

		protected override void PrepareRehydrated(IBlockRehydrationFactory rehydrationFactory) {
			rehydrationFactory.PrepareBlock(this);
		}

	#endregion

	}
}