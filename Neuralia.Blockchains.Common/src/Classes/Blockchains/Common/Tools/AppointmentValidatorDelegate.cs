using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {

	public class AppointmentValidatorDelegate<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAppointmentValidatorDelegate
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly object nonceLocker = new object();

		private readonly object publicKeyLocker = new object();
		protected readonly ConcurrentDictionary<byte, SafeArrayHandle> publicKeys = new ConcurrentDictionary<byte, SafeArrayHandle>();
		protected bool strideLoaded;

		public AppointmentValidatorDelegate(CENTRAL_COORDINATOR centralCoordinator) {
			this.centralCoordinator = centralCoordinator;
		}

		public ConcurrentDictionary<DateTime, AppointmentDetails> ActiveAppointmentDetails { get; } = new ConcurrentDictionary<DateTime, AppointmentDetails>();
		protected SafeArrayHandle Stride { get; set; }

		protected AccountId GetValidatorAccountId => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId;
		protected bool IsValidator => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidator;

		public virtual void Initialize() {

			lock(this.nonceLocker) {
				if(!this.strideLoaded) {
					LockContext lockContext = null;
					IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).WaitAndUnwrapException();

					this.Stride = account.Stride.Clone();
					this.strideLoaded = true;
				}
			}
		}
		
		private RecursiveAsyncLock activeAppointmentDetailsLocker = new RecursiveAsyncLock();
		public virtual async Task<ValidatorProtocol1.CodeTranslationResponseOperation> HandleCodeTranslationWorkflow(ValidatorProtocol1.CodeTranslationRequestOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			DateTime appointment = operation.Appointment;

			if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
				return null;
			}
			

			if(!this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.IsRequesterIndexAssigned(appointment, operation.Index)) {
				return null;
			}
			
			var resultOperation = new ValidatorProtocol1.CodeTranslationResponseOperation();

			using(await this.activeAppointmentDetailsLocker.LockAsync().ConfigureAwait(false)) {
				if(!this.ActiveAppointmentDetails.ContainsKey(appointment)) {
					byte ordinal = AppointmentUtils.GetKeyOrdinal(this.GetValidatorAccountId.ToLongRepresentation());

					LockContext lockContext = null;

					var details = new AppointmentDetails();
					details.keyHash = this.GetValidatorKeyHash(ordinal, appointment, lockContext);
					details.AppointmentKey = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentKey(appointment).ConfigureAwait(false);

					this.ActiveAppointmentDetails.TryAdd(appointment, details);
				}
			}

			AppointmentDetails appointmentDetails = this.ActiveAppointmentDetails[appointment];

			if(appointmentDetails.RequesterStatuses.ContainsKey(operation.Index) && appointmentDetails.RequesterStatuses[operation.Index].HasFlag(AppointmentDetails.AppointmentValidationWorkflowSteps.CodeTranslation)) {
				// this requester already did this
				return null;
			}

			resultOperation.ValidatorCode = AppointmentUtils.ValidatorRestoreKeyCode(appointment, appointmentDetails.keyHash, operation.ValidatorCode.Branch(), this.Stride);

			// ok, this requester is valid
			await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordRequestSecretCode(operation.Appointment, operation.Index, operation.ValidatorCode).ConfigureAwait(false);

			NLog.Default.Verbose($"Received a valid puzzle Code Translation request from Index {operation.Index} for appointment {appointment}");

			if(!appointmentDetails.RequesterStatuses.ContainsKey(operation.Index)) {
				appointmentDetails.RequesterStatuses.TryAdd(operation.Index, AppointmentDetails.AppointmentValidationWorkflowSteps.None);
			}

			appointmentDetails.RequesterStatuses[operation.Index] |= AppointmentDetails.AppointmentValidationWorkflowSteps.CodeTranslation;

			return resultOperation;
		}

		private readonly RecursiveAsyncLock locker1 = new RecursiveAsyncLock();
		private readonly object locker2 = new object();
		private readonly RecursiveAsyncLock locker3 = new RecursiveAsyncLock();
		
		public virtual async Task<ValidatorProtocol1.TriggerSessionResponseOperation> HandleTriggerSessionWorkflow(ValidatorProtocol1.TriggerSessionOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			DateTime appointment = operation.Appointment;

			if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
				return null;
			}

			byte ordinal = AppointmentUtils.GetKeyOrdinal2(this.GetValidatorAccountId.ToLongRepresentation());

			LockContext lockContext = null;
			AppointmentDetails appointmentDetails = null;

			using(await this.locker1.LockAsync().ConfigureAwait(false)) {
				if(!this.ActiveAppointmentDetails.ContainsKey(appointment)) {

					var details = new AppointmentDetails();
					details.keyHash2 = this.GetValidatorKeyHash(ordinal, appointment, lockContext);
					this.ActiveAppointmentDetails.TryAdd(appointment, details);
				}
			}
			
			appointmentDetails = this.ActiveAppointmentDetails[appointment];
			
			using(await this.locker3.LockAsync().ConfigureAwait(false)) {

				if(!appointmentDetails.AppointmentKeyValid) {
					appointmentDetails.AppointmentKey = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentKey(appointment).ConfigureAwait(false);
					appointmentDetails.AppointmentKeyValid = true;
				}
			}

			lock(this.locker2) {
				if(appointmentDetails.keyHash2 == 0) {

					appointmentDetails.keyHash2 = this.GetValidatorKeyHash(ordinal, appointment, lockContext);
				}
			}
			
			if(appointmentDetails.RequesterStatuses.ContainsKey(operation.Index) && appointmentDetails.RequesterStatuses[operation.Index].HasFlag(AppointmentDetails.AppointmentValidationWorkflowSteps.TriggerSession)) {
				// this requester already did this
				return null;
			}

			int codeHash = AppointmentUtils.GenerateValidatorSecretCodeHash(operation.SecretCode, appointment, appointmentDetails.AppointmentKey.Branch(), this.GetValidatorAccountId, appointmentDetails.keyHash2, this.Stride);

			ConcurrentDictionary<int, ushort> codes = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetValidatorAssignedCodes(appointment).ConfigureAwait(false);

			if((codes == null) || !codes.ContainsKey(codeHash)) {
				return null;
			}

			var resultOperation = new ValidatorProtocol1.TriggerSessionResponseOperation();

			await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordTriggerPuzzle(operation.Appointment, operation.Index, operation.SecretCode).ConfigureAwait(false);

			//lets offer the secret level 2 code, proof that they have begun
			resultOperation.SecretCodeL2 = codes[codeHash];

			NLog.Default.Verbose($"Received a valid puzzle trigger request from Index {operation.Index} for appointment {appointment}");

			if(!appointmentDetails.RequesterStatuses.ContainsKey(operation.Index)) {
				appointmentDetails.RequesterStatuses.TryAdd(operation.Index, AppointmentDetails.AppointmentValidationWorkflowSteps.None);
			}

			appointmentDetails.RequesterStatuses[operation.Index] |= AppointmentDetails.AppointmentValidationWorkflowSteps.TriggerSession;

			return resultOperation;
		}

		public virtual async Task<ValidatorProtocol1.CompleteSessionResponseOperation> HandleCompleteSessionWorkflow(ValidatorProtocol1.CompleteSessionOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			DateTime appointment = operation.Appointment;

			
			if(!this.ActiveAppointmentDetails.ContainsKey(appointment)) {
				return null;
			}

			if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
				return null;
			}

			AppointmentDetails appointmentDetails = this.ActiveAppointmentDetails[appointment];

			if(!appointmentDetails.RequesterStatuses.ContainsKey(operation.Index) || appointmentDetails.RequesterStatuses[operation.Index].HasFlag(AppointmentDetails.AppointmentValidationWorkflowSteps.CompleteSession)) {
				// this requester already did this
				return null;
			}

			var resultOperation = new ValidatorProtocol1.CompleteSessionResponseOperation();

			bool recorded = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordCompletePuzzle(operation.Appointment, operation.Index, operation.Results).ConfigureAwait(false);

			if(!recorded) {
				return null;
			}

			resultOperation.Result = true;

			NLog.Default.Verbose($"Received a valid puzzle completed request from Index {operation.Index} for appointment {appointment}");

			appointmentDetails.RequesterStatuses[operation.Index] |= AppointmentDetails.AppointmentValidationWorkflowSteps.CompleteSession;

			return resultOperation;
		}

		protected virtual SafeArrayHandle GetPublicKey(byte ordinal, LockContext lockContext) {
			lock(this.publicKeyLocker) {
				if(this.publicKeys.ContainsKey(ordinal)) {
					return this.publicKeys[ordinal];
				}

				using(IWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(ordinal, lockContext).WaitAndUnwrapException()) {
					SafeArrayHandle publicKey = key.PublicKey.Clone();

					this.publicKeys.TryAdd(ordinal, publicKey);

					return publicKey;
				}
			}
		}

		protected virtual long GetValidatorKeyHash(byte ordinal, DateTime appointment, LockContext lockContext) {
			SafeArrayHandle publicKey = this.GetPublicKey(ordinal, lockContext);

			return AppointmentUtils.GetValidatorKeyHash(publicKey, appointment);
		}

		public class AppointmentDetails {

			[Flags]
			public enum AppointmentValidationWorkflowSteps : byte {
				None = 0,
				CodeTranslation = 1 << 0,
				TriggerSession = 1 << 1,
				CompleteSession = 1 << 2
			}

			public long keyHash { get; set; }
			public long keyHash2 { get; set; }
			public SafeArrayHandle AppointmentKey { get; set; }
			public bool AppointmentKeyValid  { get; set; }
			public ConcurrentDictionary<long, AppointmentValidationWorkflowSteps> RequesterStatuses { get; } = new ConcurrentDictionary<long, AppointmentValidationWorkflowSteps>();
		}
	}
}