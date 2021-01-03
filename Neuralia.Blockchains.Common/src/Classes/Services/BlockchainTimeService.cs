using System;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Common.Classes.Services {
	public interface IBlockchainTimeService : ITimeService {
		DateTime GetTransactionDateTime(TransactionId transactionId, DateTime chainInception);
		DateTime GetTransactionDateTime(TransactionTimestamp timestamp, DateTime chainInception);
		TransactionTimestamp GetChainDateTimeOffsetTimestamp(DateTime chainInception);
		TimeSpan GetTransactionTimeDifference(TransactionTimestamp timestamp, DateTime time, DateTime chainInception);
		
		DateTime GetTransactionExpiration(ITransactionEnvelope transactionEnvelope, DateTime chainInception);
		//DateTime GetTHSExtendedExpiration(DateTime expiration, IEnvelope thsEnvelope);
		//TimeSpan GetTHSExtendedExpirationSpan(IEnvelope envelope);
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

		/// <summary>
		/// this method will return the properly adjusted expected expiration time for the transaction
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public DateTime GetTransactionExpiration(ITransactionEnvelope transactionEnvelope, DateTime chainInception) {
			var expiration = transactionEnvelope.GetExpirationTime(this, chainInception);

			expiration = this.GetTHSExtendedExpiration(expiration, transactionEnvelope);
			
#if MAINNET_LAUNCH_CODE

			// special code to accomodate mainnet launch
			if(expiration.ToUniversalTime() < GlobalsService.MainnetLauchTime + TimeSpan.FromDays(2)) {
				expiration = GlobalsService.MainnetLauchTime + TimeSpan.FromDays(2);
			}	
#else
we have to remove this code!!
#endif
			return expiration;
		}

		public TimeSpan GetTHSExtendedExpirationSpan(IEnvelope envelope) {
			TimeSpan extension = TimeSpan.Zero;
			
			if(envelope is ITHSEnvelope thsEnvelope) {
				// we dont need this anymore, expiration already has THS factored in

				// TimeSpan estimatedRoundTime = TimeSpan.Zero;
				//
				// if(thsEnvelope is IPresentationTransactionEnvelope presentationTransactionEnvelope) {
				//
				// 	if(presentationTransactionEnvelope.Contents.RehydratedEvent is IStandardPresentationTransaction standardPresentationTransaction) {
				// 		if(standardPresentationTransaction.IsServer) {
				// 			estimatedRoundTime = THSRulesSet.ServerPresentationDefaultRulesSetDescriptor.EstimatedHigherRoundTime;
				// 		} else {
				// 			estimatedRoundTime = THSRulesSet.PresentationDefaultRulesSetDescriptor.EstimatedHigherRoundTime;
				// 		}
				// 	}
				// } else if(thsEnvelope is IInitiationAppointmentMessageEnvelope) {
				// 	estimatedRoundTime = THSRulesSet.InitiationAppointmentDefaultRulesSetDescriptor.EstimatedHigherRoundTime;
				// }
				//
				// // if it is a ths transaction, then we add the estimated time it took as a bonus
				// if(thsEnvelope.THSEnvelopeSignatureBase != null && thsEnvelope.THSEnvelopeSignatureBase.Solution.IsValid) {
				// 	var totalNonce = thsEnvelope.THSEnvelopeSignatureBase.Solution.Solutions.Sum(s => s.nonce);
				//
				// 	// this is our best estimate to how long the THS took.
				// 	extension = estimatedRoundTime * totalNonce;
				// }
				
				// give a little extension
				extension = TimeSpan.FromHours(3);
			}
		
			return extension;
		}
		
		public DateTime GetTHSExtendedExpiration(DateTime expiration, IEnvelope envelope) {
			// ok, for presentations, we add the time of the THS
			return expiration + GetTHSExtendedExpirationSpan(envelope);
		}
	}
}