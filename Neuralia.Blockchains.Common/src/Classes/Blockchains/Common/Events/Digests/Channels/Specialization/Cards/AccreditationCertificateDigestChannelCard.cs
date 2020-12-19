using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization.Cards {
	public interface IAccreditationCertificateDigestChannelCard : IAccreditationCertificateSnapshot<IAccreditationCertificateSnapshotAccount>, IChannelBandSqliteProviderEntry<int>, IBinarySerializable {
		AccountId AssignedAccountFull { get; set; }
		void ConvertToSnapshotEntry(IAccreditationCertificateSnapshot other, ICardUtils cardUtils);
	}

	public abstract class AccreditationCertificateDigestChannelCard : IAccreditationCertificateDigestChannelCard {

		public ushort Major {
			get => this.CertificateVersion.Major.Value;
			set => this.CertificateVersion = new ComponentVersion(value, this.CertificateVersion.Minor);
		}

		public ushort Minor {
			get => this.CertificateVersion.Minor;
			set => this.CertificateVersion = new ComponentVersion(this.CertificateVersion.Major.Value, value);
		}

		public int Id {
			get => this.CertificateId;
			set => this.CertificateId = value;
		}

		public ComponentVersion CertificateVersion { get; set; } = new ComponentVersion();
		public int CertificateId { get; set; }
		public int CertificateType { get; set; }
		public Enums.CertificateApplicationTypes ApplicationType { get; set; } = Enums.CertificateApplicationTypes.Abstract;
		public Enums.CertificateStates CertificateState { get; set; } = Enums.CertificateStates.Revoked;

		public DateTime EmissionDate { get; set; }
		public DateTime ValidUntil { get; set; }

		public long AssignedAccount {
			get => this.AssignedAccountFull.ToLongRepresentation();
			set => this.AssignedAccountFull = value.ToAccountId();
		}

		public AccountId AssignedAccountFull { get; set; } = new AccountId();

		public string Application { get; set; }
		public string Organisation { get; set; }
		public string Url { get; set; }
		public Enums.CertificateAccountPermissionTypes CertificateAccountPermissionType { get; set; } = Enums.CertificateAccountPermissionTypes.None;
		public int PermittedAccountCount { get; set; }
		public ImmutableList<IAccreditationCertificateSnapshotAccount> PermittedAccountsBase => this.PermittedAccounts.ToImmutableList();
		public List<IAccreditationCertificateSnapshotAccount> PermittedAccounts { get; } = new List<IAccreditationCertificateSnapshotAccount>();

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.CertificateState = rehydrator.ReadByteEnum<Enums.CertificateStates>();
			this.ApplicationType = rehydrator.ReadIntEnum<Enums.CertificateApplicationTypes>();

			this.CertificateType = rehydrator.ReadUShort();

			this.CertificateId = rehydrator.ReadInt();
			this.CertificateVersion.Rehydrate(rehydrator);

			this.EmissionDate = rehydrator.ReadDateTime();

			this.AssignedAccountFull.Rehydrate(rehydrator);

			this.Application = rehydrator.ReadString();
			this.Organisation = rehydrator.ReadString();
			this.Url = rehydrator.ReadString();

			if(this.CertificateState != Enums.CertificateStates.Revoked) {

				this.ValidUntil = rehydrator.ReadDateTime();
				this.CertificateAccountPermissionType = rehydrator.ReadIntEnum<Enums.CertificateAccountPermissionTypes>();

				this.PermittedAccountCount = rehydrator.ReadUShort();

				this.PermittedAccounts.Clear();

				foreach(AccountId entry in AccountIdGroupSerializer.Rehydrate(rehydrator, true)) {
					IAccreditationCertificateSnapshotAccount account = this.CreateAccreditationCertificateSnapshotAccount();
					account.AccountId = entry.ToLongRepresentation();

					this.PermittedAccounts.Add(account);
				}
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write((byte) this.CertificateState);
			dehydrator.Write((int) this.ApplicationType);
			dehydrator.Write(this.CertificateType);

			dehydrator.Write(this.CertificateId);
			this.CertificateVersion.Dehydrate(dehydrator);

			dehydrator.Write(this.EmissionDate);

			this.AssignedAccountFull.Dehydrate(dehydrator);

			dehydrator.Write(this.Application);
			dehydrator.Write(this.Organisation);
			dehydrator.Write(this.Url);

			if(this.CertificateState != Enums.CertificateStates.Revoked) {

				dehydrator.Write(this.ValidUntil);
				dehydrator.Write((int) this.CertificateAccountPermissionType);

				dehydrator.Write((ushort) this.PermittedAccounts.Count);

				AccountIdGroupSerializer.Dehydrate(this.PermittedAccounts.Select(e => e.AccountId.ToAccountId()).ToList(), dehydrator, true);
			}
		}

		public virtual void ConvertToSnapshotEntry(IAccreditationCertificateSnapshot other, ICardUtils cardUtils) {

			cardUtils.Copy(this, other);
		}

		public void ClearCollection() {
			this.PermittedAccounts.Clear();
		}

		public void CreateNewCollectionEntry(out IAccreditationCertificateSnapshotAccount result) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.CreateNewCollectionEntry(this.PermittedAccounts, out result);
		}

		public void AddCollectionEntry(IAccreditationCertificateSnapshotAccount entry) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.AddCollectionEntry(entry, this.PermittedAccounts);
		}

		public void RemoveCollectionEntry(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.RemoveCollectionEntry(predicate, this.PermittedAccounts);
		}

		public IAccreditationCertificateSnapshotAccount GetCollectionEntry(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollectionEntry(predicate, this.PermittedAccounts);
		}

		public List<IAccreditationCertificateSnapshotAccount> GetCollectionEntries(Func<IAccreditationCertificateSnapshotAccount, bool> predicate) {
			return TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollectionEntries(predicate, this.PermittedAccounts);
		}

		public ImmutableList<IAccreditationCertificateSnapshotAccount> CollectionCopy => TypedCollectionExposureUtil<IAccreditationCertificateSnapshotAccount>.GetCollection(this.PermittedAccounts);

		protected abstract IAccreditationCertificateSnapshotAccount CreateAccreditationCertificateSnapshotAccount();

		protected abstract IAccreditationCertificateDigestChannelCard CreateCard();
	}
}