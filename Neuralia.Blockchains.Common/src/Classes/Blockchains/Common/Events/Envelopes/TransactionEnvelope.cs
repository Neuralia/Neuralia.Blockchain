using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Utilities;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ITransactionEnvelope : ISignedEnvelope<IDehydratedTransaction, IEnvelopeSignature> {
		ushort Expiration { get; }
		DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception);
		TimeSpan GetExpirationSpan();
		void SetExpiration(ushort value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception);

		HashNodeList GetTransactionHashingStructuresArray(TransactionEnvelope.TransactionHashingTypes type = TransactionEnvelope.TransactionHashingTypes.Full);
		HashNodeList GetFixedStructuresArray();
	}
	

	public abstract class TransactionEnvelope : SignedEnvelope<IDehydratedTransaction, IEnvelopeSignature>, ITransactionEnvelope {

		[Flags]
		public enum TransactionHashingTypes {
			None = 0,
			Time = 1,
			NoTime = None,
			Full = Time 
		}

		public const int SLICE_MINUTES = 30; // 30 minute

		public const int HOURLY_ENTRIES = 60 / SLICE_MINUTES; // 30 minute in 60 minute

		/// <summary>
		///     30 minutes
		/// </summary>
		public const int MINIMUM_EXPIRATION_TIME = 1;

		/// <summary>
		///     two weeks in half hours! should be more than plenty
		/// </summary>
		public const int MAXIMUM_EXPIRATION_TIME = 24 * HOURLY_ENTRIES * 15;

		/// <summary>
		///     6 hours
		/// </summary>
		public const int DEFAULT_EXPIRATION_TIME = HOURLY_ENTRIES * 6;

		private ushort expiration = DEFAULT_EXPIRATION_TIME;

		/// <summary>
		///     The expiration time in half hours
		/// </summary>
		public ushort Expiration {
			get => this.expiration;
			private set => this.expiration = this.ClampExpirationTime(value);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="value">expiration time in half hours</param>
		/// <param name="transactionId"></param>
		/// <param name="timeService"></param>
		/// <param name="chainInception"></param>
		public void SetExpiration(ushort value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception) {

			DateTime transactionTime = timeService.GetTransactionDateTime(transactionId, chainInception);

			TimeSpan delta = DateTimeEx.CurrentTime - transactionTime;

			int halfHourDelta = (int) Math.Round((decimal) delta.TotalMinutes / SLICE_MINUTES, 0);

			this.Expiration = (ushort) Math.Max(Math.Min(halfHourDelta + (value != 0 ? value : DEFAULT_EXPIRATION_TIME), ushort.MaxValue), MINIMUM_EXPIRATION_TIME);
		}

		public DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception) {
			return timeService.GetTimestampDateTime(this.Contents.Uuid.Timestamp.Value, chainInception) + this.GetExpirationSpan();
		}
		
		public TimeSpan GetExpirationSpan() {
			return TimeSpan.FromMinutes(this.ClampExpirationTime(this.Expiration) * SLICE_MINUTES);
		}
		

		public override string GetId() {
			return this.Contents.Uuid.ToString();
		}
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();
			
			return nodeList;
		}

		/// <summary>
		/// get only the fixed elements of the transaction
		/// </summary>
		/// <returns></returns>
		public virtual HashNodeList GetFixedStructuresArray() {
			try {
				this.skipRehydratedEventStructuresArray = true;
				HashNodeList nodeList = this.GetStructuresArray();

				nodeList.Add(this.Contents.RehydratedEvent.GetStructuresArray(Enums.MutableStructureTypes.Fixed));

				return nodeList;
			} finally {
				this.skipRehydratedEventStructuresArray = false;
			}
		}

		private bool skipRehydratedEventStructuresArray;
		
		protected override HashNodeList GetContentStructuresArray() {
			// do nothing here, we add it explicitly
			if(this.skipRehydratedEventStructuresArray) {
				return null;
			}
			return this.Contents.RehydratedEvent.GetStructuresArray();
		}

		/// <summary>
		///     Extra conditional fields that will be hashed with the transaction
		/// </summary>
		/// <returns></returns>
		public virtual HashNodeList GetTransactionHashingStructuresArray(TransactionHashingTypes type = TransactionHashingTypes.Full) {
			HashNodeList hashNodeList = new HashNodeList();

			if(type.HasFlag(TransactionHashingTypes.Time)) {
				hashNodeList.Add(this.Expiration);
			}

			return hashNodeList;
		}

		public void SetExpiration(ushort value) {

			this.Expiration = value;
		}

		private ushort ClampExpirationTime(ushort expiration) {
			if(expiration == 0) {
				expiration = DEFAULT_EXPIRATION_TIME;
			}

			return (ushort) Math.Max(Math.Min((decimal) expiration, MAXIMUM_EXPIRATION_TIME), MINIMUM_EXPIRATION_TIME);
		}
		

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			SimpleOverflowShort tool = new SimpleOverflowShort();
			tool.Rehydrate(rehydrator);
			this.Expiration = tool.Value;
		}
		
		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			SimpleOverflowShort tool = new SimpleOverflowShort(this.Expiration);
			tool.Dehydrate(dehydrator);
		}

		protected override IDehydratedTransaction RehydrateContents(IDataRehydrator rh) {

			IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
			dehydratedTransaction.Rehydrate(rh);

			return dehydratedTransaction;
		}
		
		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.SignedTransaction, 1, 0);
		}
	}
}