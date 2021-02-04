using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IAppointmentsProvider : IChainProvider {
		Enums.OperationStatus OperatingMode { get; set; }
		Enums.AppointmentStatus AppointmentMode { get; set; }
		bool IsValidator { get; }
		bool IsValidatorWindow { get; }
		bool IsValidatorWindowProximity { get; }
		IAppointmentRegistryDal AppointmentRegistryDal { get; }

		Task<bool> ClearAppointment(LockContext lockContext, string accountCode = null, bool force = false);
		void ClearAppointmentDetails();

		void SetPuzzleAnswers(List<int> answers);
		Task<List<(DateTime appointment, TimeSpan window, int requesterCount)>> GetAppointmentWindows();
		Task<ConcurrentDictionary<int, (int secretCodeL2, long index)[]>> GetValidatorAssignedCodes(DateTime appointment);
		Task<ConcurrentDictionary<int, int>> GetValidatorAssignedIndices(DateTime appointment);
		Task<bool> IsRequesterIndexAssigned(DateTime appointment, int index);
		Task RecordRequestSecretCode(DateTime appointment, int index, SafeArrayHandle validatorCode, DateTime? timestamp = null);
		Task RecordTriggerPuzzle(DateTime appointment, int index, int secretCode, DateTime? timestamp = null);
		Task<bool> RecordCompletePuzzle(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, DateTime? timestamp = null);
		Task<bool> RecordCompleteTHS(DateTime appointment, int index, SafeArrayHandle thsResults, DateTime? timestamp = null);

		Task EnsureAppointmentDetails(DateTime appointment);
		Task<bool> AppointmentExistsAndStarted(DateTime appointment);
		bool CheckAppointmentExistsAndStarted(DateTime appointment);
		Task<SafeArrayHandle> GetAppointmentKey(DateTime appointment);

		Task HandleBlockchainMessage(IAppointmentBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext);
		Task CleanAppointmentsRegistry();
		Task CheckOperatingMode(LockContext lockContext);
		Task UpdateOperatingMode(LockContext lockContext);
		Task<SafeArrayHandle> CheckAppointmentTriggerUpdate(DateTime appointment, LockContext lockContext);

		Task<SafeArrayHandle> GetAppointmentRequestConfirmationMessage(Guid requesterId, DateTime? appointment, LockContext lockContext);
		Task<SafeArrayHandle> GetAppointmentVerificationConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext);
		Task<SafeArrayHandle> GetAppointmentContextGossipMessage(int requesterIndex, DateTime appointment, LockContext lockContext);
		Task<SafeArrayHandle> GetAppointmentTriggerGossipMessage(DateTime appointment, LockContext lockContext);
		Task UpdateAppointmentDetails(DateTime appointment);
		event Action AppointmentPuzzleCompletedEvent;
		event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;
		Task<Dictionary<int, bool>> PrepareAppointmentRequesterResult(DateTime appointmentTime, List<IAppointmentRequesterResult> entries, LockContext lockContext);
		ConcurrentDictionary<DateTime, AppointmentsProvider.AppointmentPuzzleStrikesSet> AppointmentPuzzleStrikes { get; }
	}

	public interface IAppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAppointmentsProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class AppointmentsProvider {
		public class ValidatorState {

			public const int STRIKE_COUNT = 3;
			public const int FULL_ROUND_STRIKE_COUNT = STRIKE_COUNT*2;
			[Flags]
			public enum AppointmentValidationWorkflowSteps : byte {
				None = 0,
				CodeTranslation = 1 << 0,
				TriggerSession = 1 << 1,
				PuzzleCompleted = 1 << 2,
				THSCompleted = 1 << 3,
			
				CodeAndTrigger = CodeTranslation | TriggerSession
			}

			public AppointmentValidationWorkflowSteps Status { get; set; }
			public int Strikes { get; set; }
			public SafeArrayHandle ValidatorCode { get; set; }
			public int SecretCodeL2 { get; set; }

			public Enums.AppointmentValidationProtocols Protocol { get; set; } = Enums.AppointmentValidationProtocols.Undefined;
		
			/// <summary>
			/// have we connected at least once on this protocol
			/// </summary>
			public bool Connected { get; set; }
			
			/// <summary>
			/// number of failed attempts to connect on this protocol
			/// </summary>
			public int FailedConnectionAttempts { get; set; }
			/// <summary>
			/// can we still try on this protocol
			/// </summary>
			public bool ProtocolValid => this.FailedConnectionAttempts < STRIKE_COUNT;
			
			/// <summary>
			/// if true, we are done with this validator, nothing left to do, it is exhaused
			/// </summary>
			public bool NoMoreTries => !this.ProtocolValid && this.Protocol == Enums.AppointmentValidationProtocols.Backup;
			
			public bool Contacted => this.Connected || this.Strikes != 0;
			public bool StrikesValid => this.Strikes < STRIKE_COUNT;
			public bool StrikesInvalid => !this.StrikesValid;
			
			public bool CodeTranslationCompleted => this.Status.HasFlag(AppointmentValidationWorkflowSteps.CodeTranslation);
			public bool TriggerSessionCompleted => this.Status.HasFlag(AppointmentValidationWorkflowSteps.TriggerSession);
			public bool PuzzleCompleted => this.Status.HasFlag(AppointmentValidationWorkflowSteps.PuzzleCompleted);
			public bool THSCompleted => this.Status.HasFlag(AppointmentValidationWorkflowSteps.THSCompleted);
		}
		
		
		public class AppointmentPuzzleStrikesSet {
			public ConcurrentDictionary<Guid, AppointmentsProvider.ValidatorState> Validators { get; } = new ConcurrentDictionary<Guid, AppointmentsProvider.ValidatorState>();
		}

	}

	public abstract class AppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IAppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly object appointmentWorkflowLocker = new();
		private readonly object sendVerificationResultsWorkflowLocker = new();

		private readonly object locker = new();

		private IAppointmentRegistryDal appointmentRegistryDal;
		private IPuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> appointmentWorkflow;

		protected DateTime? checkAppointmentsContexts;
		protected DateTime? checkAppointmentsDispatches;
		private ConcurrentDictionary<DateTime, ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>> sendVerificationResultsWorkflows = new ConcurrentDictionary<DateTime, ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>>();
		public ConcurrentDictionary<DateTime, AppointmentsProvider.AppointmentPuzzleStrikesSet> AppointmentPuzzleStrikes { get; } = new ConcurrentDictionary<DateTime, AppointmentsProvider.AppointmentPuzzleStrikesSet>();
		private static TimeSpan LastAppointmentOperationTimeoutSpan = TimeSpan.FromMinutes(30);

		public AppointmentsProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;

		}

		protected CENTRAL_COORDINATOR CentralCoordinator { get; }

		public virtual IAppointmentRegistryDal AppointmentRegistryDal {
			get {
				lock(this.locker) {
					if(this.appointmentRegistryDal == null) {
						bool enablePuzzleTHS = !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableAppointmentPuzzleTHS;
						this.appointmentRegistryDal = this.CentralCoordinator.ChainDalCreationFactory.CreateAppointmentRegistryDal<IAppointmentRegistryDal>(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), this.CentralCoordinator, enablePuzzleTHS, this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.appointmentRegistryDal;
			}
		}

		public event Action AppointmentPuzzleCompletedEvent;
		public event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;

		public Enums.OperationStatus OperatingMode { get; set; } = Enums.OperationStatus.Unknown;
		public Enums.AppointmentStatus AppointmentMode { get; set; } = Enums.AppointmentStatus.None;
		public bool IsValidator => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidator;

		public bool IsValidatorWindow => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidatorWindow;
		public bool IsValidatorWindowProximity => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidatorWindowProximity;

		public virtual Task HandleBlockchainMessage(IAppointmentBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext) {

			if(message is IAppointmentRequestConfirmationMessage appointmentRequestConfirmationMessage) {
				return this.RecordAppointmentRequestConfirmationMessage(appointmentRequestConfirmationMessage, messageEnvelope, lockContext);
			}

			if(message is IAppointmentVerificationConfirmationMessage appointmentVerificationConfirmationMessage) {
				return this.RecordAppointmentVerificationConfirmationMessage(appointmentVerificationConfirmationMessage, messageEnvelope, lockContext);
			}

			if(message is IAppointmentContextMessage appointmentContextMessage) {
				return this.RecordAppointmentContextGossipMessage(appointmentContextMessage, messageEnvelope, lockContext);
			}

			if(message is IAppointmentTriggerMessage appointmentTriggerMessage) {
				return this.RecordAppointmentTriggerGossipMessage(appointmentTriggerMessage, messageEnvelope, lockContext);
			}

			return Task.CompletedTask;
		}

		/// <summary>
		///     this method allows to check if its time to act, or if we should sleep more
		/// </summary>
		/// <returns></returns>
		protected bool ShouldAct(ref DateTime? action) {
			if(!action.HasValue) {
				return true;
			}

			if(action.Value < DateTimeEx.CurrentTime) {
				action = null;

				return true;
			}

			return false;
		}

	#region Appointments

		/// <summary>
		///     do some cleaning and remove all cached appointments that are expired.
		/// </summary>
		/// <returns></returns>
		public async Task CleanAppointmentsRegistry() {
			IAppointmentRegistryDal appointmentsDal = this.AppointmentRegistryDal;

			if(appointmentsDal != null) {
				List<Guid> messageIds = await this.AppointmentRegistryDal.ClearExpired().ConfigureAwait(false);

				await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.ClearCachedAppointmentMessages(messageIds).ConfigureAwait(false);
			}

			foreach(DateTime entry in this.AppointmentDetails.Keys.Where(d => d.AddDays(1) < DateTimeEx.CurrentTime)) {
				this.AppointmentDetails.Remove(entry, out AppointmentDataDetails _);
				this.AppointmentPuzzleStrikes.TryRemove(entry, out var _);
			}
		}

		public void ClearAppointmentDetails() {
			this.AppointmentDetails.Clear();
			this.AppointmentPuzzleStrikes.Clear();
		}

		public virtual async Task<bool> RecordAppointmentRequestConfirmationMessage(IAppointmentRequestConfirmationMessage appointmentRequestConfirmationMessage, IMessageEnvelope messageEnvelope, LockContext lockContext) {

			// store the file in the cache
			SafeArrayHandle envelopeBytes = messageEnvelope.DehydrateEnvelope();
			await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheAppointmentMessage(messageEnvelope.ID, envelopeBytes).ConfigureAwait(false);

			List<Guid> requesters = appointmentRequestConfirmationMessage.Requesters.Select(r => r.RequesterId).ToList();
			await this.AppointmentRegistryDal.InsertAppointmentRequestConfirmationMessage(requesters, messageEnvelope.ID, appointmentRequestConfirmationMessage.AppointmentTimestamp).ConfigureAwait(false);

			if(this.IsInAppointmentOperation) {

				// ok, we are in an appointment. lets do some followup
				// ok, we are in an appointment
				IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

				// ok, we are way behind. we need to get our confirmation
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentRequested) {

					if(requesters.Contains(account.AccountAppointment.RequesterId.Value)) {

						AppointmentRequestConfirmationMessage.AppointmentRequester localInfo = appointmentRequestConfirmationMessage.Requesters.SingleOrDefault(r => r.RequesterId == account.AccountAppointment.RequesterId.Value);

						await this.ProcessAppointmentRequest(appointmentRequestConfirmationMessage.AppointmentTimestamp, localInfo.Index, appointmentRequestConfirmationMessage.Preparation, appointmentRequestConfirmationMessage.Finalization, localInfo.AppointmentId, lockContext).ConfigureAwait(false);
					}
				}
			}

			return false;
		}

		private async Task CheckAppointmentRequestUpdate(LockContext lockContext) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentRequested) {
				if(!account.AccountAppointment.LastAppointmentOperationTimeout.HasValue || account.AccountAppointment.LastAppointmentOperationTimeout.Value < DateTimeEx.CurrentTime) {
					(bool success, CheckAppointmentRequestConfirmedResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentRequestUpdateCheck(account.AccountAppointment.RequesterId.Value, lockContext).ConfigureAwait(false);

					bool retry = true;

					if(success && result != null && result.Index != 0) {
						retry = false;

						await this.ProcessAppointmentRequest(result.Appointment, result.Index, result.Preparation, result.Finalization, SafeArrayHandle.Wrap(result.SecretAppointmentId), lockContext, a => {

							a.AccountAppointment.LastAppointmentOperationTimeout = null;
						}).ConfigureAwait(false);
					}
					
					if(retry) {
						await walletProvider.ScheduleTransaction((provider, token, lc) => {
#if TESTING
							account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime.AddSeconds(15);
#else
							account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime + LastAppointmentOperationTimeoutSpan;
#endif

							return Task.FromResult(true);
						}, lockContext).ConfigureAwait(false);
					}
				}
			}
		}

		private async Task ProcessAppointmentRequest(DateTime appointmentTime, int index, int preparation, int finalization, SafeArrayHandle appointmentIdBytes, LockContext lockContext, Action<IWalletAccount> transactionExtra = null) {
			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			// ok, we are way behind. we need to get our confirmation
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			SafeArrayHandle decrypted = LargeMessageEncryptor.Decrypt(appointmentIdBytes, account.AccountAppointment.AppointmentPrivateKey);

			// lets get our secret appointment Id
			TypeSerializer.Deserialize(decrypted.Span, out Guid appointmentId);

			await walletProvider.ScheduleTransaction((provider, token, lc) => {

				account.AccountAppointment.AppointmentTime = appointmentTime;
				account.AccountAppointment.AppointmentContextTime = appointmentTime.AddMinutes(-preparation);
				account.AccountAppointment.AppointmentVerificationTime = appointmentTime.AddMinutes(finalization).AddHours(3);

				// now our secret infos
				account.AccountAppointment.AppointmentId = appointmentId;
				account.AccountAppointment.AppointmentIndex = index;

				account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentSet;
				account.AccountAppointment.LastAppointmentOperationTimeout = null;
				this.AppointmentMode = account.AccountAppointment.AppointmentStatus;

				if(transactionExtra != null) {
					transactionExtra(account);
				}

				return Task.FromResult(true);
			}, lockContext).ConfigureAwait(false);

			this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentRequestConfirmed, new CorrelationContext());
		}

		public async Task<SafeArrayHandle> GetAppointmentRequestConfirmationMessage(Guid requesterId, DateTime? appointment, LockContext lockContext) {

			IAppointmentResponseEntry messageEntry = await this.AppointmentRegistryDal.GetAppointmentRequestConfirmation(requesterId, appointment).ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		public virtual async Task<bool> RecordAppointmentVerificationConfirmationMessage(IAppointmentVerificationConfirmationMessage appointmentVerificationConfirmationMessage, IMessageEnvelope messageEnvelope, LockContext lockContext) {

			// store the file in the cache
			SafeArrayHandle envelopeBytes = messageEnvelope.DehydrateEnvelope();
			await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheAppointmentMessage(messageEnvelope.ID, envelopeBytes).ConfigureAwait(false);

			List<Guid> requesters = appointmentVerificationConfirmationMessage.Requesters.Select(r => r.RequesterId).ToList();
			await this.AppointmentRegistryDal.InsertAppointmentVerificationConfirmationMessage(requesters, messageEnvelope.ID, DateTimeEx.CurrentTime.AddHours(24)).ConfigureAwait(false);

			if(this.IsInAppointmentOperation) {

				// ok, we are in an appointment. lets do some followup
				// ok, we are in an appointment
				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentPuzzleCompleted) {
					// ok, we are way behind. we need to get our confirmation

					IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
					IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

					if(appointmentTime.HasValue && appointmentVerificationConfirmationMessage.AppointmentTimestamp == appointmentTime.Value) {
						AppointmentVerificationConfirmationMessage.AppointmentRequester resultEntry = appointmentVerificationConfirmationMessage.Requesters.SingleOrDefault(e => e.RequesterId == account.AccountAppointment.RequesterId.Value);

						if(resultEntry != null) {

							await this.ProcessAppointmentVerificationConfirmation(resultEntry.AppointmentConfirmationCode, TimeSpan.FromDays(appointmentVerificationConfirmationMessage.VerificationSpan), lockContext).ConfigureAwait(false);
						}
					}
				}
			}

			return false;
		}

		private async Task CheckAppointmentCompletedUpdate(LockContext lockContext) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(!account.AccountAppointment.LastAppointmentOperationTimeout.HasValue || account.AccountAppointment.LastAppointmentOperationTimeout.Value < DateTimeEx.CurrentTime) {
				(bool success, CheckAppointmentVerificationConfirmedResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentCompletedUpdateCheck(account.AccountAppointment.RequesterId.Value, account.AccountAppointment.AppointmentId.Value, lockContext).ConfigureAwait(false);

				bool retry = true;

				if(success && result != null) {

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

					if(appointmentTime.HasValue && result.Appointment == appointmentTime.Value) {
						retry = false;

						await this.ProcessAppointmentVerificationConfirmation(SafeArrayHandle.Wrap(result.ConfirmationCorrelationCode), TimeSpan.FromDays(result.VerificationSpan), lockContext, a => {

							if(a.AccountAppointment != null) {
								a.AccountAppointment.LastAppointmentOperationTimeout = null;
							}
						}).ConfigureAwait(false);
					}
				}

				if(retry) {
					await walletProvider.ScheduleTransaction((provider, token, lc) => {
						account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime + LastAppointmentOperationTimeoutSpan;

						return Task.FromResult(true);
					}, lockContext).ConfigureAwait(false);
				}
			}
		}

		private async Task ProcessAppointmentVerificationConfirmation(SafeArrayHandle appointmentConfirmationCodeBytes, TimeSpan verificationSpan, LockContext lockContext, Action<IWalletAccount> transactionExtra = null) {

			if(appointmentConfirmationCodeBytes == null || appointmentConfirmationCodeBytes.IsZero) {
				return;
			}

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			bool verified = false;
			long? appointmentConfirmationCode;

			try {
				(verified, appointmentConfirmationCode) = AppointmentUtils.DecryptSecretConfirmationCorrelationCode(appointmentConfirmationCodeBytes, SafeArrayHandle.Wrap(account.AccountAppointment.VerificationResponseSeed));
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Error(ex, "Failed to Decrypt Secret Confirmation Correlation Code in appointment package");

				throw;
			}

			// we received our reply!!
			await walletProvider.ScheduleTransaction((provider, token, lc) => {

				account.AccountAppointment.AppointmentVerified = verified;
				account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentCompleted;

				if(verified) {
					if(account.AccountAppointment != null) {
						account.VerificationLevel = Enums.AccountVerificationTypes.Appointment;
					} else if(account.SMSDetails != null) {
						account.VerificationLevel = Enums.AccountVerificationTypes.SMS;
					}

					account.AccountAppointment.AppointmentVerificationSpan = verificationSpan;
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + verificationSpan;
				}

				if(account.Status != Enums.PublicationStatus.Published) {
					// this is it, we are done, and we have our code
					account.AccountAppointment.AppointmentConfirmationCode = appointmentConfirmationCode;
					account.AccountAppointment.AppointmentConfirmationCodeExpiration = account.AccountAppointment.AppointmentVerificationTime.Value.AddDays(2);
				}

				this.AppointmentMode = account.AccountAppointment.AppointmentStatus;

				if(transactionExtra != null) {
					transactionExtra(account);
				}

				return Task.FromResult(true);
			}, lockContext).ConfigureAwait(false);

			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.AppointmentVerificationCompleted(verified, appointmentConfirmationCode), new CorrelationContext());
		}

		public async Task<SafeArrayHandle> GetAppointmentVerificationConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext) {

			IAppointmentVerificationConfirmationEntry messageEntry = await this.AppointmentRegistryDal.GetAppointmentVerificationConfirmations(requesterId, appointment).ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		private readonly RecursiveAsyncLock appointmentDetailsLocker = new();

		/// <summary>
		///     ensure that appointment details are loaded. otherwise do so
		/// </summary>
		/// <param name="appointment"></param>
		/// <returns></returns>
		public async Task EnsureAppointmentDetails(DateTime appointment) {

			if(!this.AppointmentDetails.TryGetValue(appointment, out var details) || details.IsLoaded || TestingUtil.Testing) {
				using(await this.appointmentDetailsLocker.LockAsync().ConfigureAwait(false)) {
					await this.UpdateAppointmentDetails(appointment).ConfigureAwait(false);

					if(this.AppointmentDetails.ContainsKey(appointment)) {
						await this.GetAppointmentKey(appointment).ConfigureAwait(false);
					}
				}
			}

		}

		public async Task UpdateAppointmentDetails(DateTime appointment) {
			IAppointmentValidatorSession session = await this.AppointmentRegistryDal.GetAppointmentValidatorSession(appointment).ConfigureAwait(false);

			if(session == null) {
				return;
			}

			AppointmentDataDetails entry = this.AppointmentDetails.GetOrAdd(appointment, new AppointmentDataDetails());

			entry.ValidatorCodes.Clear();
			entry.AssignedIndices.Clear();

			foreach(KeyValuePair<int, (int secretCodeL2, long index)[]> codeEntry in this.GetValidatorAssignedCodes(session)) {
				entry.ValidatorCodes.TryAdd(codeEntry.Key, codeEntry.Value);
			}

			foreach(int index in this.GetValidatorAssignedIndicesSet(session)) {
				entry.AssignedIndices.TryAdd(index, 0);
			}
		}

		public async Task<ConcurrentDictionary<int, (int secretCodeL2, long index)[]>> GetValidatorAssignedCodes(DateTime appointment) {

			await this.EnsureAppointmentDetails(appointment).ConfigureAwait(false);

			if(this.AppointmentDetails.TryGetValue(appointment, out var details)) {
				return details.ValidatorCodes;
			}

			return null;
		}

		public async Task<ConcurrentDictionary<int, int>> GetValidatorAssignedIndices(DateTime appointment) {
			await this.EnsureAppointmentDetails(appointment).ConfigureAwait(false);

			if(this.AppointmentDetails.TryGetValue(appointment, out var details)) {
				return details.AssignedIndices;
			}

			return null;
		}

		public async Task<bool> IsRequesterIndexAssigned(DateTime appointment, int index) {

			await this.EnsureAppointmentDetails(appointment).ConfigureAwait(false);

			if(this.AppointmentDetails.TryGetValue(appointment, out var details)) {
				return details.AssignedIndices.ContainsKey(index);
			}

			return false;
		}

	#region appointment validator

		public async Task RecordRequestSecretCode(DateTime appointment, int index, SafeArrayHandle validatorCode, DateTime? timestamp = null) {

			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry == null) {
				entry = new AppointmentRequesterResultSqlite();
				entry.Appointment = appointment;
				entry.Index = index;
				entry.ValidatorCode = validatorCode.ToExactByteArrayCopy();
				entry.RequestedCodeCompleted = timestamp.HasValue?timestamp.Value:DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.InsertAppointmentRequesterResult(entry).ConfigureAwait(false);
			}
		}

		public async Task RecordTriggerPuzzle(DateTime appointment, int index, int secretCode, DateTime? timestamp = null) {

			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry == null) {
				entry = new AppointmentRequesterResultSqlite();
				entry.Appointment = appointment;
				entry.Index = index;
				entry.SecretCode = secretCode;
				entry.TriggerCompleted = timestamp.HasValue?timestamp.Value:DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.InsertAppointmentRequesterResult(entry).ConfigureAwait(false);
			} else {
				entry.SecretCode = secretCode;
				entry.TriggerCompleted = timestamp.HasValue?timestamp.Value:DateTimeEx.CurrentTime;
				await this.AppointmentRegistryDal.UpdateAppointmentRequesterResult(entry).ConfigureAwait(false);
			}
		}

		public async Task<bool> RecordCompletePuzzle(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, DateTime? timestamp = null) {
			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry != null) {

				using SafeArrayHandle bytes = AppointmentsResultTypeSerializer.SerializeResultSet(results);
				entry.PuzzleResults = bytes.ToExactByteArrayCopy();
				entry.PuzzleCompleted = timestamp.HasValue?timestamp.Value:DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.UpdateAppointmentRequesterResult(entry).ConfigureAwait(false);

				return true;
			}

			return false;
		}

		public async Task<bool> RecordCompleteTHS(DateTime appointment, int index, SafeArrayHandle thsResults, DateTime? timestamp = null) {
			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry != null) {

				entry.THSResults = thsResults.ToExactByteArrayCopy();
				entry.THSCompleted = timestamp.HasValue?timestamp.Value:DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.UpdateAppointmentRequesterResult(entry).ConfigureAwait(false);

				return true;
			}

			return false;
		}

		public Dictionary<int, (int secretCodeL2, long index)[]> GetValidatorAssignedCodes(IAppointmentValidatorSession appointmentValidatorSession) {

			return AppointmentUtils.RehydrateAssignedSecretCodes(SafeArrayHandle.Wrap(appointmentValidatorSession.SecretCodes));
		}

		public HashSet<int> GetValidatorAssignedIndicesSet(IAppointmentValidatorSession appointmentValidatorSession) {

			return AppointmentUtils.RehydrateAssignedIndices(SafeArrayHandle.Wrap(appointmentValidatorSession.Indices));
		}

		public class AppointmentDataDetails {
			public SafeArrayHandle TriggerKey { get; set; } = SafeArrayHandle.Create();
			public bool Triggered { get; set; }

			public ConcurrentDictionary<int, (int secretCodeL2, long index)[]> ValidatorCodes = new ConcurrentDictionary<int, (int secretCodeL2, long index)[]>();
			public ConcurrentDictionary<int, int> AssignedIndices = new ConcurrentDictionary<int, int>();

			
			public void SetIsLoaded() {
				this.IsLoaded = this.AssignedIndices.Any() && ValidatorCodes.Any();
			}

			public bool IsLoaded { get; private set; }
		}

		public ConcurrentDictionary<DateTime, AppointmentDataDetails> AppointmentDetails { get; } = new();

		private readonly RecursiveAsyncLock appointmentExistsAndStartedLocker = new();

		public bool CheckAppointmentExistsAndStarted(DateTime appointment) {
			return this.AppointmentDetails.ContainsKey(appointment);
		}

		public virtual async Task<bool> AppointmentExistsAndStarted(DateTime appointment) {
			using(await this.appointmentExistsAndStartedLocker.LockAsync().ConfigureAwait(false)) {

				if(this.AppointmentDetails.ContainsKey(appointment)) {
					return true;
				}

				IAppointmentTriggerGossipMessage trigger = await this.AppointmentRegistryDal.GetAppointmentTrigger(appointment).ConfigureAwait(false);

				if(trigger != null) {
					return trigger.Appointment == appointment;
				}

				return false;
			}
		}

		private readonly RecursiveAsyncLock appointmentKeyLocker = new();

		public async Task<SafeArrayHandle> GetAppointmentKey(DateTime appointment) {

			SafeArrayHandle key = null;

			using(await this.appointmentKeyLocker.LockAsync().ConfigureAwait(false)) {

				if(this.AppointmentDetails.TryGetValue(appointment, out var details)) {
					key = details.TriggerKey;

					if(key != null && !key.IsZero) {
						return key.Clone();
					}
				}

				LockContext lockContext = null;

				// load it
				try {
					SafeArrayHandle triggerBytes = await this.GetAppointmentTriggerGossipMessage(appointment, lockContext).ConfigureAwait(false);

					if(triggerBytes != null) {
						ISignedMessageEnvelope envelope = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ISignedMessageEnvelope>(triggerBytes);

						envelope.RehydrateContents();
						envelope.Contents.Rehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

						IAppointmentTriggerMessage appointmentTriggerMessage = (IAppointmentTriggerMessage) envelope.Contents.RehydratedEvent;

						if(appointment == appointmentTriggerMessage.Appointment) {
							if(!this.AppointmentDetails.ContainsKey(appointment)) {
								this.AppointmentDetails.TryAdd(appointment, new AppointmentDataDetails());

								// gotta try to load the context
								await this.CheckAppointmentContextUpdate(appointment, lockContext).ConfigureAwait(false);
							}

							if(appointmentTriggerMessage.Key != null && !appointmentTriggerMessage.Key.IsZero) {
								this.AppointmentDetails[appointment].TriggerKey = appointmentTriggerMessage.Key.Clone();

								return appointmentTriggerMessage.Key.Clone();
							}
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to load trigger from gossip message");
				}

				// let's do a check
				return await this.CheckAppointmentTriggerUpdate(appointment, lockContext).ConfigureAwait(false);
			}
		}

	#endregion

		protected Task ExtractValidatorContextMessage(IAppointmentContextMessage appointmentContextMessage, LockContext lockContext) {

			return this.ExtractValidatorContext(appointmentContextMessage.Appointment, appointmentContextMessage.PuzzleWindow, appointmentContextMessage.ValidatorWindow, appointmentContextMessage.Validators, lockContext);
		}

		protected virtual async Task ExtractValidatorContext(DateTime appointment, int appointmentWindow, int validatorWindow, Dictionary<Guid, SafeArrayHandle> validators, LockContext lockContext) {
			IAppointmentValidatorSession entry = await this.AppointmentRegistryDal.GetAppointmentValidatorSession(appointment).ConfigureAwait(false);

			if(entry == null) {
				entry = new AppointmentValidatorSessionSqlite();

				entry.Appointment = appointment;
				entry.Window = appointmentWindow;
				entry.RequesterCount = 0;

				// establish a random dispatch time within the window in hours. we will wait a minimum of 20 minutes
				entry.Appointment.AddMinutes(GlobalRandom.GetNext(10, Math.Max(validatorWindow - 30, 30)));

#if TESTING
				entry.Dispatch = entry.Appointment;
#else
				entry.Dispatch = AppointmentUtils.ComputeDispatchDelay(appointment, validatorWindow);
#endif

				IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

				entry.ValidatorHash = AppointmentUtils.GetValidatorIdHash(appointment, this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId, account.Stride);

				await this.AppointmentRegistryDal.InsertAppointmentValidatorSession(entry).ConfigureAwait(false);
			}

			if(validators.ContainsKey(entry.ValidatorHash)) {

				// ok, we have some assigned requesters
				Dictionary<int, (int secretCodeL2, long index)[]> assignedCodes = this.GetValidatorAssignedCodes(entry);
				HashSet<int> indices = this.GetValidatorAssignedIndicesSet(entry);

				SafeArrayHandle encryptedSecretCodeBytes = validators[entry.ValidatorHash];

				AppointmentContextMessage.ValidatorSessionDetails validatorSessionDetails = new();

				IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

				using(INTRUPrimeWalletKey key = await walletProvider.LoadKey<INTRUPrimeWalletKey>(GlobalsService.VALIDATOR_SECRET_KEY_NAME, lockContext).ConfigureAwait(false)) {

					using SafeArrayHandle decryptedBytes = AppointmentUtils.DecryptValidatorMessage(appointment, encryptedSecretCodeBytes, account.Stride, key.PublicKey);
					validatorSessionDetails.Rehydrate(decryptedBytes);
				}

				entry.RequesterCount = validatorSessionDetails.AssignedIndices.Count;
				await this.AppointmentRegistryDal.UpdateAppointmentValidatorSession(entry).ConfigureAwait(false);

				for(int i = 0; i < validatorSessionDetails.AssignedIndices.Count; i++) {
					(int secretCode, int secretCodeL2) entryx = validatorSessionDetails.SecretCodes[i];
					long index = validatorSessionDetails.AssignedIndices[i];

					if(!assignedCodes.ContainsKey(entryx.secretCode)) {
						(int secretCodeL2, long index)[] array = {(entryx.secretCodeL2, index)};
						assignedCodes.Add(entryx.secretCode, array);
					} else {
						// this should be very rare, but still possible! it has happened!
						(int secretCodeL2, long index)[] array = assignedCodes[entryx.secretCode];
						List<(int secretCodeL2, long index)> list = array.ToList();
						list.Add((entryx.secretCodeL2, index));
						assignedCodes[entryx.secretCode] = list.ToArray();
					}
				}

				foreach(int index in validatorSessionDetails.AssignedIndices) {
					if(!indices.Contains(index)) {
						indices.Add(index);
					}
				}

				using SafeArrayHandle secretCodeBytes = AppointmentUtils.DehydrateAssignedSecretCodes(assignedCodes);
				entry.SecretCodes = secretCodeBytes.ToExactByteArrayCopy();

				using SafeArrayHandle indicesBytes = AppointmentUtils.DehydrateAssignedIndices(indices.ToList());
				entry.Indices = indicesBytes.ToExactByteArrayCopy();

				await this.AppointmentRegistryDal.UpdateAppointmentValidatorSession(entry).ConfigureAwait(false);

				var details = this.AppointmentDetails.GetOrAdd(appointment, new AppointmentDataDetails());

				foreach(KeyValuePair<int, (int secretCodeL2, long index)[]> codeEntry in assignedCodes) {

					details.ValidatorCodes.TryAdd(codeEntry.Key, codeEntry.Value);
				}

				foreach(int index in indices) {

					details.AssignedIndices.TryAdd(index, 0);
				}

				details.SetIsLoaded();
			}
		}

		public virtual async Task<bool> RecordAppointmentContextGossipMessage(IAppointmentContextMessage appointmentContextMessage, IMessageEnvelope messageEnvelope, LockContext lockContext) {

			SafeArrayHandle envelopeBytes = messageEnvelope.DehydrateEnvelope();
			await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheAppointmentMessage(messageEnvelope.ID, envelopeBytes).ConfigureAwait(false);

			(int start, int end) range = appointmentContextMessage.ComputeRange();
			await this.AppointmentRegistryDal.InsertAppointmentContextGossipMessage(messageEnvelope.ID, appointmentContextMessage.Appointment, range.start, range.end).ConfigureAwait(false);

			if(this.IsValidator) {
				// ok, we are a validator, lets determine our validator hash for this appointment

				await this.ExtractValidatorContextMessage(appointmentContextMessage, lockContext).ConfigureAwait(false);
			}

			// if we are in appointment mode, lets check for our data and perform the workflow
			if(this.IsInAppointmentOperation) {
				IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

				// first, ensure we are up to date
				await this.CheckOperatingMode(lockContext).ConfigureAwait(false);

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentSet) {
					// lets see if we find our data in this context
					IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

					int adjustedIndex = account.AccountAppointment.AppointmentIndex.Value - range.start;

					SafeArrayHandle secretPuzzleBytes = null;

					if(appointmentContextMessage.Applicants.Count > adjustedIndex) {
						secretPuzzleBytes = appointmentContextMessage.Applicants[adjustedIndex];
					}

					if(appointmentTime.HasValue && appointmentTime.Value == appointmentContextMessage.Appointment && !appointmentContextMessage.SecretPuzzles.IsZero && secretPuzzleBytes != null && !secretPuzzleBytes.IsZero && !account.AccountAppointment.AppointmentContextDetailsCached) {
						// ok, this is ours. lets see if we are in the right index
						if(account.AccountAppointment.AppointmentIndex.Value >= range.start && account.AccountAppointment.AppointmentIndex.Value <= range.end) {

							using SafeArrayHandle thsRuleSetBytes = appointmentContextMessage.THSRuleSet.Dehydrate();
							await this.ProcessAppointmentContext(appointmentContextMessage.PuzzleWindow, appointmentContextMessage.PuzzleEngineVersion, thsRuleSetBytes.ToExactByteArrayCopy(), appointmentContextMessage.SecretPuzzles.ToExactByteArrayCopy(), secretPuzzleBytes.ToExactByteArrayCopy(), lockContext).ConfigureAwait(false);
						}
					}
				}
			}

			return false;
		}

		protected virtual async Task CheckAppointmentContextUpdate(DateTime appointment, LockContext lockContext) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			(bool success, CheckAppointmentContextResult2 result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentContextUpdateCheck(account.AccountAppointment.RequesterId.Value, account.AccountAppointment.AppointmentIndex.Value, appointment, lockContext).ConfigureAwait(false);

			bool retry = true;

			if(success) {

				DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

				if(result != null && result.PuzzleBytes != null && result.PuzzleBytes.Length > 0 && result.SecretPackageBytes != null && result.SecretPackageBytes.Length > 0 && appointmentTime.HasValue && result.Appointment == appointmentTime.Value) {
					retry = false;

					await this.ProcessAppointmentContext(result.Window, result.EngineVersion, Convert.FromBase64String(result.POwRuleSet), Convert.FromBase64String(result.PuzzleBytes), Convert.FromBase64String(result.SecretPackageBytes), lockContext, a => {

						a.AccountAppointment.LastAppointmentOperationTimeout = null;
					}).ConfigureAwait(false);
				}
			}

			if(retry) {
				await walletProvider.ScheduleTransaction((provider, token, lc) => {
					account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime + LastAppointmentOperationTimeoutSpan;

					return Task.FromResult(true);
				}, lockContext).ConfigureAwait(false);
			}
		}

		private async Task<bool> ProcessAppointmentContext(int window, int engineVersion, byte[] thsRuleSet, byte[] secretPuzzles, byte[] secretPackageBytes, LockContext lockContext, Action<IWalletAccount> transactionExtra = null) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			// ok, this is our message!  lets cache everything
			DistilledAppointmentContext distilledAppointmentContext = new();

			distilledAppointmentContext.Window = window;
			distilledAppointmentContext.EngineVersion = engineVersion;
			distilledAppointmentContext.THSRuleSet = thsRuleSet;

			if(distilledAppointmentContext.EngineVersion > GlobalsService.MAXIMUM_PUZZLE_ENGINE_VERSION || distilledAppointmentContext.EngineVersion < GlobalsService.MINIMUM_PUZZLE_ENGINE_VERSION) {

				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.InvalidPuzzleEngineVersion(distilledAppointmentContext.EngineVersion, GlobalsService.MINIMUM_PUZZLE_ENGINE_VERSION, GlobalsService.MAXIMUM_PUZZLE_ENGINE_VERSION), new CorrelationContext());

				return false;
			}

			distilledAppointmentContext.PuzzleBytes = secretPuzzles;
			distilledAppointmentContext.PackageBytes = secretPackageBytes;

			await walletProvider.WriteDistilledAppointmentContextFile(distilledAppointmentContext).ConfigureAwait(false);

			await walletProvider.ScheduleTransaction(async (provider, token, lc) => {

				account.AccountAppointment.AppointmentWindow = window;
				account.AccountAppointment.AppointmentContextDetailsCached = true;
				account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentContextCached;
				this.AppointmentMode = account.AccountAppointment.AppointmentStatus;

				if(transactionExtra != null) {
					transactionExtra(account);
				}

				return true;
			}, lockContext).ConfigureAwait(false);

			this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentContextCached, new CorrelationContext());

			return true;
		}

		public virtual async Task<SafeArrayHandle> GetAppointmentContextGossipMessage(int requesterIndex, DateTime appointment, LockContext lockContext) {

			IAppointmentContextGossipMessage messageEntry = await this.AppointmentRegistryDal.GetAppointmentContext(requesterIndex, appointment).ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		public void SetPuzzleAnswers(List<int> answers) {
			if(this.IsInAppointmentOperation && this.AppointmentMode == Enums.AppointmentStatus.AppointmentContextCached) {
				if(this.appointmentWorkflow != null && !this.appointmentWorkflow.IsCompleted) {
					this.appointmentWorkflow.ProvidePuzzleAnswers(answers);
				}
			}
		}

		public virtual async Task<bool> RecordAppointmentTriggerGossipMessage(IAppointmentTriggerMessage appointmentTriggerMessage, IMessageEnvelope messageEnvelope, LockContext lockContext) {
			SafeArrayHandle envelopeBytes = messageEnvelope.DehydrateEnvelope();
			await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheAppointmentMessage(messageEnvelope.ID, envelopeBytes).ConfigureAwait(false);

			await this.AppointmentRegistryDal.InsertAppointmentTriggerGossipMessage(messageEnvelope.ID, appointmentTriggerMessage.Appointment).ConfigureAwait(false);

			if(!this.AppointmentDetails.ContainsKey(appointmentTriggerMessage.Appointment)) {
				this.AppointmentDetails.TryAdd(appointmentTriggerMessage.Appointment, new AppointmentDataDetails());

				// gotta try to load the context
				await this.CheckAppointmentContextUpdate(appointmentTriggerMessage.Appointment, lockContext).ConfigureAwait(false);
			}

			this.AppointmentDetails[appointmentTriggerMessage.Appointment].TriggerKey = appointmentTriggerMessage.Key.Branch();

			return false;
		}

		public virtual async Task<SafeArrayHandle> CheckAppointmentTriggerUpdate(DateTime appointment, LockContext lockContext) {

			if(this.AppointmentDetails.ContainsKey(appointment)) {
				SafeArrayHandle key = this.AppointmentDetails[appointment].TriggerKey;

				if(key != null && !key.IsZero) {
					return key.Clone();
				}
			}

			(bool success, string triggerKey) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentTriggerUpdateCheck(appointment, lockContext).ConfigureAwait(false);

			if(success && !string.IsNullOrWhiteSpace(triggerKey)) {

				if(!this.AppointmentDetails.ContainsKey(appointment)) {
					this.AppointmentDetails.TryAdd(appointment, new AppointmentDataDetails());

					// gotta try to load the context
					await this.CheckAppointmentContextUpdate(appointment, lockContext).ConfigureAwait(false);
				}

				this.AppointmentDetails[appointment].TriggerKey = SafeArrayHandle.FromBase64(triggerKey);

				return this.AppointmentDetails[appointment].TriggerKey.Clone();
			}

			return null;
		}

		public virtual async Task<SafeArrayHandle> GetAppointmentTriggerGossipMessage(DateTime appointment, LockContext lockContext) {

			IAppointmentTriggerGossipMessage messageEntry = await this.AppointmentRegistryDal.GetAppointmentTrigger(appointment).ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		public virtual Task<List<(DateTime appointment, TimeSpan window, int requesterCount)>> GetAppointmentWindows() {
			return this.AppointmentRegistryDal.GetAppointments();
		}

		public virtual async Task UpdateOperatingMode(LockContext lockContext) {
			IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			this.UpdateOperatingMode(account, lockContext);
		}

		public virtual void UpdateOperatingMode(IWalletAccount account, LockContext lockContext) {

			if(account.AccountAppointment != null) {
				this.OperatingMode = Enums.OperationStatus.Appointment;
				this.AppointmentMode = account.AccountAppointment.AppointmentStatus;
			} else {
				this.OperatingMode = Enums.OperationStatus.None;
				this.AppointmentMode = Enums.AppointmentStatus.None;
			}
		}

		protected ConcurrentDictionary<DateTime, DateTime> appointmentDispatches = new();

		protected virtual async Task<long> GetValidatorKeyHash(byte ordinal, DateTime appointmentTime, LockContext lockContext) {
			using(IWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(ordinal, lockContext).ConfigureAwait(false)) {

				return AppointmentUtils.GetValidatorKeyHash(key.PublicKey, appointmentTime);
			}
		}

		public async Task<bool> ClearAppointment(LockContext lockContext, string accountCode = null, bool force = false) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			IWalletAccount account = null;

			if(string.IsNullOrWhiteSpace(accountCode)) {
				account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			} else {
				account = await walletProvider.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);
			}

			if(account == null || account.AccountAppointment == null) {
				return false;
			}

			bool cleared = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.ClearAppointment(account.AccountCode, lockContext, force).ConfigureAwait(false);

			if(cleared) {
				this.AppointmentMode = Enums.AppointmentStatus.None;

				await this.CleanAppointmentsRegistry().ConfigureAwait(false);
				this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AppointmentReset, new CorrelationContext());
				this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, new CorrelationContext());
			}

			return cleared;
		}

		public virtual async Task CheckOperatingMode(LockContext lockContext) {

			if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded || !await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.HasAccount(lockContext).ConfigureAwait(false)) {
				return;
			}

			if(this.OperatingMode == Enums.OperationStatus.Unknown) {
				// ok, first step is to update our operating mode
				await this.UpdateOperatingMode(lockContext).ConfigureAwait(false);
			}

			if(this.IsValidator) {

				DateTime? appointment = null;
				
				if(this.ShouldAct(ref this.checkAppointmentsContexts)) {

					await this.QueryWebAppointments(lockContext).ConfigureAwait(false);
#if TESTING
					this.checkAppointmentsContexts = DateTimeEx.CurrentTime.AddSeconds(15);
#else
					this.checkAppointmentsContexts = DateTimeEx.CurrentTime.AddHours(6);
#endif

					appointment = await this.AppointmentRegistryDal.GetInRangeAppointments().ConfigureAwait(false);
				}
				

				if(appointment.HasValue) {
					await this.EnsureAppointmentDetails(appointment.Value).ConfigureAwait(false);
				}

				// let's update our dispatches
				await this.UpdateAppointmentDispatches().ConfigureAwait(false);

#if TESTING
				if(this.appointmentDispatches.Values.Any()) {
#else
				if(this.appointmentDispatches.Values.Any(a => a < DateTimeEx.CurrentTime)) {
#endif

					List<DateTime> sessions = await this.appointmentRegistryDal.GetReadyAppointmentSessions().ConfigureAwait(false);

					if(!sessions.Any()) {
						return;
					}

					foreach(var sessionAppointment in sessions) {

						if(DateTimeEx.CurrentTime >= sessionAppointment-AppointmentsValidatorProvider.AppointmentWindowHead && DateTimeEx.CurrentTime <= sessionAppointment-AppointmentsValidatorProvider.AppointmentWindowTail) {
							// we are IN the appointment, dont do anything
							continue;
						}
						bool ready = false;

						lock(this.sendVerificationResultsWorkflowLocker) {
							ready = !this.sendVerificationResultsWorkflows.ContainsKey(sessionAppointment) || this.sendVerificationResultsWorkflows[sessionAppointment].IsCompleted;
						}

						if(ready) {

							int total = await appointmentRegistryDal.GetReadyAppointmentRequesterResultCount(sessionAppointment).ConfigureAwait(false);

							if(total == 0) {
								continue;
							}

							ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> sendVerificationResultsWorkflow = null;

							lock(this.sendVerificationResultsWorkflowLocker) {
								sendVerificationResultsWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateSendAppointmentVerificationResultsMessageWorkflow(sessionAppointment, new CorrelationContext());
								this.sendVerificationResultsWorkflows.TryAdd(sessionAppointment, sendVerificationResultsWorkflow);
							}

							sendVerificationResultsWorkflow.Completed2 += (b, workflow) => {

								try {

								} finally {

									lock(this.sendVerificationResultsWorkflowLocker) {
										this.sendVerificationResultsWorkflows.TryRemove(sessionAppointment, out var _);
									}
								}

								return Task.CompletedTask;
							};

							this.CentralCoordinator.PostWorkflow(sendVerificationResultsWorkflow);
						}

						try {
							await this.CleanAppointmentsRegistry().ConfigureAwait(false);

						} catch(Exception ex) {
							this.CentralCoordinator.Log.Error("Failed to clear obsolete appointments.");
						}

						// clear obsoletes
						foreach(KeyValuePair<DateTime, DateTime> entry in this.appointmentDispatches.Where(k => k.Key.AddMinutes(5) < DateTimeEx.CurrentTime)) {
							this.appointmentDispatches.Remove(entry.Key, out var _);
						}
					}
				}

			}

			if(this.IsInAppointmentOperation) {

				IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

				this.UpdateOperatingMode(account, lockContext);

				if(!this.IsInAppointmentOperation) {
					return;
				}

				// first, check if an appointment has expired under various scenarios

				// context received but puzzle never completed in time.
				bool appointmentExpired = account.AccountAppointment.AppointmentStatus <= Enums.AppointmentStatus.AppointmentContextCached && account.AccountAppointment.AppointmentTime.HasValue && account.AccountAppointment.AppointmentTime.Value.AddHours(1) < DateTimeEx.CurrentTime;

				// absolute appointment time long passed X days
				bool appointmentExpired2 = appointmentExpired || account.AccountAppointment.AppointmentTime.HasValue && account.AccountAppointment.AppointmentTime.Value.AddDays(3) < DateTimeEx.CurrentTime;

				// we sent a request, and never received a response
				bool appointmentExpired3 = appointmentExpired2 || !account.AccountAppointment.AppointmentTime.HasValue && account.AccountAppointment.AppointmentStatus <= Enums.AppointmentStatus.AppointmentRequested && account.AccountAppointment.AppointmentRequestTimeStamp.HasValue && account.AccountAppointment.AppointmentRequestTimeStamp.Value.AddDays(1) < DateTimeEx.CurrentTime;

				// puzzle completed, but we never received any verification results
				bool appointmentExpired4 = appointmentExpired3 || account.AccountAppointment.AppointmentStatus >= Enums.AppointmentStatus.AppointmentPuzzleCompleted && AppointmentUtils.AppointmentVerificationExpired(account.AccountAppointment.AppointmentVerificationTime);

				if(appointmentExpired || appointmentExpired2 || appointmentExpired3 || appointmentExpired4) {
					// ok, we are way behind. we need to get our confirmation

					await this.ClearAppointment(lockContext).ConfigureAwait(false);

					return;
				}

				// ok, we are in an appointment. lets do some followup
				// ok, we are in an appointment
				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentRequested) {
					// ok, we are way behind. we need to get our confirmation

					await this.CheckAppointmentRequestUpdate(lockContext).ConfigureAwait(false);
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentSet) {
					// ok, we are way behind. we need to get our confirmation

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;
					DateTime? appointmentContextTime = account.AccountAppointment.AppointmentContextTime;

					if(appointmentTime.HasValue && appointmentContextTime.HasValue && appointmentContextTime.Value < DateTimeEx.CurrentTime) {
						// ok, we can try to get the contexts info we need
						await this.CheckAppointmentContextUpdate(appointmentTime.Value, lockContext).ConfigureAwait(false);
					}
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentContextCached) {
					// ok, we are ready to go. lets make sure we dont miss the appointment!

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;
					DateTime? appointmentExpirationTime = account.AccountAppointment.AppointmentExpirationTime;

					if(appointmentTime.HasValue && appointmentTime.Value + AppointmentsValidatorProvider.AppointmentWindowHead < DateTimeEx.CurrentTime && appointmentTime.Value.AddMinutes(30) > DateTimeEx.CurrentTime && (!appointmentExpirationTime.HasValue || appointmentExpirationTime.Value >= DateTimeEx.CurrentTime)) {
						// this is our appointment!!  it begins! lets begin the workflow

						bool ready = false;

						lock(this.appointmentWorkflowLocker) {
							ready = this.appointmentWorkflow == null || this.appointmentWorkflow.IsCompleted;
						}

						if(ready) {
							lock(this.appointmentWorkflowLocker) {
								this.appointmentWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateAppointmentPuzzleExecutionWorkflow(new CorrelationContext());
							}

							this.appointmentWorkflow.PuzzleBeginEvent += this.PuzzleBeginEvent;

							this.appointmentWorkflow.Completed2 += async (b, workflow) => {

								try {

									this.AppointmentMode = account.AccountAppointment.AppointmentStatus;
									this.TriggerAppointmentPuzzleCompleted();
								} finally {
									this.appointmentWorkflow.PuzzleBeginEvent -= this.PuzzleBeginEvent;

									lock(this.appointmentWorkflowLocker) {
										this.appointmentWorkflow = null;
									}
								}
							};

							this.CentralCoordinator.PostWorkflow(this.appointmentWorkflow);
						}
					}
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentPuzzleCompleted) {
					// ok, we are done, we check for our results

					await this.CheckAppointmentCompletedUpdate(lockContext).ConfigureAwait(false);
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentCompleted) {
					// nothing to do here really?

					if(account.AccountAppointment.AppointmentConfirmationCodeExpiration < DateTimeEx.CurrentTime) {
						await walletProvider.ScheduleTransaction(async (provider, token, lc) => {

							// we are done
							account.AccountAppointment = null;
							this.AppointmentMode = Enums.AppointmentStatus.None;

							return true;
						}, lockContext).ConfigureAwait(false);
					}

				}
			}
		}

		/// <summary>
		/// here we perform the puzzle verification of a list of requesters
		/// </summary>
		/// <param name="appointmentTime"></param>
		/// <param name="entries"></param>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		public async Task<Dictionary<int, bool>> PrepareAppointmentRequesterResult(DateTime appointmentTime, List<IAppointmentRequesterResult> entries, LockContext lockContext) {
			IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			// we only send them when we can
			if(!this.appointmentDispatches.ContainsKey(appointmentTime) || this.appointmentDispatches[appointmentTime] > DateTimeEx.CurrentTime) {
				return new Dictionary<int, bool>();
			}

			this.CentralCoordinator.Log.Verbose($"Publishing puzzle {entries.Count()} results for appointment {appointmentTime}");

			SafeArrayHandle appointmentKey = this.AppointmentDetails[appointmentTime].TriggerKey;

			if(appointmentKey == null || appointmentKey.IsZero) {

				await this.CheckAppointmentTriggerUpdate(appointmentTime, lockContext).ConfigureAwait(false);

				appointmentKey = this.AppointmentDetails[appointmentTime].TriggerKey;

				if(appointmentKey == null || appointmentKey.IsZero) {

					return new Dictionary<int, bool>();
				}
			}

			// lets verify our secretCodes Level2
			ConcurrentDictionary<int, (int secretCodeL2, long index)[]> codes = await this.GetValidatorAssignedCodes(appointmentTime).ConfigureAwait(false);

			byte ordinal = AppointmentUtils.GetKeyOrdinal2(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId.ToLongRepresentation());

			long keyHash2 = await this.GetValidatorKeyHash(ordinal, appointmentTime, lockContext).ConfigureAwait(false);

			ConcurrentDictionary<int, bool> verificationResults = new ConcurrentDictionary<int, bool>();

			await ParallelAsync.ForEach(entries, async data => {
				IAppointmentRequesterResult entry = data.entry;

				int codeHash = 0;
				int secretCodeL2 = 0;

				using SafeArrayHandle results = SafeArrayHandle.Wrap(entry.PuzzleResults);
				Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> resultSet = null;

				if(results != null && !results.IsZero) {
					try {
						resultSet = AppointmentsResultTypeSerializer.DeserializeResultSet(results);

						KeyValuePair<Enums.AppointmentsResultTypes, SafeArrayHandle> secretCodeL2Bytes = resultSet.SingleOrDefault(e => e.Key == Enums.AppointmentsResultTypes.SecretCodeL2);
						secretCodeL2 = AppointmentsResultTypeSerializer.DeserializeSecretCodeL2(secretCodeL2Bytes.Value);

						codeHash = AppointmentUtils.GenerateValidatorSecretCodeHash(entry.SecretCode, appointmentTime, appointmentKey, this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId, keyHash2, account.Stride);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, "Failed to process results");
					}
				}

				bool invalid = codeHash == 0 || secretCodeL2 == 0;

				if(!invalid) {
					invalid = !codes.ContainsKey(codeHash);

					if(!invalid) {
						invalid = !codes[codeHash].Any(e => e.index == entry.Index);

						if(!invalid) {
							invalid = codes[codeHash].Single(e => e.index == entry.Index).secretCodeL2 != secretCodeL2;
						}
					}
				}

				if(invalid) {
					// ok, we have a failure to offer the right code L2
					entry.Valid = false;

					verificationResults.TryAdd(entry.Index, false);

					return;
				}

				// ok, now we can check the THS

				bool thsResult = false;

				if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableAppointmentPuzzleTHS) {
					try {
						SafeArrayHandle thsResults = SafeArrayHandle.Wrap(entry.THSResults);

						KeyValuePair<Enums.AppointmentsResultTypes, SafeArrayHandle> puzzleDetails = resultSet.SingleOrDefault(e => e.Key == Enums.AppointmentsResultTypes.Puzzle);

						List<int> puzzleAnswers = AppointmentsResultTypeSerializer.DeserializePuzzleResult(puzzleDetails.Value);

						if(puzzleAnswers.Any() && puzzleAnswers.All(e => e != 0)) {
							THSSolutionSet thsSolutionSet = AppointmentsResultTypeSerializer.DeserializeTHS(thsResults);

							thsResult = await AppointmentUtils.VerifyPuzzleTHS(appointmentKey, puzzleAnswers, thsSolutionSet).ConfigureAwait(false);
						}
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, "Failed to ths results results");
					}
				} else {
					// we dont verify
					thsResult = true;
				}

				verificationResults.TryAdd(entry.Index, thsResult);
			}).ConfigureAwait(false);

			return verificationResults.ToDictionary(e => e.Key, e => e.Value);
		}

		protected virtual async Task<bool> UpdateAppointmentDispatches() {
			bool processed = false;

			if(this.ShouldAct(ref this.checkAppointmentsDispatches)) {
				processed = true;
				List<(DateTime appointment, TimeSpan window, int requesterCount)> appointments = await this.AppointmentRegistryDal.GetAppointments().ConfigureAwait(false);

				foreach(DateTime app in appointments.Select(a => a.appointment)) {
					if(!this.appointmentDispatches.ContainsKey(app)) {
						IAppointmentValidatorSession session = await this.AppointmentRegistryDal.GetAppointmentValidatorSession(app).ConfigureAwait(false);

						if(session != null) {
							this.appointmentDispatches.TryAdd(app, session.Dispatch.ToUniversalTime());
						}
					}
				}

#if TESTING
				this.checkAppointmentsDispatches = DateTimeEx.CurrentTime.AddMinutes(1);
#else
				this.checkAppointmentsDispatches = DateTimeEx.CurrentTime.AddMinutes(30);
#endif

			}

			return processed;
		}

		protected virtual async Task QueryWebAppointments(LockContext lockContext) {
			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			(bool success, CheckAppointmentsResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.QueryAvailableAppointments(lockContext).ConfigureAwait(false);

			if(success && result != null && result.Appointments != null && result.Appointments.Any()) {

				List<DateTime> registryAppointments = (await this.AppointmentRegistryDal.GetAppointments().ConfigureAwait(false)).Select(a => a.appointment.ToUniversalTime()).Where(a => a > DateTimeEx.CurrentTime).ToList();
				List<DateTime> remoteAppointmentsList = result.Appointments.Select(a => a.ToUniversalTime()).Where(a => a > DateTimeEx.CurrentTime).ToList();

				// determine which ones we are missing
				List<DateTime> containedAppointments = remoteAppointmentsList.Where(a => registryAppointments.Contains(a)).ToList();
				List<DateTime> missingAppointments = remoteAppointmentsList.Where(a => !registryAppointments.Contains(a)).ToList();

				// now the ones we have but dont have the context
				List<DateTime> missingContextsAppointments = missingAppointments.ToList();

				foreach(DateTime appointmentTime in containedAppointments) {

					// if we dont ahve the context, we will need to query it
					if(await this.AppointmentRegistryDal.GetAppointmentValidatorSession(appointmentTime).ConfigureAwait(false) == null) {
						missingContextsAppointments.Add(appointmentTime);
					}
				}

				missingContextsAppointments = missingContextsAppointments.Distinct().ToList();

				if(!missingContextsAppointments.Any()) {
					return;
				}

				AccountId miningAccountId = this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId;

				Dictionary<DateTime, Guid> hashes = missingContextsAppointments.ToDictionary(a => a, a => AppointmentUtils.GetValidatorIdHash(a, miningAccountId, account.Stride));

				List<Guid> hashesList = missingContextsAppointments.Where(a => hashes.ContainsKey(a)).Select(a => hashes[a]).ToList();

				if(!hashesList.Any()) {
					return;
				}
				(bool success2, QueryValidatorAppointmentSessionsResult result2) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.QueryValidatorAppointmentSessions(miningAccountId, missingContextsAppointments, hashesList, lockContext).ConfigureAwait(false);

				if(success2 && result2 != null) {
					foreach(QueryValidatorAppointmentSessionsResult.ValidatorAppointmentSession session in result2.Sessions) {

						foreach(QueryValidatorAppointmentSessionsResult.ValidatorAppointmentSession.ValidatorSessionSlice slice in session.Slices) {

							Dictionary<Guid, SafeArrayHandle> validators = new();

							if(hashes.ContainsKey(session.Appointment)) {
								validators.Add(hashes[session.Appointment], SafeArrayHandle.Wrap(slice.EncryptedSecretCodeBytes));
								await this.ExtractValidatorContext(session.Appointment, session.Window, session.ValidatorWindow, validators, lockContext).ConfigureAwait(false);
							}
						}

						// lets add the appointment window to our registrations
						this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AddAppointmentWindow(session.Appointment, TimeSpan.FromSeconds(session.ValidatorWindow), 0);
					}
				}
			}
		}

		private void TriggerAppointmentPuzzleCompleted() {
			if(this.AppointmentPuzzleCompletedEvent != null) {
				this.AppointmentPuzzleCompletedEvent();
			}
		}

		protected bool IsInAppointmentOperation => this.OperatingMode == Enums.OperationStatus.Appointment;

	#endregion

	}
}