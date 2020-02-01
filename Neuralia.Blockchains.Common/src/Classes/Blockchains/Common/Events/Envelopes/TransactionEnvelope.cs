using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ITransactionEnvelope : ISignedEnvelope<IDehydratedTransaction, IEnvelopeSignature> {
		DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception);
		void SetExpiration( byte value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception);
		
		HashNodeList GetTransactionHashingStructuresArray();
		byte Expiration { get; }
		byte Options { get; }
		bool IsPresentation { get; set; }
	}

	public abstract class TransactionEnvelope : SignedEnvelope<IDehydratedTransaction, IEnvelopeSignature>, ITransactionEnvelope {

		public enum EnvelopTransactionOptionTypes:byte {
			Presentation = 1
		}

		public const int SLICE_MINUTES = 30; // 30 minute
		
		public const int HOURLY_ENTRIES = 60/SLICE_MINUTES; // 30 minute in 60 minute
		
		/// <summary>
		///     30 minutes
		/// </summary>
		public const int MINIMUM_EXPIRATION_TIME = 1;

		/// <summary>
		///    5 days in half hours
		/// </summary>
		public const int MAXIMUM_EXPIRATION_TIME = 24 * HOURLY_ENTRIES * 5;
		
		/// <summary>
		/// 3 hours
		/// </summary>
		public const int DEFAULT_EXPIRATION_TIME = HOURLY_ENTRIES*3;

		private byte expiration = DEFAULT_EXPIRATION_TIME;

		/// <summary>
		///     The expiration time in hours
		/// </summary>
		public byte Expiration {
			get => this.expiration;
			private set => this.expiration = this.ClampExpirationTime(value);
		}

		public byte Options { get; set; }

		public bool IsPresentation {
			get {
				var option = new ByteExclusiveOption<EnvelopTransactionOptionTypes>(this.Options);
				return option.HasOption(EnvelopTransactionOptionTypes.Presentation);
			}
			set {
				
				var option = new ByteExclusiveOption<EnvelopTransactionOptionTypes>(this.Options);
				option.SetOptionValue(EnvelopTransactionOptionTypes.Presentation, value);
				this.Options = option;
			}
		}

		public void SetExpiration( byte value, TransactionId transactionId, IBlockchainTimeService timeService, DateTime chainInception) {

			DateTime transactionTime = timeService.GetTransactionDateTime(transactionId, chainInception);

			TimeSpan delta = DateTime.UtcNow - transactionTime;

			int secondsDelta = (int)Math.Round((decimal) delta.TotalMinutes / 30, 0);

			this.Expiration = (byte)Math.Max(Math.Min(secondsDelta + (value!=0?value:DEFAULT_EXPIRATION_TIME), byte.MaxValue), MINIMUM_EXPIRATION_TIME);
		}
		
		public void SetExpiration( byte value) {

			this.Expiration = value;
		}
		
		public DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception) {
			return timeService.GetTimestampDateTime(this.Contents.Uuid.Timestamp.Value, chainInception).AddMinutes(this.ClampExpirationTime(this.Expiration)*SLICE_MINUTES);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.GetTransactionHashingStructuresArray());

			return nodeList;
		}

		private byte ClampExpirationTime(byte expiration) {
			if(expiration == 0) {
				expiration = DEFAULT_EXPIRATION_TIME;
			}
			return (byte) Math.Max(Math.Min((decimal) expiration, MAXIMUM_EXPIRATION_TIME), MINIMUM_EXPIRATION_TIME);
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Expiration);
			dehydrator.Write(this.Options);
			
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Expiration = rehydrator.ReadByte();
			this.Options = rehydrator.ReadByte();
		}

		protected override IDehydratedTransaction RehydrateContents(IDataRehydrator rh) {

			IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
			dehydratedTransaction.Rehydrate(rh);

			return dehydratedTransaction;
		}

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.Transaction, 1, 0);
		}

		/// <summary>
		/// Extra fields that will be hashed with the transaction
		/// </summary>
		/// <returns></returns>
		public virtual HashNodeList GetTransactionHashingStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Expiration);
			hashNodeList.Add(this.Options);
			
			return hashNodeList;
		}
	}
}