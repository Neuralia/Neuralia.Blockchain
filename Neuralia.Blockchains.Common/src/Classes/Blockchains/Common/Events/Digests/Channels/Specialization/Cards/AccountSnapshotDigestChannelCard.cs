using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IAccountSnapshotDigestChannelCard : IAccountSnapshot<IAccountFeature>, IBinarySerializable {
		AccountId AccountIdFull { get; set; }
		void ConvertToSnapshotEntry(IAccountSnapshot other, ICardUtils cardUtils);
	}

	public abstract class AccountSnapshotDigestChannelCard : IAccountSnapshotDigestChannelCard {
		public byte AccountType { get; set; }

		public long AccountId {
			get => this.AccountIdFull.ToLongRepresentation();
			set => this.AccountIdFull = value.ToAccountId();
		}

		public AccountId AccountIdFull { get; set; } = new AccountId();

		public long InceptionBlockId { get; set; }
		public byte TrustLevel { get; set; }
		public long? CorrelationId { get; set; }
		public List<IAccountFeature> AppliedFeatures { get; } = new List<IAccountFeature>();

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			// this one must ALWAYS be first
			this.AccountType = rehydrator.ReadByte();

			this.AccountIdFull.Rehydrate(rehydrator);
			this.InceptionBlockId = rehydrator.ReadLong();
			this.CorrelationId = rehydrator.ReadNullableLong();
			
			this.TrustLevel = rehydrator.ReadByte();

			this.AppliedFeatures.Clear();
			bool any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				for(int i = 0; i < count; i++) {
					IAccountFeature attribute = this.CreateAccountFeature();

					attribute.CertificateId = rehydrator.ReadInt();

					IByteArray data = rehydrator.ReadNullEmptyArray();

					if(data != null) {
						attribute.Data = data.ToExactByteArray();
					}

					this.AppliedFeatures.Add(attribute);
				}
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			// this one must ALWAYS be first
			dehydrator.Write(this.AccountType);

			this.AccountIdFull.Dehydrate(dehydrator);
			dehydrator.Write(this.InceptionBlockId);
			dehydrator.Write(this.CorrelationId);
			
			dehydrator.Write(this.TrustLevel);
			
			dehydrator.Write((byte) this.AppliedFeatures.Count);

			bool any = this.AppliedFeatures.Any();
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.AppliedFeatures.Count);

				foreach(IAccountFeature entry in this.AppliedFeatures) {

					dehydrator.Write(entry.CertificateId);
					dehydrator.Write(entry.Data);
				}
			}
		}

		public void ConvertToSnapshotEntry(IAccountSnapshot other, ICardUtils cardUtils) {
			cardUtils.Copy(this, other);
		}

		public void ClearCollection() {
			this.AppliedFeatures.Clear();
		}

		public void CreateNewCollectionEntry(out IAccountFeature result) {
			TypedCollectionExposureUtil<IAccountFeature>.CreateNewCollectionEntry(this.AppliedFeatures, out result);
		}

		public void AddCollectionEntry(IAccountFeature entry) {
			TypedCollectionExposureUtil<IAccountFeature>.AddCollectionEntry(entry, this.AppliedFeatures);
		}

		public void RemoveCollectionEntry(Func<IAccountFeature, bool> predicate) {
			TypedCollectionExposureUtil<IAccountFeature>.RemoveCollectionEntry(predicate, this.AppliedFeatures);
		}

		public IAccountFeature GetCollectionEntry(Func<IAccountFeature, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountFeature>.GetCollectionEntry(predicate, this.AppliedFeatures);
		}

		public List<IAccountFeature> GetCollectionEntries(Func<IAccountFeature, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountFeature>.GetCollectionEntries(predicate, this.AppliedFeatures);
		}

		public ImmutableList<IAccountFeature> CollectionCopy => TypedCollectionExposureUtil<IAccountFeature>.GetCollection(this.AppliedFeatures);

		protected abstract IAccountFeature CreateAccountFeature();

		public static Enums.AccountTypes GetAccountType(IDataRehydrator rehydrator) {
			rehydrator.SnapshotPosition();
			byte accountType = rehydrator.ReadByte();
			rehydrator.Rewind2Snapshot();

			return (Enums.AccountTypes) accountType;
		}

		protected abstract IAccountSnapshotDigestChannelCard CreateCard();
	}
}