using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccount {

		/// <summary>
		///     Our unique internal account id
		/// </summary>
		Guid AccountUuid { get; set; }

		/// <summary>
		///     This is our permanent public account ID. we get it once our presentation ID is confirmed
		/// </summary>
		AccountId PublicAccountId { get; set; }

		/// <summary>
		///     This is the hash of our account id which we use to obtain the final public account id
		/// </summary>
		AccountId AccountUuidHash { get; set; }

		TransactionId PresentationTransactionId { get; set; }

		/// <summary>
		/// If we have any correlation Id required, here it is set
		/// </summary>
		long? CorrelationId  { get; set; }
		
		string FriendlyName { get; set; }

		/// <summary>
		///     the trust level of this account in the network.false the higher the better.false 0 is completely untrusted
		/// </summary>
		/// <returns></returns>
		byte TrustLevel { get; set; }

		long ConfirmationBlockId { get; set; }

		Enums.AccountTypes WalletAccountType { get; set; }

		/// <summary>
		///     has this account been published and confirmed? if no, we can not use it yet, the network will reject it as unknown
		/// </summary>
		/// <returns></returns>
		Enums.PublicationStatus Status { get; set; }

		List<KeyInfo> Keys { get; set; }

		EncryptorParameters KeyLogFileEncryptionParameters { get; set; }

		ByteArray KeyLogFileSecret { get; set; }

		EncryptorParameters KeyHistoryFileEncryptionParameters { get; set; }

		ByteArray KeyHistoryFileSecret { get; set; }

		EncryptorParameters ChainStateFileEncryptionParameters { get; set; }

		ByteArray ChainStateFileSecret { get; set; }

		EncryptorParameters TransactionCacheFileEncryptionParameters { get; set; }

		ByteArray TransactionCacheFileSecret { get; set; }

		bool KeysEncrypted { get; set; }
		bool KeysEncryptedIndividually { get; set; }

		void InitializeNew(string name, BlockchainServiceSet serviceSet, Enums.AccountTypes accountType);

		void ClearEncryptionParameters();
		void InitializeNewEncryptionParameters(BlockchainServiceSet serviceSet);

		AccountId GetAccountId();
	}

	public abstract class WalletAccount : IWalletAccount {

		static WalletAccount() {
			LiteDBMappers.RegisterAccountId();
		}

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public EncryptorParameters ElectionCacheFileEncryptionParameters { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		/// </summary>
		public ByteArray ElectionCacheFileSecret { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		///     </Snapshot
		public ByteArray SnapshotFileSecret { get; set; }

		/// <summary>
		///     The Snapshot file encryption parameters
		/// </summary>
		public EncryptorParameters SnapshotFileEncryptionParameters { get; set; }

		public Guid AccountUuid { get; set; }

		/// <summary>
		///     This is our permanent public account ID. we get it once our presentation ID is confirmed
		/// </summary>
		public AccountId PublicAccountId { get; set; }

		/// <summary>
		///     The block where this account was confirmed
		/// </summary>
		public long ConfirmationBlockId { get; set; }

		/// <summary>
		///     This is the hash of our account id which we use to obtain the final public account id
		/// </summary>
		public AccountId AccountUuidHash { get; set; }

		/// <summary>
		///     The presentation transaction that presented this account
		/// </summary>
		public TransactionId PresentationTransactionId { get; set; }

		/// <summary>
		/// If we have any correlation Id required, here it is set
		/// </summary>
		public long? CorrelationId { get; set; }

		public Enums.AccountTypes WalletAccountType { get; set; } = Enums.AccountTypes.Standard;

		public string FriendlyName { get; set; }

		/// <summary>
		///     the trust level of this account in the network. the higher the better.false 0 is completely untrusted
		/// </summary>
		/// <returns></returns>
		public byte TrustLevel { get; set; }

		/// <summary>
		///     ARE THE KEYS ENCRYPTED?
		/// </summary>
		public bool KeysEncrypted { get; set; }

		public bool KeysEncryptedIndividually { get; set; }

		/// <summary>
		///     has this account been published and confirmed? if no, we can not use it yet, the network will reject it as unknown
		/// </summary>
		/// <returns></returns>
		public Enums.PublicationStatus Status { get; set; }

		public virtual void InitializeNew(string name, BlockchainServiceSet serviceSet, Enums.AccountTypes accountType) {
			IBlockchainGuidService guidService = serviceSet.BlockchainGuidService;

			this.AccountUuid = guidService.Create();
			this.AccountUuidHash = guidService.CreateTemporaryAccountId(this.AccountUuid, accountType);

			this.WalletAccountType = accountType;
			this.FriendlyName = name;
			this.Status = Enums.PublicationStatus.New;
			this.TrustLevel = 0; // untrusted

		}

		public virtual void ClearEncryptionParameters() {
			this.ClearKeyLogEncryptionParameters();
			this.ClearKeyHistoryEncryptionParameters();
			this.ClearChainStateEncryptionParameters();
			this.ClearTransactionCacheEncryptionParameters();
			this.ClearElectionCacheEncryptionParameters();
			this.ClearSnapshotEncryptionParameters();
			this.ClearKeyHistoryEncryptionParameters();
		}

		public virtual void InitializeNewEncryptionParameters(BlockchainServiceSet serviceSet) {
			this.InitializeNewKeyLogEncryptionParameters(serviceSet);
			this.InitializeNewKeyHistoryEncryptionParameters(serviceSet);
			this.InitializeNewChainStateEncryptionParameters(serviceSet);
			this.InitializeNewTransactionCacheEncryptionParameters(serviceSet);
			this.InitializeNewElectionCacheEncryptionParameters(serviceSet);
			this.InitializeNewSnapshotEncryptionParameters(serviceSet);
			this.InitializeNewKeyHistoryEncryptionParameters(serviceSet);
		}

		/// <summary>
		///     here we return our keys as a list
		/// </summary>
		/// <returns></returns>
		public List<KeyInfo> Keys { get; set; } = new List<KeyInfo>();

		/// <summary>
		///     The keyLog file encryption parameters
		/// </summary>
		public EncryptorParameters KeyLogFileEncryptionParameters { get; set; }

		/// <summary>
		///     The keylog file encryption key
		/// </summary>
		public ByteArray KeyLogFileSecret { get; set; }

		public EncryptorParameters KeyHistoryFileEncryptionParameters { get; set; }
		public ByteArray KeyHistoryFileSecret { get; set; }

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public EncryptorParameters ChainStateFileEncryptionParameters { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		/// </summary>
		public ByteArray ChainStateFileSecret { get; set; }

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public EncryptorParameters TransactionCacheFileEncryptionParameters { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		/// </summary>
		public ByteArray TransactionCacheFileSecret { get; set; }

		public AccountId GetAccountId() {
			if((this.PublicAccountId == null) || (this.PublicAccountId == new AccountId())) {
				return this.AccountUuidHash;
			}

			return this.PublicAccountId;
		}

		private void ClearKeyLogEncryptionParameters() {
			this.KeyLogFileEncryptionParameters = null;
			this.KeyLogFileSecret = null;
		}

		private void ClearKeyHistoryEncryptionParameters() {
			this.KeyHistoryFileEncryptionParameters = null;
			this.KeyHistoryFileSecret = null;
		}

		private void ClearChainStateEncryptionParameters() {
			this.ChainStateFileEncryptionParameters = null;
			this.ChainStateFileSecret = null;
		}

		private void ClearTransactionCacheEncryptionParameters() {
			this.TransactionCacheFileEncryptionParameters = null;
			this.TransactionCacheFileSecret = null;
		}

		private void ClearElectionCacheEncryptionParameters() {
			this.ElectionCacheFileEncryptionParameters = null;
			this.ElectionCacheFileSecret = null;
		}

		private void ClearSnapshotEncryptionParameters() {
			this.SnapshotFileEncryptionParameters = null;
			this.SnapshotFileSecret = null;
		}

		private void InitializeNewKeyLogEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.KeyLogFileEncryptionParameters == null) {
				this.KeyLogFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.KeyLogFileSecret = secretKey;
			}
		}

		private void InitializeNewKeyHistoryEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.KeyHistoryFileEncryptionParameters == null) {
				this.KeyHistoryFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.KeyHistoryFileSecret = secretKey;
			}
		}

		private void InitializeNewChainStateEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.ChainStateFileEncryptionParameters == null) {
				this.ChainStateFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.ChainStateFileSecret = secretKey;
			}
		}

		private void InitializeNewTransactionCacheEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.TransactionCacheFileEncryptionParameters == null) {
				this.TransactionCacheFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.TransactionCacheFileSecret = secretKey;
			}
		}

		private void InitializeNewElectionCacheEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.ElectionCacheFileEncryptionParameters == null) {
				this.ElectionCacheFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.ElectionCacheFileSecret = secretKey;
			}
		}

		private void InitializeNewSnapshotEncryptionParameters(BlockchainServiceSet serviceSet) {
			// create those no matter what
			if(this.SnapshotFileEncryptionParameters == null) {
				this.SnapshotFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(GlobalSettings.ApplicationSettings);
				var secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.SnapshotFileSecret = secretKey;
			}
		}
	}
}