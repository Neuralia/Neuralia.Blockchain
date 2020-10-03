using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IAppointmentsProvider : IChainProvider {

		Enums.OperationStatus OperatingMode { get; set; }
		Enums.AppointmentStatus AppointmentMode { get; }
		bool IsValidator { get; }
		bool IsValidatorWindow { get; }
		bool IsValidatorWindowProximity { get; }
		IAppointmentRegistryDal AppointmentRegistryDal { get; }

		void                                                SetPuzzleAnswers(List<int> answers);
		Task<List<(DateTime appointment, TimeSpan window)>> GetAppointmentWindows();
		Task<ConcurrentDictionary<int, int>>                GetValidatorAssignedCodes(DateTime appointment);
		bool                                                IsRequesterIndexAssigned(DateTime appointment, int index);
		Task                                                RecordRequestSecretCode(DateTime appointment, int index, SafeArrayHandle validatorCode);
		Task                                                RecordTriggerPuzzle(DateTime appointment, int index, int secretCode);
		Task<bool>                                          RecordCompletePuzzle(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results);

		Task<bool> AppointmentExistsAndStarted(DateTime appointment);
		Task<SafeArrayHandle> GetAppointmentKey(DateTime appointment);

		Task HandleBlockchainMessage(IBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext);
		Task CleanAppointmentsRegistry();
		Task CheckOperatingMode(LockContext lockContext);
		Task UpdateOperatingMode(LockContext lockContext);
		Task<SafeArrayHandle> CheckAppointmentTriggerUpdate(DateTime appointment, LockContext lockContext);

		Task<SafeArrayHandle>                                         GetAppointmentRequestConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext);
		Task<SafeArrayHandle>                                         GetAppointmentVerificationConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext);
		Task<SafeArrayHandle>                                         GetAppointmentContextGossipMessage(int requesterIndex, DateTime appointment, LockContext lockContext);
		Task<SafeArrayHandle>                                         GetAppointmentTriggerGossipMessage(LockContext lockContext);
		Task                                                          UpdateAppointmentDetails(DateTime appointment);
		event Action                                                  AppointmentPuzzleCompletedEvent;
		event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;
	}

	public interface IAppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAppointmentsProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class AppointmentsProvider {
	}

	public abstract class AppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IAppointmentsProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly object appointmentWorkflowLocker = new object();

		private readonly object locker = new object();

		private IAppointmentRegistryDal appointmentRegistryDal;
		private IPuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> appointmentWorkflow;
		protected DateTime? checkAppointmentsContexts;
		protected DateTime? checkAppointmentsDispatches;
		
		public AppointmentsProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;

		}

		public virtual IAppointmentRegistryDal AppointmentRegistryDal {
			get {
				lock(this.locker) {
					if(this.appointmentRegistryDal == null) {
						this.appointmentRegistryDal = this.CentralCoordinator.ChainDalCreationFactory.CreateAppointmentRegistryDal<IAppointmentRegistryDal>(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), this.CentralCoordinator.BlockchainServiceSet, this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.appointmentRegistryDal;
			}
		}

		protected CENTRAL_COORDINATOR CentralCoordinator { get; }
		public event Action AppointmentPuzzleCompletedEvent;
		public event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;

		public Enums.OperationStatus OperatingMode { get; set; } = Enums.OperationStatus.Unknown;
		public Enums.AppointmentStatus AppointmentMode { get; protected set; } = Enums.AppointmentStatus.None;
		public bool IsValidator => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidator;

		public bool IsValidatorWindow => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidatorWindow;
		public bool IsValidatorWindowProximity => this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidatorWindowProximity;

		public virtual Task HandleBlockchainMessage(IBlockchainMessage message, IMessageEnvelope messageEnvelope, LockContext lockContext) {

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
				this.AppointmentDetails.Remove(entry, out var _);
			}

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
				if(!account.AccountAppointment.LastAppointmentOperationTimeout.HasValue || (account.AccountAppointment.LastAppointmentOperationTimeout.Value < DateTimeEx.CurrentTime)) {
					(bool success, CheckAppointmentRequestConfirmedResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentRequestUpdateCheck(account.AccountAppointment.RequesterId.Value, lockContext).ConfigureAwait(false);

					var retry = true;

					if(success) {

						if((result != null) && (result.Index != 0)) {
							retry = false;

							await this.ProcessAppointmentRequest(result.Appointment, result.Index, result.Preparation, result.Finalization, SafeArrayHandle.Wrap(result.SecretAppointmentId), lockContext, a => {

								a.AccountAppointment.LastAppointmentOperationTimeout = null;
							}).ConfigureAwait(false);
						}
					}

					if(retry) {
						await walletProvider.ScheduleTransaction((provider, token, lc) => {
#if TESTING
							account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime.AddSeconds(15);
#else
							account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime.AddMinutes(5);
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

			SafeArrayHandle decrypted = LargeMessageEncryptor.Decrypt(appointmentIdBytes, account.AccountAppointment.AppointmentPrivateKey, LargeMessageEncryptor.EncryptionStrength.Regular);

			// lets get our secret appointment Id
			TypeSerializer.Deserialize(decrypted.Span, out Guid appointmentId);

			await walletProvider.ScheduleTransaction((provider, token, lc) => {

				account.AccountAppointment.AppointmentTime = appointmentTime;
				account.AccountAppointment.AppointmentContextTime = appointmentTime.AddMinutes(-preparation);
				account.AccountAppointment.AppointmentVerificationTime = appointmentTime.AddMinutes(finalization);

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

			this.CentralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AppointmentRequestConfirmed, new CorrelationContext());
		}

		public async Task<SafeArrayHandle> GetAppointmentRequestConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext) {

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

					if(appointmentTime.HasValue && (appointmentVerificationConfirmationMessage.AppointmentTimestamp == appointmentTime.Value)) {
						AppointmentVerificationConfirmationMessage.AppointmentRequester resultEntry = appointmentVerificationConfirmationMessage.Requesters.SingleOrDefault(e => e.RequesterId == account.AccountAppointment.RequesterId.Value);

						if(resultEntry != null) {

							await this.ProcessAppointmentVerificationConfirmation(resultEntry.AppointmentConfirmationCode, appointmentVerificationConfirmationMessage.VerificationSpan, lockContext).ConfigureAwait(false);
						}
					}
				}
			}

			return false;
		}

		private async Task CheckAppointmentCompletedUpdate(LockContext lockContext) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			if(!account.AccountAppointment.LastAppointmentOperationTimeout.HasValue || (account.AccountAppointment.LastAppointmentOperationTimeout.Value < DateTimeEx.CurrentTime)) {
				(bool success, CheckAppointmentVerificationConfirmedResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentCompletedUpdateCheck(account.AccountAppointment.RequesterId.Value, account.AccountAppointment.AppointmentId.Value, lockContext).ConfigureAwait(false);

				var retry = true;

				if(success && (result != null)) {

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

					if(appointmentTime.HasValue && (result.Appointment == appointmentTime.Value)) {
						retry = false;

						await this.ProcessAppointmentVerificationConfirmation(SafeArrayHandle.Wrap(result.ConfirmationCorrelationCode), TimeSpan.FromTicks(result.VerificationSpan), lockContext, a => {

							a.AccountAppointment.LastAppointmentOperationTimeout = null;
						}).ConfigureAwait(false);
					}
				}

				if(retry) {
					await walletProvider.ScheduleTransaction((provider, token, lc) => {
						account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime.AddMinutes(5);

						return Task.FromResult(true);
					}, lockContext).ConfigureAwait(false);
				}
			}
		}

		private async Task ProcessAppointmentVerificationConfirmation(SafeArrayHandle appointmentConfirmationCodeBytes, TimeSpan verificationSpan, LockContext lockContext, Action<IWalletAccount> transactionExtra = null) {

			if((appointmentConfirmationCodeBytes == null) || appointmentConfirmationCodeBytes.IsZero) {
				return;
			}

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			(bool verified, long? appointmentConfirmationCode) = AppointmentUtils.DecryptSecretConfirmationCorrelationCode(appointmentConfirmationCodeBytes, SafeArrayHandle.Wrap(account.AccountAppointment.VerificationResponseSeed));

			// we received our reply!!
			await walletProvider.ScheduleTransaction((provider, token, lc) => {

				if(verified && (account.Status == Enums.PublicationStatus.Published)) {
					// this is it, we are done and re-validated
					account.AccountAppointment = null;
					account.SMSDetails = null;
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + verificationSpan;
					this.AppointmentMode = Enums.AppointmentStatus.None;
				} else {
					// we can proceed to the next steps
					account.AccountAppointment.AppointmentVerified = verified;
					account.AccountAppointment.AppointmentVerificationSpan = verificationSpan;
					account.AccountAppointment.AppointmentConfirmationCode = appointmentConfirmationCode;
					account.AccountAppointment.AppointmentConfirmationCodeExpiration = account.AccountAppointment.AppointmentVerificationTime.Value.AddDays(2);
					account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentCompleted;

					this.AppointmentMode = account.AccountAppointment.AppointmentStatus;
				}

				if(transactionExtra != null) {
					transactionExtra(account);
				}

				return Task.FromResult(true);
			}, lockContext).ConfigureAwait(false);

			this.CentralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AppointmentVerificationCompleted(verified, appointmentConfirmationCode), new CorrelationContext());
		}

		public async Task<SafeArrayHandle> GetAppointmentVerificationConfirmationMessage(Guid requesterId, DateTime appointment, LockContext lockContext) {

			IAppointmentVerificationConfirmationEntry messageEntry = await this.AppointmentRegistryDal.GetAppointmentVerificationConfirmations(requesterId, appointment).ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		private readonly RecursiveAsyncLock appointmentDetailsLocker = new RecursiveAsyncLock();
		
		
		/// <summary>
		/// ensure that appointment details are loaded. otherwise do so
		/// </summary>
		/// <param name="appointment"></param>
		/// <returns></returns>
		private async Task EnsureAppointmentDetails(DateTime appointment) {
			using(await appointmentDetailsLocker.LockAsync().ConfigureAwait(false)) {
				
				if(!this.AppointmentDetails.ContainsKey(appointment)) {

					await UpdateAppointmentDetails(appointment).ConfigureAwait(false);

					if(this.AppointmentDetails.ContainsKey(appointment)) {
						this.AppointmentDetails[appointment].TriggerKey = await this.GetAppointmentKey(appointment).ConfigureAwait(false);
					}
				}
			}
		}

		public async Task UpdateAppointmentDetails(DateTime appointment) {
			var session = await this.AppointmentRegistryDal.GetAppointmentValidatorSession(appointment).ConfigureAwait(false);

			if(session == null) {
				return;
			}

			this.AppointmentDetails.TryAdd(appointment, new AppointmentDataDetails());

			foreach(KeyValuePair<int, int> codeEntry in this.GetValidatorAssignedCodes(session)) {
				if(!this.AppointmentDetails[appointment].ValidatorCodes.ContainsKey(codeEntry.Key)) {
					this.AppointmentDetails[appointment].ValidatorCodes.TryAdd(codeEntry.Key, codeEntry.Value);
				}
			}

			foreach(int index in this.GetValidatorAssignedIndices(session)) {
				if(!this.AppointmentDetails[appointment].assignedIndices.ContainsKey(index)) {
					this.AppointmentDetails[appointment].assignedIndices.TryAdd(index, 0);
				}
			}
		}
		
		public async Task<ConcurrentDictionary<int, int>> GetValidatorAssignedCodes(DateTime appointment) {

			await this.EnsureAppointmentDetails(appointment).ConfigureAwait(false);

			if(this.AppointmentDetails.ContainsKey(appointment)) {
				return this.AppointmentDetails[appointment].ValidatorCodes;
			}

			return null;
		}

		public bool IsRequesterIndexAssigned(DateTime appointment, int index) {

			bool result = false;
			if(this.AppointmentDetails.ContainsKey(appointment)) {
				result = this.AppointmentDetails[appointment].assignedIndices.ContainsKey(index);
			}

			return result;
		}

	#region appointment validator

		public async Task RecordRequestSecretCode(DateTime appointment, int index, SafeArrayHandle validatorCode) {

			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry == null) {
				entry = new AppointmentRequesterResultSqlite();
				entry.Appointment = appointment;
				entry.Index = index;
				entry.ValidatorCode = validatorCode.ToExactByteArrayCopy();
				entry.RequestedCode = DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.InsertAppointmentRequesterResult(entry).ConfigureAwait(false);
			}
		}

		public async Task RecordTriggerPuzzle(DateTime appointment, int index, int secretCode) {
			
			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry == null) {
				entry = new AppointmentRequesterResultSqlite();
				entry.Appointment = appointment;
				entry.Index = index;
				entry.SecretCode = secretCode;
				entry.Trigger = DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.InsertAppointmentRequesterResult(entry).ConfigureAwait(false);
			} else {
				entry.SecretCode = secretCode;
				entry.Trigger = DateTimeEx.CurrentTime;
				await this.AppointmentRegistryDal.UpdateAppointmentRequesterResult(entry).ConfigureAwait(false);
			}
		}

		public async Task<bool> RecordCompletePuzzle(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results) {
			IAppointmentRequesterResult entry = await this.AppointmentRegistryDal.GetAppointmentRequesterResult(appointment, index).ConfigureAwait(false);

			if(entry != null) {

				using SafeArrayHandle bytes = AppointmentsResultTypeSerializer.SerializeResultSet(results);
				entry.Results = bytes.ToExactByteArrayCopy();
				entry.Completed = DateTimeEx.CurrentTime;

				await this.AppointmentRegistryDal.UpdateAppointmentRequesterResult(entry).ConfigureAwait(false);

				return true;
			}

			return false;
		}

		public Dictionary<int, int> GetValidatorAssignedCodes(IAppointmentValidatorSession appointmentValidatorSession) {
			
			return AppointmentUtils.RehydrateAssignedSecretCodes(SafeArrayHandle.Wrap(appointmentValidatorSession.SecretCodes));
		}

		public HashSet<int> GetValidatorAssignedIndices(IAppointmentValidatorSession appointmentValidatorSession) {
			
			return AppointmentUtils.RehydrateAssignedIndices(SafeArrayHandle.Wrap(appointmentValidatorSession.Indices));
		}

		public class AppointmentDataDetails {
			public ConcurrentDictionary<int, int> ValidatorCodes  { get; }      = new ConcurrentDictionary<int, int>();
			public SafeArrayHandle                TriggerKey      { get; set; } = new SafeArrayHandle();
			public ConcurrentDictionary<int, int> assignedIndices { get; }      = new ConcurrentDictionary<int, int>();
		}

		public ConcurrentDictionary<DateTime, AppointmentDataDetails> AppointmentDetails { get; } = new ConcurrentDictionary<DateTime, AppointmentDataDetails>();

		private RecursiveAsyncLock appointmentExistsAndStartedLocker = new RecursiveAsyncLock();
		public async Task<bool> AppointmentExistsAndStarted(DateTime appointment) {
			using(await this.appointmentExistsAndStartedLocker.LockAsync().ConfigureAwait(false)) {

				if(this.AppointmentDetails.ContainsKey(appointment)) {
					return true;
				}

				IAppointmentTriggerGossipMessage trigger = await this.AppointmentRegistryDal.GetAppointmentTrigger().ConfigureAwait(false);

				if(trigger != null) {
					return trigger.Appointment == appointment;
				}

				return false;
			}
		}

		private readonly RecursiveAsyncLock appointmentKeyLocker = new RecursiveAsyncLock();
		public async Task<SafeArrayHandle> GetAppointmentKey(DateTime appointment) {

			SafeArrayHandle key = null;

			using(await appointmentKeyLocker.LockAsync().ConfigureAwait(false)) {
				if(this.AppointmentDetails.ContainsKey(appointment)) {
					key = this.AppointmentDetails[appointment].TriggerKey;

					if((key != null) && !key.IsZero) {
						return this.AppointmentDetails[appointment].TriggerKey;
					}
				}

				LockContext lockContext = null;

				// load it
				SafeArrayHandle triggerBytes = await this.GetAppointmentTriggerGossipMessage(lockContext).ConfigureAwait(false);

				if(triggerBytes != null) {
					var envelope = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ISignedMessageEnvelope>(triggerBytes);

					envelope.RehydrateContents();
					envelope.Contents.Rehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

					var appointmentTriggerMessage = (IAppointmentTriggerMessage) envelope.Contents.RehydratedEvent;

					if(appointment == appointmentTriggerMessage.Appointment) {
						if(!this.AppointmentDetails.ContainsKey(appointment)) {
							this.AppointmentDetails.TryAdd(appointment, new AppointmentDataDetails());
						}

						this.AppointmentDetails[appointment].TriggerKey = appointmentTriggerMessage.Key.Branch();

						return appointmentTriggerMessage.Key;
					}
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
				entry.Window      = appointmentWindow;
				
				// establish a random dispatch time within the window in hours. we will wait a minimum of 20 minutes
#if TESTING
				entry.Dispatch = entry.Appointment;
#else
				entry.Dispatch = entry.Appointment.AddMinutes(GlobalRandom.GetNext(10, Math.Max(validatorWindow - 30, 30)));
#endif

				IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

				entry.ValidatorHash = AppointmentUtils.GetValidatorIdHash(appointment, this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId, account.Stride);

				await this.AppointmentRegistryDal.InsertAppointmentValidatorSession(entry).ConfigureAwait(false);
			}

			if(validators.ContainsKey(entry.ValidatorHash)) {

				// ok, we have some assigned requesters
				Dictionary<int, int> assignedCodes = this.GetValidatorAssignedCodes(entry);
				HashSet<int>         indices       = this.GetValidatorAssignedIndices(entry);

				SafeArrayHandle encryptedSecretCodeBytes = validators[entry.ValidatorHash];

				var validatorSessionDetails = new AppointmentContextMessage.ValidatorSessionDetails();

				var walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
				IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
				
				using(INTRUPrimeWalletKey key = await walletProvider.LoadKey<INTRUPrimeWalletKey>(GlobalsService.VALIDATOR_SECRET_KEY_NAME, lockContext).ConfigureAwait(false)) {
					
					using SafeArrayHandle decryptedBytes = AppointmentUtils.DecryptValidatorMessage(appointment, encryptedSecretCodeBytes, account.Stride, key.PublicKey);
					validatorSessionDetails.Rehydrate(decryptedBytes);
				}

				foreach((int secretCode, int secretCodeL2) secretCode in validatorSessionDetails.SecretCodes) {
					if(!assignedCodes.ContainsKey(secretCode.secretCode)) {
						assignedCodes.Add(secretCode.secretCode, secretCode.secretCodeL2);
					}
				}

				foreach(int index in validatorSessionDetails.AssignedOpen) {
					if(!indices.Contains(index)) {
						indices.Add(index);
					}
				}

				using var secretCodeBytes = AppointmentUtils.DehydrateAssignedSecretCodes(assignedCodes);
				entry.SecretCodes = secretCodeBytes.ToExactByteArrayCopy();
				
				using var indicesBytes = AppointmentUtils.DehydrateAssignedIndices(indices.ToList());
				entry.Indices = indicesBytes.ToExactByteArrayCopy();
				
				await this.AppointmentRegistryDal.UpdateAppointmentValidatorSession(entry).ConfigureAwait(false);

				if(!this.AppointmentDetails.ContainsKey(appointment)) {
					this.AppointmentDetails.TryAdd(appointment, new AppointmentDataDetails());
				}

				foreach(KeyValuePair<int, int> codeEntry in assignedCodes) {

					if(!this.AppointmentDetails[appointment].ValidatorCodes.ContainsKey(codeEntry.Key)) {
						this.AppointmentDetails[appointment].ValidatorCodes.TryAdd(codeEntry.Key, codeEntry.Value);
					}
				}

				foreach(int index in indices) {

					if(!this.AppointmentDetails[appointment].assignedIndices.ContainsKey(index)) {
						this.AppointmentDetails[appointment].assignedIndices.TryAdd(index, 0);
					}
				}
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

					if(appointmentTime.HasValue && (appointmentTime.Value == appointmentContextMessage.Appointment) && !account.AccountAppointment.AppointmentContextDetailsCached) {
						// ok, this is ours. lets see if we are in the right index
						if((account.AccountAppointment.AppointmentIndex.Value >= range.start) && (account.AccountAppointment.AppointmentIndex.Value <= range.end)) {

							var adjustedIndex = (int) (account.AccountAppointment.AppointmentIndex.Value - range.start);

							using SafeArrayHandle powRuleSetBytes = appointmentContextMessage.POWRuleSet.Dehydrate();
							await this.ProcessAppointmentContext(appointmentContextMessage.PuzzleWindow, appointmentContextMessage.PuzzleEngineVersion, powRuleSetBytes.ToExactByteArrayCopy(), appointmentContextMessage.SecretPuzzles.ToExactByteArrayCopy(), appointmentContextMessage.Applicants[adjustedIndex].ToExactByteArrayCopy(), lockContext).ConfigureAwait(false);
						}
					}
				}
			}

			return false;
		}

		private async Task CheckAppointmentContextUpdate(DateTime appointment, LockContext lockContext) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			(bool success, CheckAppointmentContextResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentContextUpdateCheck(account.AccountAppointment.RequesterId.Value, account.AccountAppointment.AppointmentIndex.Value, appointment, lockContext).ConfigureAwait(false);

			var retry = true;

			if(success) {

				DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;

				if((result != null) && appointmentTime.HasValue && (result.Appointment == appointmentTime.Value)) {
					retry = false;

					await this.ProcessAppointmentContext(result.Window, result.EngineVersion, result.POwRuleSet, result.PuzzleBytes, result.SecretPackageBytes, lockContext, a => {

						a.AccountAppointment.LastAppointmentOperationTimeout = null;
					}).ConfigureAwait(false);
				}
			}

			if(retry) {
				await walletProvider.ScheduleTransaction((provider, token, lc) => {
					account.AccountAppointment.LastAppointmentOperationTimeout = DateTimeEx.CurrentTime.AddMinutes(5);

					return Task.FromResult(true);
				}, lockContext).ConfigureAwait(false);
			}
		}

		private async Task<bool> ProcessAppointmentContext(int window, int engineVersion, byte[] powRuleSet, byte[] secretPuzzles, byte[] secretPackageBytes, LockContext lockContext, Action<IWalletAccount> transactionExtra = null) {

			IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;
			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			// ok, this is our message!  lets cache everything
			var distilledAppointmentContext = new DistilledAppointmentContext();

			distilledAppointmentContext.Window = window;
			distilledAppointmentContext.EngineVersion = engineVersion;
			distilledAppointmentContext.POWRuleSet = powRuleSet;

			if((distilledAppointmentContext.EngineVersion > GlobalsService.MAXIMUM_PUZZLE_ENGINE_VERSION) || (distilledAppointmentContext.EngineVersion < GlobalsService.MINIMUM_PUZZLE_ENGINE_VERSION)) {

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

			this.CentralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AppointmentContextCached, new CorrelationContext());

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
			if(this.IsInAppointmentOperation && (this.AppointmentMode == Enums.AppointmentStatus.AppointmentContextCached)) {
				if((this.appointmentWorkflow != null) && !this.appointmentWorkflow.IsCompleted) {
					this.appointmentWorkflow.SetAnswers(answers);
				}
			}
		}

		public virtual async Task<bool> RecordAppointmentTriggerGossipMessage(IAppointmentTriggerMessage appointmentTriggerMessage, IMessageEnvelope messageEnvelope, LockContext lockContext) {
			SafeArrayHandle envelopeBytes = messageEnvelope.DehydrateEnvelope();
			await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.CacheAppointmentMessage(messageEnvelope.ID, envelopeBytes).ConfigureAwait(false);

			await this.AppointmentRegistryDal.InsertAppointmentTriggerGossipMessage(messageEnvelope.ID, appointmentTriggerMessage.Appointment).ConfigureAwait(false);

			if(!this.AppointmentDetails.ContainsKey(appointmentTriggerMessage.Appointment)) {
				this.AppointmentDetails.TryAdd(appointmentTriggerMessage.Appointment, new AppointmentDataDetails());
			}

			this.AppointmentDetails[appointmentTriggerMessage.Appointment].TriggerKey = appointmentTriggerMessage.Key.Branch();

			return false;
		}

		public virtual async Task<SafeArrayHandle> CheckAppointmentTriggerUpdate(DateTime appointment, LockContext lockContext) {

			if(this.AppointmentDetails.ContainsKey(appointment)) {
				SafeArrayHandle key = this.AppointmentDetails[appointment].TriggerKey;

				if((key != null) && !key.IsZero) {
					return key;
				}
			}
			
			(bool success, CheckAppointmentTriggerResult result) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PerformAppointmentTriggerUpdateCheck(appointment, lockContext).ConfigureAwait(false);

			if(success && (result != null)) {

				if(!this.AppointmentDetails.ContainsKey(result.Appointment)) {
					this.AppointmentDetails.TryAdd(result.Appointment, new AppointmentDataDetails());
				}

				this.AppointmentDetails[result.Appointment].TriggerKey = SafeArrayHandle.Wrap(result.Key);

				return SafeArrayHandle.WrapAndOwn(result.Key);
			}

			return null;
		}

		public virtual async Task<SafeArrayHandle> GetAppointmentTriggerGossipMessage(LockContext lockContext) {

			IAppointmentTriggerGossipMessage messageEntry = await this.AppointmentRegistryDal.GetAppointmentTrigger().ConfigureAwait(false);

			if(messageEntry != null) {
				return await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetCachedAppointmentMessage(messageEntry.MessageUuid).ConfigureAwait(false);
			}

			return null;
		}

		public virtual Task<List<(DateTime appointment, TimeSpan window)>> GetAppointmentWindows() {
			return this.AppointmentRegistryDal.GetAppointments();
		}

		public virtual async Task UpdateOperatingMode(LockContext lockContext) {
			IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			this.OperatingMode = Enums.OperationStatus.None;
			this.AppointmentMode = Enums.AppointmentStatus.None;

			if(account.AccountAppointment != null) {
				this.OperatingMode = Enums.OperationStatus.Appointment;
				this.AppointmentMode = account.AccountAppointment.AppointmentStatus;
			}
		}

		protected ConcurrentDictionary<DateTime, DateTime> appointmentDispatches = new ConcurrentDictionary<DateTime, DateTime>();

		protected virtual async Task<long> GetValidatorKeyHash(byte ordinal, DateTime appointmentTime, LockContext lockContext) {
			using(IWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(ordinal, lockContext).ConfigureAwait(false)) {


				return AppointmentUtils.GetValidatorKeyHash(key.PublicKey, appointmentTime);
			}
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

				DateTime? appointment = await this.AppointmentRegistryDal.GetInRangeAppointments().ConfigureAwait(false);

				if(!appointment.HasValue) {
					if(this.ShouldAct(ref this.checkAppointmentsContexts)) {

						await this.QueryWebAppointments(lockContext).ConfigureAwait(false);
#if TESTING
						this.checkAppointmentsContexts = DateTimeEx.CurrentTime.AddSeconds(15);
#else
						this.checkAppointmentsContexts = DateTimeEx.CurrentTime.AddHours(2);
#endif

						appointment = await this.AppointmentRegistryDal.GetInRangeAppointments().ConfigureAwait(false);
					}
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

					//TODO: all this can be optimized

					// as validators, we have to send results if we have any to send
					List<IAppointmentRequesterResult> ready = await this.AppointmentRegistryDal.GetReadyAppointmentRequesterResult().ConfigureAwait(false);

					if(ready.Any()) {

						var verificationResults = new Dictionary<long, bool>();
						IEnumerable<IGrouping<DateTime, IAppointmentRequesterResult>> readyGroups = ready.GroupBy(e => e.Appointment);

						IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

						// we operate per appointment
						foreach(IGrouping<DateTime, IAppointmentRequesterResult> group in readyGroups) {

							DateTime appointmentTime = group.Key;

							// we only send them when we can
							if(!this.appointmentDispatches.ContainsKey(appointmentTime) || (this.appointmentDispatches[appointmentTime] > DateTimeEx.CurrentTime)) {
								continue;
							}

							NLog.Default.Verbose($"Publishing puzzle {group.Count()} results for appointment {appointmentTime}");

							SafeArrayHandle appointmentKey = this.AppointmentDetails[appointmentTime].TriggerKey;

							if((appointmentKey == null) || appointmentKey.IsZero) {

								await this.CheckAppointmentTriggerUpdate(appointmentTime, lockContext).ConfigureAwait(false);
								
								appointmentKey = this.AppointmentDetails[appointmentTime].TriggerKey;
								
								if((appointmentKey == null) || appointmentKey.IsZero) {

									continue;
								}
							}

							// lets verify our secretCodes Level2
							var codes = await this.GetValidatorAssignedCodes(appointmentTime).ConfigureAwait(false);

							byte ordinal = AppointmentUtils.GetKeyOrdinal2(this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId.ToLongRepresentation());

							long keyHash2 = await this.GetValidatorKeyHash(ordinal, appointmentTime, lockContext).ConfigureAwait(false);

							foreach(IAppointmentRequesterResult entry in group) {


								int codeHash     = 0;
								int secretCodeL2 = 0;
								
								using var results = SafeArrayHandle.Wrap(entry.Results);
								Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> resultSet = null;
								
								if(results != null && !results.IsZero) {
									try {
										resultSet = AppointmentsResultTypeSerializer.DeserializeResultSet(results);

										KeyValuePair<Enums.AppointmentsResultTypes, SafeArrayHandle> secretCodeL2Bytes = resultSet.SingleOrDefault(e => e.Key == Enums.AppointmentsResultTypes.SecretCodeL2);
										secretCodeL2 = AppointmentsResultTypeSerializer.DeserializeSecretCodeL2(secretCodeL2Bytes.Value);

										codeHash = AppointmentUtils.GenerateValidatorSecretCodeHash(entry.SecretCode, appointmentTime, appointmentKey, this.CentralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId, keyHash2, account.Stride);
									} catch(Exception ex) {
										NLog.Default.Error(ex, "Failed to process results");
									}
								}

								if(codeHash == 0 || secretCodeL2 == 0 || !codes.ContainsKey(codeHash) || (codes[codeHash] != secretCodeL2)) {
									// ok, we have a failure to offer the right code L2
									entry.Valid = false;

									verificationResults.Add(entry.Index, false);

									continue;
								}

								// ok, now we can check the POW
								
								var powResult = false;

								try {
									KeyValuePair<Enums.AppointmentsResultTypes, SafeArrayHandle> puzzleDetails = resultSet.SingleOrDefault(e => e.Key == Enums.AppointmentsResultTypes.Puzzle);

									List<int> puzzleAnswers = AppointmentsResultTypeSerializer.DeserializePuzzleResult(puzzleDetails.Value);

									if(puzzleAnswers.Any() && puzzleAnswers.All(e => e != 0)) {
										KeyValuePair<Enums.AppointmentsResultTypes, SafeArrayHandle> powDetails = resultSet.SingleOrDefault(e => e.Key == Enums.AppointmentsResultTypes.POW);
										(long nonce, int solution) pow = AppointmentsResultTypeSerializer.DeserializePOW(powDetails.Value);

										powResult = await AppointmentUtils.VerifyPuzzlePow(appointmentKey, puzzleAnswers.First(), pow.nonce, pow.solution).ConfigureAwait(false);
									}
								} catch(Exception ex) {
									NLog.Default.Error(ex, "Failed to pow results results");
								}

								verificationResults.Add(entry.Index, powResult);
							}

							if(verificationResults.Any()) {
								var workflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateSendAppointmentVerificationResultsMessageWorkflow(ready, verificationResults, new CorrelationContext());

								this.CentralCoordinator.PostWorkflow(workflow);

								await workflow.Wait().ConfigureAwait(false);

								this.appointmentDispatches.Remove(appointmentTime, out var _);
							}
						}

						try {
							await this.AppointmentRegistryDal.ClearReadyAppointmentRequesterResult(ready.Cast<AppointmentRequesterResultSqlite>().Select(e => e.Id).ToList()).ConfigureAwait(false);
						} catch(Exception ex) {
							NLog.Default.Error("Failed to Clear Ready Appointment Requester Results");
						}

						try {
							await this.CleanAppointmentsRegistry().ConfigureAwait(false);

						} catch(Exception ex) {
							NLog.Default.Error("Failed to clear obsolete appointments.");
						}
					}

					// clear obsoletes
					foreach(KeyValuePair<DateTime, DateTime> entry in this.appointmentDispatches.Where(k => k.Key.AddMinutes(5) < DateTimeEx.CurrentTime)) {
						this.appointmentDispatches.Remove(entry.Key, out var _);
					}
				}
			}

			if(this.IsInAppointmentOperation) {

				// ok, we are in an appointment. lets do some followup
				// ok, we are in an appointment
				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentRequested) {
					// ok, we are way behind. we need to get our confirmation

					await this.CheckAppointmentRequestUpdate(lockContext).ConfigureAwait(false);
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentSet) {
					// ok, we are way behind. we need to get our confirmation
					IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;
					DateTime? appointmentContextTime = account.AccountAppointment.AppointmentContextTime;

					if(appointmentTime.HasValue && appointmentContextTime.HasValue && (appointmentContextTime.Value < DateTimeEx.CurrentTime)) {
						// ok, we can try to get the contexts info we need
						await this.CheckAppointmentContextUpdate(appointmentTime.Value, lockContext).ConfigureAwait(false);
					}
				}

				if(this.AppointmentMode == Enums.AppointmentStatus.AppointmentContextCached) {
					// ok, we are ready to go. lets make sure we dont miss the appointment!
					IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

					DateTime? appointmentTime = account.AccountAppointment.AppointmentTime;
					DateTime? appointmentExpirationTime = account.AccountAppointment.AppointmentExpirationTime;

					if(appointmentTime.HasValue && (DateTimeEx.CurrentTime > appointmentTime.Value.AddMinutes(-5)) && (!appointmentExpirationTime.HasValue || (appointmentExpirationTime.Value >= DateTimeEx.CurrentTime))) {
						// this is our appointment!!  it begins! lets begin the workflow

						var ready = false;

						lock(this.appointmentWorkflowLocker) {
							ready = (this.appointmentWorkflow == null) || this.appointmentWorkflow.IsCompleted;
						}

						if(ready) {
							lock(this.appointmentWorkflowLocker) {
								this.appointmentWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateAppointmentPuzzleExecutionWorkflow(new CorrelationContext());
							}

							this.appointmentWorkflow.PuzzleBeginEvent += this.PuzzleBeginEvent;

							this.appointmentWorkflow.Completed2 += async (b, workflow) => {

								try {

									IWalletAccount account2 = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

									this.AppointmentMode = account2.AccountAppointment.AppointmentStatus;
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
					IWalletProviderProxy walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

					IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

					if(account.AccountAppointment.AppointmentConfirmationCodeExpiration < DateTimeEx.CurrentTime) {
						await walletProvider.ScheduleTransaction(async (provider, token, lc) => {

							// we are done
							account.AccountAppointment = null;

							return true;
						}, lockContext).ConfigureAwait(false);
					}

				}
			}
		}

		protected virtual async Task<bool> UpdateAppointmentDispatches() {
			var processed = false;

			if(this.ShouldAct(ref this.checkAppointmentsDispatches)) {
				processed = true;
				List<(DateTime appointment, TimeSpan window)> appointments = await this.AppointmentRegistryDal.GetAppointments().ConfigureAwait(false);

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

			if(success && (result != null)) {

				List<DateTime> registryAppointments = (await this.AppointmentRegistryDal.GetAppointments().ConfigureAwait(false)).Select(a => a.appointment.ToUniversalTime()).ToList();

				List<DateTime> remoteAppointmentsList = result.Appointments.Select(a => a.ToUniversalTime()).ToList();

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

				(bool success2, QueryValidatorAppointmentSessionsResult result2) = await this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.QueryValidatorAppointmentSessions(miningAccountId, missingContextsAppointments, hashesList, lockContext).ConfigureAwait(false);

				if(success2 && (result2 != null)) {
					foreach(QueryValidatorAppointmentSessionsResult.ValidatorAppointmentSession session in result2.Sessions) {

						foreach(QueryValidatorAppointmentSessionsResult.ValidatorAppointmentSession.ValidatorSessionSlice slice in session.Slices) {

							var validators = new Dictionary<Guid, SafeArrayHandle>();

							if(hashes.ContainsKey(session.Appointment)) {
								validators.Add(hashes[session.Appointment], SafeArrayHandle.Wrap(slice.EncryptedSecretCodeBytes));
								await this.ExtractValidatorContext(session.Appointment, session.Window, session.ValidatorWindow, validators, lockContext).ConfigureAwait(false);
							}
						}

						// lets add the appointment window to our registrations
						this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.AddAppointmentWindow(session.Appointment, TimeSpan.FromSeconds(session.ValidatorWindow));
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