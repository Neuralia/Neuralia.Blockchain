using System;
using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccount {

		/// <summary>
		///     Our unique internal account id
		/// </summary>
		string AccountCode { get; set; }

		/// <summary>
		///     This is our permanent public account ID. we get it once our presentation ID is confirmed
		/// </summary>
		AccountId PublicAccountId { get; set; }

		/// <summary>
		///     This is the hash of our account id which we use to obtain the final public account id
		/// </summary>
		AccountId PresentationId { get; set; }

		TransactionId PresentationTransactionId { get; set; }

		/// <summary>
		/// if the account has been Verified with an ID or another piece of information
		/// </summary>
		Enums.AccountVerificationTypes VerificationLevel { get; set; }

		string VerificationData { get; set; }
		
		DateTime? VerificationDate { get; set; } 
		
		DateTime? VerificationExpirationDate { get; set; }
		
		string FriendlyName { get; set; }

		/// <summary>
		///     the trust level of this account in the network.false the higher the better.false 0 is completely untrusted
		/// </summary>
		/// <returns></returns>
		byte TrustLevel { get; set; }

		long ConfirmationBlockId { get; set; }

		Enums.AccountTypes WalletAccountType { get; set; }
		
		SafeArrayHandle Stride { get; set; }

		/// <summary>
		///     has this account been published and confirmed? if no, we can not use it yet, the network will reject it as unknown
		/// </summary>
		/// <returns></returns>
		Enums.PublicationStatus Status { get; set; }

		DateTime? PresentationTransactionTimeout { get; set; }

		List<KeyInfo> Keys { get; set; }

		IEncryptorParameters KeyLogFileEncryptionParameters { get; set; }

		SafeArrayHandle KeyLogFileSecret { get; set; }

		IEncryptorParameters KeyHistoryFileEncryptionParameters { get; set; }

		SafeArrayHandle KeyHistoryFileSecret { get; set; }

		IEncryptorParameters ChainStateFileEncryptionParameters { get; set; }

		SafeArrayHandle ChainStateFileSecret { get; set; }

		IEncryptorParameters GenerationCacheFileEncryptionParameters { get; set; }

		SafeArrayHandle GenerationCacheFileSecret { get; set; }
		
		bool KeysEncrypted { get; set; }
		bool KeysEncryptedIndividually { get; set; }

		void InitializeNew(string name, Enums.AccountTypes accountType, BlockchainServiceSet serviceSet);

		void ClearEncryptionParameters();
		void InitializeNewEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration);

		AccountId GetAccountId();
		
		WalletAccount.AccountAppointmentDetails AccountAppointment { get; set; }
		
		WalletAccount.AccountSMSDetails SMSDetails  { get; set; }

		// mining cache
		WalletAccount.WalletAccountChainStateMiningCache MiningCache { get; set; }
	}

	public abstract class WalletAccount : IWalletAccount {

		static WalletAccount() {
			LiteDBMappers.RegisterAccountId();
		}

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public IEncryptorParameters ElectionCacheFileEncryptionParameters { get; set; }

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
		public IEncryptorParameters SnapshotFileEncryptionParameters { get; set; }

		public string AccountCode { get; set; }

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
		public AccountId PresentationId { get; set; }

		/// <summary>
		///     The presentation transaction that presented this account
		/// </summary>
		public TransactionId PresentationTransactionId { get; set; }

		/// <summary>
		///     If we have any verification Id required, here it is set
		/// </summary>
		public Enums.AccountVerificationTypes VerificationLevel { get; set; }

		/// <summary>
		/// the data that we used to correlate the account
		/// </summary>
		public string VerificationData { get; set; }
		
		public DateTime? VerificationDate { get; set; }
		public DateTime? VerificationExpirationDate { get; set; }
		
		public Enums.AccountTypes WalletAccountType { get; set; } = Enums.AccountTypes.User;
		
		/// <summary>
		/// if its a server account, then these are the nonces from the presentation
		/// </summary>
		public SafeArrayHandle Stride { get; set; }

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
		public Enums.PublicationStatus Status { get; set; } = Enums.PublicationStatus.New;

		public DateTime? PresentationTransactionTimeout { get; set; } = null;

		public WalletAccountChainStateMiningCache MiningCache { get; set; } = null;
		

		public virtual void InitializeNew(string name, Enums.AccountTypes accountType, BlockchainServiceSet serviceSet) {
			IBlockchainGuidService guidService = serviceSet.BlockchainGuidService;

			this.AccountCode = guidService.CreateAccountCode();
			this.PresentationId = guidService.CreateTemporaryAccountId(this.AccountCode);

			this.WalletAccountType = accountType;
			this.FriendlyName = name;
			this.Status = Enums.PublicationStatus.New;
			this.TrustLevel = 0; // untrusted
		}

		public virtual void ClearEncryptionParameters() {
			this.ClearKeyLogEncryptionParameters();
			this.ClearKeyHistoryEncryptionParameters();
			this.ClearChainStateEncryptionParameters();
			this.ClearGenerationCacheEncryptionParameters();
			this.ClearElectionCacheEncryptionParameters();
			this.ClearSnapshotEncryptionParameters();
			this.ClearKeyHistoryEncryptionParameters();
		}

		public virtual void InitializeNewEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			this.InitializeNewKeyLogEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewKeyHistoryEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewChainStateEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewGenerationCacheEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewElectionCacheEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewSnapshotEncryptionParameters(serviceSet, chainConfiguration);
			this.InitializeNewKeyHistoryEncryptionParameters(serviceSet, chainConfiguration);
		}

		/// <summary>
		///     here we return our keys as a list
		/// </summary>
		/// <returns></returns>
		public List<KeyInfo> Keys { get; set; } = new List<KeyInfo>();

		/// <summary>
		///     The keyLog file encryption parameters
		/// </summary>
		public IEncryptorParameters KeyLogFileEncryptionParameters { get; set; }

		/// <summary>
		///     The keylog file encryption key
		/// </summary>
		public SafeArrayHandle KeyLogFileSecret { get; set; }

		public IEncryptorParameters KeyHistoryFileEncryptionParameters { get; set; }
		public SafeArrayHandle KeyHistoryFileSecret { get; set; }

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public IEncryptorParameters ChainStateFileEncryptionParameters { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		/// </summary>
		public SafeArrayHandle ChainStateFileSecret { get; set; }

		/// <summary>
		///     The ChainState file encryption parameters
		/// </summary>
		public IEncryptorParameters GenerationCacheFileEncryptionParameters { get; set; }

		/// <summary>
		///     The ChainState file encryption key
		/// </summary>
		public SafeArrayHandle GenerationCacheFileSecret { get; set; }
		
		// now appointment information
		
		public AccountAppointmentDetails AccountAppointment { get; set; }
		
		public AccountSMSDetails SMSDetails  { get; set; }
		
		/// <summary>
		/// here we can store the last dispatched initiation or presentation transaction so we can reset if lost.
		/// </summary>
		public SafeArrayHandle LastSentCachedOperation { get; set; } = null;
		
		/// <summary>
		/// the time at which this cached transaction will be expired
		/// </summary>
		public DateTime? LastSentCachedOperationExpiration { get; set; } = null;
		
		public AccountId GetAccountId() {
			if((this.PublicAccountId == default(AccountId)) || (this.PublicAccountId == new AccountId())) {
				return this.PresentationId;
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

		private void ClearGenerationCacheEncryptionParameters() {
			this.GenerationCacheFileEncryptionParameters = null;
			this.GenerationCacheFileSecret = null;
		}

		private void ClearElectionCacheEncryptionParameters() {
			this.ElectionCacheFileEncryptionParameters = null;
			this.ElectionCacheFileSecret = null;
		}

		private void ClearSnapshotEncryptionParameters() {
			this.SnapshotFileEncryptionParameters = null;
			this.SnapshotFileSecret = null;
		}

		private void InitializeNewKeyLogEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.KeyLogFileEncryptionParameters == null) {
				this.KeyLogFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.KeyLogFileSecret = SafeArrayHandle.WrapAndOwn(secretKey);
			}
		}

		private void InitializeNewKeyHistoryEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.KeyHistoryFileEncryptionParameters == null) {
				this.KeyHistoryFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.KeyHistoryFileSecret = SafeArrayHandle.WrapAndOwn(secretKey);
			}
		}

		private void InitializeNewChainStateEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.ChainStateFileEncryptionParameters == null) {
				this.ChainStateFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.ChainStateFileSecret = SafeArrayHandle.WrapAndOwn(secretKey);
			}
		}

		private void InitializeNewGenerationCacheEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.GenerationCacheFileEncryptionParameters == null) {
				this.GenerationCacheFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.GenerationCacheFileSecret = SafeArrayHandle.WrapAndOwn(secretKey);
			}
		}

		private void InitializeNewElectionCacheEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.ElectionCacheFileEncryptionParameters == null) {
				this.ElectionCacheFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.ElectionCacheFileSecret = ByteArray.WrapAndOwn(secretKey);
			}
		}

		private void InitializeNewSnapshotEncryptionParameters(BlockchainServiceSet serviceSet, ChainConfigurations chainConfiguration) {
			// create those no matter what
			if(this.SnapshotFileEncryptionParameters == null) {
				this.SnapshotFileEncryptionParameters = FileEncryptorUtils.GenerateEncryptionParameters(chainConfiguration);
				byte[] secretKey = new byte[333];
				GlobalRandom.GetNextBytes(secretKey);
				this.SnapshotFileSecret = ByteArray.WrapAndOwn(secretKey);
			}
		}

		public class WalletAccountChainStateMiningCache {
			public long MiningPassword { get; set; } = 0;

			public byte[] MiningAutograph { get; set; } = null;

			public DateTime LastMiningRegistrationUpdate { get; set; } = DateTimeEx.MinValue;
		}
		
		public class AccountSMSDetails {

			public Guid RequesterId { get; set; }
			
			public long ConfirmationCode { get; set; }
			
			/// <summary>
			/// for how long is this Id valid? after this deadline, the entire appointment is considered expired and void
			/// </summary>
			public DateTime ConfirmationCodeExpiration { get; set; }
			
			/// <summary>
			/// how long the verification will last
			/// </summary>
			public TimeSpan? VerificationSpan { get; set; }
		}

		public class AccountAppointmentDetails {
			

			
			public Guid? RequesterId { get; set; }

			private DateTime? appointmentTime = null;
			private DateTime? appointmentExpirationTime = null;
			private DateTime? appointmentContextTime;
			private DateTime? appointmentVerificationTime;
			private DateTime? appointmentRequestTimeStamp;
			private DateTime? appointmentConfirmationCodeExpiration;
			private DateTime? lastAppointmentOperationTimeout;

			/// <summary>
			/// the actual appointment date/time
			/// </summary>
			public DateTime? AppointmentTime {
				get => this.appointmentTime?.ToUniversalTime();
				set => this.appointmentTime = value;
			}

			public DateTime? AppointmentExpirationTime {
				get => this.appointmentExpirationTime?.ToUniversalTime();
				set => this.appointmentExpirationTime = value;
			}
			
			/// <summary>
			/// What time do we expect the contexts to start to arrive
			/// </summary>
			public DateTime? AppointmentContextTime {
				get => this.appointmentContextTime?.ToUniversalTime();
				set => this.appointmentContextTime = value;
			}

			/// <summary>
			/// when can we expect the verifications to be fully completed
			/// </summary>
			public DateTime? AppointmentVerificationTime {
				get => this.appointmentVerificationTime?.ToUniversalTime();
				set => this.appointmentVerificationTime = value;
			}

			/// <summary>
			///     has this account been published and confirmed? if no, we can not use it yet, the network will reject it as unknown
			/// </summary>
			/// <returns></returns>
			public Enums.AppointmentStatus AppointmentStatus { get; set; } = Enums.AppointmentStatus.None;

			public DateTime? AppointmentRequestTimeStamp {
				get => this.appointmentRequestTimeStamp?.ToUniversalTime();
				set => this.appointmentRequestTimeStamp = value;
			}

			/// <summary>
			/// the identifying XMSS key
			/// </summary>
			public byte[] IdentitySignatureKey { get; set; }
			/// <summary>
			/// The seed we will need to decrypt the response when it arrives
			/// </summary>
			public byte[] VerificationResponseSeed  { get; set; }
			
			/// <summary>
			/// this is our secret appointment Id
			/// </summary>
			public Guid? AppointmentId { get; set; } = null;
			
			/// <summary>
			/// our unique public index in the context set
			/// </summary>
			public long? AppointmentIndex { get; set; }  = null;
			
			/// <summary>
			/// the NTRUe private key we use during the appointment process
			/// </summary>
			public SafeArrayHandle AppointmentPrivateKey { get; set; }
			
			/// <summary>
			/// how much time is allocated form start to finish
			/// </summary>
			public int? AppointmentWindow { get; set; }

			/// <summary>
			/// what kind of result di we get
			/// </summary>
			public Enums.AppointmentResults LastAppointmentResult { get; set; } = Enums.AppointmentResults.None;
			
			public long? AppointmentConfirmationCode { get; set; } = null;

			/// <summary>
			/// for how long is this Id valid? after this deadline, the entire appointment is considered expired and void
			/// </summary>
			public DateTime? AppointmentConfirmationCodeExpiration {
				get => this.appointmentConfirmationCodeExpiration?.ToUniversalTime();
				set => this.appointmentConfirmationCodeExpiration = value;
			}

			/// <summary>
			/// was the account verified?  true is yes, false is rejected.
			/// </summary>
			public bool? AppointmentVerified { get; set; } = null;

			/// <summary>
			/// how long will the next verification last before it expires and a new verification must be performed
			/// </summary>
			public TimeSpan? AppointmentVerificationSpan{ get; set; } = null;
			/// <summary>
			/// has the context details been received and cached?
			/// </summary>
			public bool AppointmentContextDetailsCached { get; set; }  = false;

			/// <summary>
			/// the grace delay before we declare the last operation as timed out
			/// </summary>
			public DateTime? LastAppointmentOperationTimeout {
				get => this.lastAppointmentOperationTimeout?.ToUniversalTime();
				set => this.lastAppointmentOperationTimeout = value;
			}
		}
	}
}