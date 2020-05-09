using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization {
	public class BlockAccountSerializationSet {

		private readonly Enums.AccountTypes accountType;

		public BlockAccountSerializationSet(Enums.AccountTypes accountType) {
			this.accountType = accountType;
		}

		public List<(long TimestampOffset, ITransaction transaction)> Transactions { get; } = new List<(long TimestampOffset, ITransaction transaction)>();

		public void Rehydrate(AccountId accountId, IDataRehydrator rehydrator, ChannelsEntries<IDataRehydrator> channelRehydrators, Func<ChannelsEntries<IDataRehydrator>, AccountId, TransactionTimestamp, ITransaction> rehydrationCallback, RepeatableOffsetCalculator timestampsCalculator) {

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Rehydrate(rehydrator);
			uint transactionCount = (uint) adaptiveLong.Value;

			for(int i = 0; i < transactionCount; i++) {

				(long TimestampOffset, ITransaction transaction) transactionSet = default;

				adaptiveLong.Rehydrate(rehydrator);
				transactionSet.TimestampOffset = adaptiveLong.Value;

				// lets rebuild the values from the offsets

				TransactionTimestamp timestamp = new TransactionTimestamp(timestampsCalculator.RebuildValue(transactionSet.TimestampOffset));

				transactionSet.transaction = rehydrationCallback(channelRehydrators, accountId, timestamp);

				this.Transactions.Add(transactionSet);
				timestampsCalculator.AddLastOffset();
			}
		}

		public void Dehydrate(ChannelsEntries<IDataDehydrator> channelDehydrators, BlockChannelUtils.BlockChannelTypes activeChannels) {

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			adaptiveLong.Value = this.Transactions.Count;
			adaptiveLong.Dehydrate(channelDehydrators.LowHeaderData);

			foreach((long TimestampOffset, ITransaction transaction) transactionSet in this.Transactions) {

				adaptiveLong.Value = transactionSet.TimestampOffset;
				adaptiveLong.Dehydrate(channelDehydrators.LowHeaderData);

				IDehydratedTransaction dehydratedTransaction = transactionSet.transaction.DehydrateForBlock(activeChannels);

				dehydratedTransaction.Dehydrate(channelDehydrators);
			}
		}
	}
}