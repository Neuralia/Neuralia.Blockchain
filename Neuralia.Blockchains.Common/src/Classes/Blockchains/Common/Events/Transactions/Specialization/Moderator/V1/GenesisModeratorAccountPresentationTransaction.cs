using System;
using System.Collections.Immutable;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1 {

	public interface IGenesisModeratorAccountPresentationTransaction : IModerationKeyedTransaction {

		AccountId ModeratorAccountId { get; set; }

		NTRUPrimeCryptographicKey CommunicationsCryptographicKey { get; }
		NTRUPrimeCryptographicKey ValidatorSecretsCryptographicKey { get; }
		XmssCryptographicKey BlocksXmssCryptographicKey { get; }
		TripleXmssCryptographicKey BlocksChangeCryptographicKey { get; }
		XmssCryptographicKey DigestBlocksCryptographicKey { get; }
		TripleXmssCryptographicKey DigestBlocksChangeCryptographicKey { get; }
		XmssCryptographicKey BinaryCryptographicKey { get; }
		XmssCryptographicKey GossipCryptographicKey { get; }
		TripleXmssCryptographicKey SuperChangeCryptographicKey { get; }
		TripleXmssCryptographicKey PtahCryptographicKey { get; }

		bool IsCommunicationsKeyLoaded { get; }
		bool IsValidatorSecretsKeyLoaded { get; }
		bool IsBLocksChangeKeyLoaded { get; }
		bool IsDigestBlocksKeyLoaded { get; }
		bool IsDigestBlocksChangeKeyLoaded { get; }
		bool IsBinaryKeyLoaded { get; }
		bool IsGossipKeyLoaded { get; }
		bool IsSuperChangeKeyLoaded { get; }
		bool IsPtahKeyLoaded { get; }
	}

	public abstract class GenesisModeratorAccountPresentationTransaction : ModerationKeyedTransaction, IGenesisModeratorAccountPresentationTransaction {

		public GenesisModeratorAccountPresentationTransaction() {

			// CommunicationsKey
			this.Keyset.Add<NTRUPrimeCryptographicKey>(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID);

			// Validator Secrets
			this.Keyset.Add<NTRUPrimeCryptographicKey>(GlobalsService.MODERATOR_VALIDATOR_SECRETS_KEY_ID);
			
			// Blocks Key
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID);

			// Blocks change key
			this.Keyset.Add<TripleXmssCryptographicKey>(GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID);

			// DigestBlocksKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID);

			// DigestBlocks change key
			this.Keyset.Add<TripleXmssCryptographicKey>(GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID);
			
			// gossipKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_GOSSIP_KEY_ID);

			// binaryKey
			this.Keyset.Add<XmssCryptographicKey>(GlobalsService.MODERATOR_BINARY_KEY_ID);

			//  super change key
			this.Keyset.Add<TripleXmssCryptographicKey>(GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID);

			// PtahKey
			this.Keyset.Add<TripleXmssCryptographicKey>(GlobalsService.MODERATOR_PTAH_KEY_ID);
		}

		public bool IsBlocksXmssMTKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID);

		public AccountId ModeratorAccountId { get; set; } = new AccountId();

		public NTRUPrimeCryptographicKey CommunicationsCryptographicKey => (NTRUPrimeCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID];
		public NTRUPrimeCryptographicKey ValidatorSecretsCryptographicKey => (NTRUPrimeCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_VALIDATOR_SECRETS_KEY_ID];

		public XmssCryptographicKey BlocksXmssCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID];

		public TripleXmssCryptographicKey BlocksChangeCryptographicKey => (TripleXmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID];

		public XmssCryptographicKey DigestBlocksCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID];
		public TripleXmssCryptographicKey DigestBlocksChangeCryptographicKey => (TripleXmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID];

		public XmssCryptographicKey GossipCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_GOSSIP_KEY_ID];
		
		public XmssCryptographicKey BinaryCryptographicKey => (XmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_BINARY_KEY_ID];
		
		public TripleXmssCryptographicKey SuperChangeCryptographicKey => (TripleXmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID];
		public TripleXmssCryptographicKey PtahCryptographicKey => (TripleXmssCryptographicKey) this.Keyset.Keys[GlobalsService.MODERATOR_PTAH_KEY_ID];

		public bool IsCommunicationsKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID);
		public bool IsValidatorSecretsKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_VALIDATOR_SECRETS_KEY_ID);

		public bool IsBLocksChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID);

		public bool IsDigestBlocksKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID);
		public bool IsDigestBlocksChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID);

		public bool IsGossipKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_GOSSIP_KEY_ID);

		public bool IsBinaryKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_BINARY_KEY_ID);
		
		public bool IsSuperChangeKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID);
		public bool IsPtahKeyLoaded => this.Keyset.KeyLoaded(GlobalsService.MODERATOR_PTAH_KEY_ID);

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {

			string errorMessage = "{0} key data must be loaded to generate a sakura root";

			if(!this.IsCommunicationsKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Communications"));
			}

			if(!this.IsValidatorSecretsKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Validator Secrets"));
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
				throw new ApplicationException(string.Format(errorMessage, "Binary xmss"));
			}
			
			if(!this.IsGossipKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Gossip xmss"));
			}

			if(!this.IsSuperChangeKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Super change"));
			}

			if(!this.IsPtahKeyLoaded) {
				throw new ApplicationException(string.Format(errorMessage, "Ptah"));
			}

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray(types));

			nodeList.Add(this.ModeratorAccountId.GetStructuresArray());

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ModeratorAccountId", this.ModeratorAccountId);
		}

		public override Enums.TransactionTargetTypes TargetType => Enums.TransactionTargetTypes.All;
		public override AccountId[] ImpactedAccounts =>this.TargetAccounts;
		public override AccountId[] TargetAccounts => new[] {this.ModeratorAccountId};

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