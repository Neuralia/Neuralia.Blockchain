using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {
	public interface IChainAccreditationCertificateTransaction : IModerationTransaction {

		ChainAccreditationCertificateTransaction.CertificateOperationTypes CertificateOperation { get; set; }
		Enums.CertificateApplicationTypes ApplicationType { get; set; }

		AdaptiveLong1_9 CertificateId { get; set; }
		ComponentVersion CertificateVersion { get; set; }

		DateTime EmissionDate { get; set; }
		DateTime ValidUntil { get; set; }

		AccountId AssignedAccount { get; set; }

		string Application { get; set; }
		string Organisation { get; set; }
		string Url { get; set; }

		Enums.CertificateAccountPermissionTypes AccountPermissionType { get; set; }
		List<AccountId> PermittedAccounts { get; }

		AccreditationCertificateType CertificateType { get; set; }
	}

	public class ChainAccreditationCertificateTransaction : ModerationTransaction, IChainAccreditationCertificateTransaction {

		public enum CertificateOperationTypes : byte {
			Create = 1,
			Renew = 2,
			Revoke = 3
		}

		public CertificateOperationTypes CertificateOperation { get; set; }
		public Enums.CertificateApplicationTypes ApplicationType { get; set; }
		public AdaptiveLong1_9 CertificateId { get; set; } = new AdaptiveLong1_9();
		public ComponentVersion CertificateVersion { get; set; } = new ComponentVersion();
		public DateTime EmissionDate { get; set; }
		public DateTime ValidUntil { get; set; }
		public AccountId AssignedAccount { get; set; } = new AccountId();
		public string Application { get; set; }
		public string Organisation { get; set; }
		public string Url { get; set; }
		public Enums.CertificateAccountPermissionTypes AccountPermissionType { get; set; }
		public List<AccountId> PermittedAccounts { get; } = new List<AccountId>();
		public AccreditationCertificateType CertificateType { get; set; }

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.CertificateId);
			nodeList.Add(this.CertificateVersion);
			nodeList.Add(this.AssignedAccount);

			nodeList.Add(this.Application);
			nodeList.Add(this.Organisation);
			nodeList.Add(this.Url);

			nodeList.Add((int) this.CertificateOperation);
			nodeList.Add((int) this.ApplicationType);

			nodeList.Add(this.EmissionDate);

			nodeList.Add(this.CertificateType.Value);

			if(this.CertificateOperation != CertificateOperationTypes.Revoke) {

				nodeList.Add(this.ValidUntil);
				nodeList.Add((int) this.AccountPermissionType);
				nodeList.Add(this.PermittedAccounts.Count);
				nodeList.Add(this.PermittedAccounts);
			}

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("CertificateOperation", (int) this.CertificateOperation);
			jsonDeserializer.SetProperty("CertificateType", this.CertificateType.Value);
			jsonDeserializer.SetProperty("CertificateId", this.CertificateId);
			jsonDeserializer.SetProperty("ApplicationType", (int) this.ApplicationType);

			//
			jsonDeserializer.SetProperty("EmissionDate", this.EmissionDate);

			//
			jsonDeserializer.SetProperty("Account", this.AssignedAccount);

			//
			jsonDeserializer.SetProperty("Application", this.Application);
			jsonDeserializer.SetProperty("Organisation", this.Organisation);
			jsonDeserializer.SetProperty("Url", this.Url);

			if(this.CertificateOperation != CertificateOperationTypes.Revoke) {
				jsonDeserializer.SetProperty("ValidUntil", this.ValidUntil);
				jsonDeserializer.SetProperty("AccountPermissionType", (int) this.AccountPermissionType);

				jsonDeserializer.SetArray("PermittedAccounts", this.PermittedAccounts.OrderBy(a => a));
			}
		}

		public override ImmutableList<AccountId> TargetAccounts => new[] {this.AssignedAccount}.ToImmutableList();

		protected override void RehydrateContents(ChannelsEntries<IDataRehydrator> dataChannels, ITransactionRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(dataChannels, rehydrationFactory);

			this.CertificateOperation = (CertificateOperationTypes) dataChannels.ContentsData.ReadByte();
			this.ApplicationType = (Enums.CertificateApplicationTypes) dataChannels.ContentsData.ReadInt();

			this.CertificateType = dataChannels.ContentsData.ReadUShort();

			this.CertificateId.Rehydrate(dataChannels.ContentsData);
			this.CertificateVersion.Rehydrate(dataChannels.ContentsData);

			this.EmissionDate = dataChannels.ContentsData.ReadDateTime();

			this.AssignedAccount.Rehydrate(dataChannels.ContentsData);

			this.Application = dataChannels.ContentsData.ReadString();
			this.Organisation = dataChannels.ContentsData.ReadString();
			this.Url = dataChannels.ContentsData.ReadString();

			if(this.CertificateOperation != CertificateOperationTypes.Revoke) {

				this.ValidUntil = dataChannels.ContentsData.ReadDateTime();
				this.AccountPermissionType = (Enums.CertificateAccountPermissionTypes) dataChannels.ContentsData.ReadInt();

				this.PermittedAccounts.Clear();

				this.PermittedAccounts.AddRange(AccountIdGroupSerializer.Rehydrate(dataChannels.ContentsData, true));
			}
		}

		protected override void DehydrateContents(ChannelsEntries<IDataDehydrator> dataChannels) {
			base.DehydrateContents(dataChannels);

			dataChannels.ContentsData.Write((byte) this.CertificateOperation);
			dataChannels.ContentsData.Write((int) this.ApplicationType);
			dataChannels.ContentsData.Write(this.CertificateType.Value);
			this.CertificateId.Dehydrate(dataChannels.ContentsData);
			this.CertificateVersion.Dehydrate(dataChannels.ContentsData);

			dataChannels.ContentsData.Write(this.EmissionDate);

			this.AssignedAccount.Dehydrate(dataChannels.ContentsData);

			dataChannels.ContentsData.Write(this.Application);
			dataChannels.ContentsData.Write(this.Organisation);
			dataChannels.ContentsData.Write(this.Url);

			if(this.CertificateOperation != CertificateOperationTypes.Revoke) {

				dataChannels.ContentsData.Write(this.ValidUntil);
				dataChannels.ContentsData.Write((int) this.AccountPermissionType);

				AccountIdGroupSerializer.Dehydrate(this.PermittedAccounts, dataChannels.ContentsData, true);
			}
		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.ACCREDITATION_CERTIFICATE, 1, 0);
		}
	}
}