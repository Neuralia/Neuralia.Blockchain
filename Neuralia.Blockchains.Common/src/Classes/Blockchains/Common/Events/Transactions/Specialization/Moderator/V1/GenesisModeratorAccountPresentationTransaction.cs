using System;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {

	public interface IGenesisModeratorAccountPresentationTransaction : IModerationKeyedTransaction {

		AccountId ModeratorAccountId { get; set; }

		NtruCryptographicKey CommunicationsCryptographicKey { get; }
		XmssCryptographicKey BlocksXmssCryptographicKey { get; }
		SecretPentaCryptographicKey BlocksChangeCryptographicKey { get; }
		XmssCryptographicKey DigestBlocksCryptographicKey { get; }
		SecretPentaCryptographicKey DigestBlocksChangeCryptographicKey { get; }
		XmssCryptographicKey BinaryCryptographicKey { get; }
		SecretPentaCryptographicKey SuperChangeCryptographicKey { get; }
		SecretPentaCryptographicKey PtahCryptographicKey { get; }

		bool IsCommunicationsKeyLoaded { get; }
		bool IsBLocksChangeKeyLoaded { get; }
		bool IsDigestBlocksKeyLoaded { get; }
		bool IsDigestBlocksChangeKeyLoaded { get; }
		bool IsBinaryKeyLoaded { get; }
		bool IsSuperChangeKeyLoaded { get; }
		bool IsPtahKeyLoaded { get; }
	}

	public abstract class GenesisModeratorAccountPresentationTransaction : ModerationKeyedTransaction, IGenesisModeratorAccountPresentationTransaction {

		public GenesisModeratorAccountPresentationTransaction() {

			// CommunicationsKey
			this.Keyset.Add<NtruCryptographicKey>(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID);

			// Blocks Key
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID);

			// Blocks change key
			this.Keyset.Add<SecretPentaCryptographicKey>(GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID);

			// DigestBlocksKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID);

			// DigestBlocks change key
			this.Keyset.Add<SecretPentaCryptographicKey>(GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID);

			// binaryKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_BINARY_KEY_ID);

			//  super change key
			this.Keyset.Add<SecretPentaCryptographicKey>(GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID);

			// PtahKey
			this.Keyset.Add<SecretPentaCryptographicKey>(GlobalsService.MODERATOR_PTAH_KEY_ID);
		}

		public bool IsBlocksXmssMTKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID);

		public AccountId ModeratorAccountId { get; set; } = new AccountId();

		public NtruCryptographicKey CommunicationsCryptographicKey => (NtruCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID];

		public XmssCryptographicKey BlocksXmssCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID];

		public SecretPentaCryptographicKey BlocksChangeCryptographicKey => (SecretPentaCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID];

		public XmssCryptographicKey DigestBlocksCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID];
		public SecretPentaCryptographicKey DigestBlocksChangeCryptographicKey => (SecretPentaCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID];

		public XmssCryptographicKey BinaryCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BINARY_KEY_ID];

		public SecretPentaCryptographicKey SuperChangeCryptographicKey => (SecretPentaCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID];
		public SecretPentaCryptographicKey PtahCryptographicKey => (SecretPentaCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_PTAH_KEY_ID];

		public bool IsCommunicationsKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID);

		public bool IsBLocksChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID);

		public bool IsDigestBlocksKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID);
		public bool IsDigestBlocksChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID);

		public bool IsBinaryKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BINARY_KEY_ID);

		public bool IsSuperChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID);
		public bool IsPtahKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_PTAH_KEY_ID);

		public override HashNodeList GetStructuresArray() {

			string errorMessage = "{0} key data must be loaded to generate a sakura root";

			if(!this.IsCommunicationsKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Communications"));
			}

			if(!this.IsBlocksXmssMTKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Blocks xmssmt"));
			}

			if(!this.IsBLocksChangeKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Blocks change"));
			}

			if(!this.IsDigestBlocksKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Digest Blocks"));
			}

			if(!this.IsDigestBlocksChangeKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Digest Blocks change"));
			}

			if(!this.IsBinaryKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Binary Blocks"));
			}

			if(!this.IsSuperChangeKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Super change"));
			}

			if(!this.IsPtahKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Ptah"));
			}

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());

			nodeList.Add(this.ModeratorAccountId.GetStructuresArray());

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ModeratorAccountId", this.ModeratorAccountId);
		}

		public override ImmutableList<AccountId> TargetAccounts => new[] {this.ModeratorAccountId}.ToImmutableList();

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.ModeratorAccountId.Rehydrate(rehydrator);

		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.ModeratorAccountId.Dehydrate(dehydrator);

		}

		protected override ComponentVersion<TransactionType> SetIdentity() {
			return (TransactionTypes.Instance.GENESIS, 1, 0);
		}
	}
}