using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.AccreditationCertificates;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections {

	public interface IElectionsRegistrationMessage : IBlockchainMessage {
		SafeArrayHandle EncryptedMessage { get; }
		AccountId AccountId { get; set; }
		AccountId DelegateAccountId { get; set; }
		Enums.MiningTiers MiningTier { get; set; }

		List<AccreditationCertificateMetadata> Certificates { get; }
	}

	/// <summary>
	///     This message will request on chain a registration so that we can participate in the comming elections
	/// </summary>
	public abstract class ElectionsRegistrationMessage : BlockchainMessage, IElectionsRegistrationMessage {

		// an encrypted instance of MinerRegistrationInfo
		public SafeArrayHandle EncryptedMessage { get; } = SafeArrayHandle.Create();

		/// <summary>
		///     We want this to be public and unencrypted, so everybody can know this account requested to be registered for the
		///     elections
		/// </summary>
		public AccountId AccountId { get; set; } = new AccountId();

		/// <summary>
		///     If we are delegating our winnings to another account (such as a mining pool), we indicate it here
		/// </summary>
		public AccountId DelegateAccountId { get; set; }

		/// <summary>
		///     requested mining tier
		/// </summary>
		public Enums.MiningTiers MiningTier { get; set; }

		public List<AccreditationCertificateMetadata> Certificates { get; private set; }

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.EncryptedMessage.Entry = rehydrator.ReadNonNullableArray();
			this.AccountId.Rehydrate(rehydrator);

			this.DelegateAccountId = rehydrator.ReadRehydratable<AccountId>();

			this.MiningTier = (Enums.MiningTiers) rehydrator.ReadByte();

			bool any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				AccreditationCertificateMetadataFactory factory = this.CreateAccreditationCertificateMetadataFactory();
				this.Certificates = new List<AccreditationCertificateMetadata>();

				for(int i = 0; i < count; i++) {

					SafeArrayHandle data = (SafeArrayHandle)rehydrator.ReadNonNullableArray();

					this.Certificates.Add(factory.RehydrateMetadata(data));
				}
			}
		}

		protected abstract AccreditationCertificateMetadataFactory CreateAccreditationCertificateMetadataFactory();

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.WriteNonNullable(this.EncryptedMessage);
			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.DelegateAccountId);

			dehydrator.Write((byte) this.MiningTier);

			bool any = this.Certificates?.Any() ?? false;
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.Certificates.Count);

				foreach(AccreditationCertificateMetadata entry in this.Certificates) {

					using IDataDehydrator subDh = DataSerializationFactory.CreateDehydrator();

					entry.Dehydrate(subDh);

					SafeArrayHandle data = subDh.ToArray();

					dehydrator.WriteNonNullable(data);
					data.Return();

				}
			}

		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.EncryptedMessage);
			nodesList.Add(this.AccountId);
			nodesList.Add(this.DelegateAccountId);
			
			nodesList.Add((byte)this.MiningTier);
			
			nodesList.Add(this.Certificates.Count);

			foreach(AccreditationCertificateMetadata entry in this.Certificates) {
				nodesList.Add(entry);
			}

			return nodesList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("EncryptedMessage", this.EncryptedMessage);
			jsonDeserializer.SetProperty("AccountId", this.AccountId);
			jsonDeserializer.SetProperty("DelegateAccountId", this.DelegateAccountId);
			
			jsonDeserializer.SetProperty("MiningTier", this.MiningTier.ToString());
			
			jsonDeserializer.SetArray("Certificates", this.Certificates);
		}

		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.ELECTIONS_REGISTRATION, 1, 0);
		}
	}
}