using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ITransactionEnvelope : ISignedEnvelope<IDehydratedTransaction, IEnvelopeSignature> {
		byte Expiration { get; }
		DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception);
		void SetExpiration(byte value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception);

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
		///     5 days in half hours
		/// </summary>
		public const int MAXIMUM_EXPIRATION_TIME = 24 * HOURLY_ENTRIES * 5;

		/// <summary>
		///     3 hours
		/// </summary>
		public const int DEFAULT_EXPIRATION_TIME = HOURLY_ENTRIES * 3;

		private byte expiration = DEFAULT_EXPIRATION_TIME;

		/// <summary>
		///     The expiration time in hours
		/// </summary>
		public byte Expiration {
			get => this.expiration;
			private set => this.expiration = this.ClampExpirationTime(value);
		}
		
		public void SetExpiration(byte value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception) {

			DateTime transactionTime = timeService.GetTransactionDateTime(transactionId, chainInception);

			TimeSpan delta = DateTimeEx.CurrentTime - transactionTime;

			int secondsDelta = (int) Math.Round((decimal) delta.TotalMinutes / 30, 0);

			this.Expiration = (byte) Math.Max(Math.Min(secondsDelta + (value != 0 ? value : DEFAULT_EXPIRATION_TIME), byte.MaxValue), MINIMUM_EXPIRATION_TIME);
		}

		public DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception) {
			return timeService.GetTimestampDateTime(this.Contents.Uuid.Timestamp.Value, chainInception).AddMinutes(this.ClampExpirationTime(this.Expiration) * SLICE_MINUTES);
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

		public void SetExpiration(byte value) {

			this.Expiration = value;
		}

		private byte ClampExpirationTime(byte expiration) {
			if(expiration == 0) {
				expiration = DEFAULT_EXPIRATION_TIME;
			}

			return (byte) Math.Max(Math.Min((decimal) expiration, MAXIMUM_EXPIRATION_TIME), MINIMUM_EXPIRATION_TIME);
		}
		

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.Expiration = rehydrator.ReadByte();
		}
		
		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			
			dehydrator.Write(this.Expiration);
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