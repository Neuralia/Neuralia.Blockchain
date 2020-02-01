using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {

	public interface IAccountSnapshotDigestChannelCard : IAccountSnapshot<IAccountAttribute>, IBinarySerializable {
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
		public ImmutableList<IAccountAttribute> AppliedAttributesBase => this.AppliedAttributes.ToImmutableList();
		public List<IAccountAttribute> AppliedAttributes { get; } = new List<IAccountAttribute>();

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			// this one must ALWAYS be first
			this.AccountType = rehydrator.ReadByte();
			
			BlockId inceptionBlockId  = new BlockId();
			inceptionBlockId.Rehydrate(rehydrator);
			this.InceptionBlockId = inceptionBlockId.Value;
			
			this.CorrelationId = rehydrator.ReadNullableLong();
			
			this.TrustLevel = rehydrator.ReadByte();

			this.AppliedAttributes.Clear();
			bool any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				for(int i = 0; i < count; i++) {
					IAccountAttribute attribute = this.CreateAccountFeature();

					AdaptiveLong1_9 certificate = new AdaptiveLong1_9();
					certificate.Rehydrate(rehydrator);
					attribute.CorrelationId = (uint)certificate.Value;
					
					certificate = new AdaptiveLong1_9();
					certificate.Rehydrate(rehydrator);
					attribute.AttributeType = (AccountAttributeType)certificate.Value;
					
					attribute.Start = rehydrator.ReadNullableDateTime();
					attribute.Expiration = rehydrator.ReadNullableDateTime();

					SafeArrayHandle data = rehydrator.ReadNullEmptyArray();

					if(data != null) {
						attribute.Context = data.ToExactByteArray();
					}

					this.AppliedAttributes.Add(attribute);
				}
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			// this one must ALWAYS be first
			dehydrator.Write(this.AccountType);

			BlockId inceptionBlockId = this.InceptionBlockId;
			inceptionBlockId.Dehydrate(dehydrator);

			dehydrator.Write(this.CorrelationId);
			
			dehydrator.Write(this.TrustLevel);

			bool any = this.AppliedAttributes.Any();
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.AppliedAttributes.Count);

				foreach(IAccountAttribute entry in this.AppliedAttributes) {

					AdaptiveLong1_9 certificate = new AdaptiveLong1_9();
					certificate.Value =	entry.CorrelationId;
					certificate.Dehydrate(dehydrator);
					
					certificate.Value =	entry.AttributeType.Value;
					certificate.Dehydrate(dehydrator);
					
					dehydrator.Write(entry.Start);
					dehydrator.Write(entry.Expiration);
					
					dehydrator.Write(entry.Context);
				}
			}
		}

		public void ConvertToSnapshotEntry(IAccountSnapshot other, ICardUtils cardUtils) {
			cardUtils.Copy(this, other);
		}

		public void ClearCollection() {
			this.AppliedAttributes.Clear();
		}

		public void CreateNewCollectionEntry(out IAccountAttribute result) {
			TypedCollectionExposureUtil<IAccountAttribute>.CreateNewCollectionEntry(this.AppliedAttributes, out result);
		}

		public void AddCollectionEntry(IAccountAttribute entry) {
			TypedCollectionExposureUtil<IAccountAttribute>.AddCollectionEntry(entry, this.AppliedAttributes);
		}

		public void RemoveCollectionEntry(Func<IAccountAttribute, bool> predicate) {
			TypedCollectionExposureUtil<IAccountAttribute>.RemoveCollectionEntry(predicate, this.AppliedAttributes);
		}

		public IAccountAttribute GetCollectionEntry(Func<IAccountAttribute, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountAttribute>.GetCollectionEntry(predicate, this.AppliedAttributes);
		}

		public List<IAccountAttribute> GetCollectionEntries(Func<IAccountAttribute, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountAttribute>.GetCollectionEntries(predicate, this.AppliedAttributes);
		}

		public ImmutableList<IAccountAttribute> CollectionCopy => TypedCollectionExposureUtil<IAccountAttribute>.GetCollection(this.AppliedAttributes);

		protected abstract IAccountAttribute CreateAccountFeature();

		public static Enums.AccountTypes GetAccountType(IDataRehydrator rehydrator) {
			rehydrator.SnapshotPosition();
			byte accountType = rehydrator.ReadByte();
			rehydrator.Rewind2Snapshot();

			return (Enums.AccountTypes) accountType;
		}

		protected abstract IAccountSnapshotDigestChannelCard CreateCard();
	}
}