using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ITransactionEnvelope : ISignedEnvelope<IDehydratedTransaction, IEnvelopeSignature> {
		List<int> AccreditationCertificates { get; }
		DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception);
	}

	public abstract class TransactionEnvelope : SignedEnvelope<IDehydratedTransaction, IEnvelopeSignature>, ITransactionEnvelope {

		/// <summary>
		///     in hours
		/// </summary>
		public const int MINIMUM_EXPIRATION_TIME = 1;

		/// <summary>
		///     in hours
		/// </summary>
		public const int MAXIMUM_EXPIRATION_TIME = 24;

		private byte expiration = 3;

		/// <summary>
		///     The expiration time in hours
		/// </summary>
		public byte Expiration {
			get => this.expiration;
			set => this.expiration = this.ClampExpirationTime(value);
		}

		public List<int> AccreditationCertificates { get; } = new List<int>();

		public DateTime GetExpirationTime(ITimeService timeService, DateTime chainInception) {
			return timeService.GetTimestampDateTime(this.Contents.Uuid.Timestamp.Value, chainInception).AddHours(this.ClampExpirationTime(this.Expiration));
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Expiration);
			nodeList.Add(this.AccreditationCertificates.Count);

			foreach(int entry in this.AccreditationCertificates) {

				nodeList.Add(entry);
			}

			return nodeList;
		}

		private byte ClampExpirationTime(byte expiration) {
			return (byte) Math.Max(Math.Min((decimal) expiration, MAXIMUM_EXPIRATION_TIME), MINIMUM_EXPIRATION_TIME);
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Expiration);

			bool any = this.AccreditationCertificates.Any();
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.AccreditationCertificates.Count);

				foreach(int entry in this.AccreditationCertificates) {

					dehydrator.Write(entry);
				}
			}
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Expiration = rehydrator.ReadByte();
			this.AccreditationCertificates.Clear();
			bool any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				for(int i = 0; i < count; i++) {
					this.AccreditationCertificates.Add(rehydrator.ReadInt());
				}
			}
		}

		protected override IDehydratedTransaction RehydrateContents(IDataRehydrator rh) {

			IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();
			dehydratedTransaction.Rehydrate(rh);

			return dehydratedTransaction;
		}

		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.Transaction, 1, 0);
		}
	}
}