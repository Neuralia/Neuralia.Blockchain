using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Common.Classes.Services {
	public interface IBlockchainTimeService : ITimeService {
		DateTime GetTransactionDateTime(TransactionId transactionId, DateTime chainInception);
		DateTime GetTransactionDateTime(TransactionTimestamp timestamp, DateTime chainInception);
		TransactionTimestamp GetChainDateTimeOffsetTimestamp(DateTime chainInception);
		TimeSpan GetTransactionTimeDifference(TransactionTimestamp timestamp, DateTime time, DateTime chainInception);
	}

	public class BlockchainTimeService : TimeService, IBlockchainTimeService {

		/// <summary>
		///     Convert a timestamp offset ince inception to a complete datetime
		/// </summary>
		/// <param name="timestamp"></param>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public DateTime GetTransactionDateTime(TransactionId transactionId, DateTime chainInception) {
			return this.GetTransactionDateTime(transactionId.Timestamp.Value, chainInception);
		}

		/// <summary>
		///     Convert a timestamp offset ince inception to a complete datetime
		/// </summary>
		/// <param name="timestamp"></param>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public DateTime GetTransactionDateTime(TransactionTimestamp timestamp, DateTime chainInception) {
			return this.GetTimestampDateTime(timestamp.Value, chainInception);
		}

		public TransactionTimestamp GetChainDateTimeOffsetTimestamp(DateTime chainInception) {
			long entry = this.GetChainDateTimeOffset(chainInception);

			return new TransactionTimestamp(entry);
		}

		public TimeSpan GetTransactionTimeDifference(TransactionTimestamp timestamp, DateTime time, DateTime chainInception) {
			return this.GetTimeDifference(timestamp.Value, time, chainInception);
		}
	}
}