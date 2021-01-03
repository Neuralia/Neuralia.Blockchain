using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Pools;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {

	public class AppointmentValidatorDelegate<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAppointmentValidatorDelegate
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CENTRAL_COORDINATOR centralCoordinator;
		private CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		private readonly object nonceLocker = new object();

		private readonly object publicKeyLocker = new object();
		protected readonly ConcurrentDictionary<byte, SafeArrayHandle> publicKeys = new ConcurrentDictionary<byte, SafeArrayHandle>();
		protected bool strideLoaded;
		public AppointmentValidatorDelegate(CENTRAL_COORDINATOR centralCoordinator) {

			this.centralCoordinator = centralCoordinator;
			int targetAppointmentRequesterCount = GlobalSettings.ApplicationSettings.TargetAppointmentRequesterCount;

			this.CodeTranslationResponsePool = new ObjectPool<ValidatorProtocol1.CodeTranslationResponseOperation>(() => new ValidatorProtocol1.CodeTranslationResponseOperation(), targetAppointmentRequesterCount, 100);
			this.TriggerSessionResponsePool = new ObjectPool<ValidatorProtocol1.TriggerSessionResponseOperation>(() => new ValidatorProtocol1.TriggerSessionResponseOperation(), targetAppointmentRequesterCount, 100);
			this.PuzzleCompletedResponsePool = new ObjectPool<ValidatorProtocol1.PuzzleCompletedResponseOperation>(() => new ValidatorProtocol1.PuzzleCompletedResponseOperation(), targetAppointmentRequesterCount, 100);
			this.THSCompletedResponsePool = new ObjectPool<ValidatorProtocol1.THSCompletedResponseOperation>(() => new ValidatorProtocol1.THSCompletedResponseOperation(), targetAppointmentRequesterCount, 100);
		}

		public ConcurrentDictionary<DateTime, AppointmentSurrogateDetails> ActiveAppointmentDetails { get; } = new ConcurrentDictionary<DateTime, AppointmentSurrogateDetails>();

		protected SafeArrayHandle Stride { get; set; }

		protected AccountId GetValidatorAccountId => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.MiningAccountId;
		protected bool IsValidator => this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase.IsValidator;

		// preload the operations for performance reasons
		protected ObjectPool<ValidatorProtocol1.CodeTranslationResponseOperation> CodeTranslationResponsePool;
		protected ObjectPool<ValidatorProtocol1.TriggerSessionResponseOperation> TriggerSessionResponsePool;
		protected ObjectPool<ValidatorProtocol1.PuzzleCompletedResponseOperation> PuzzleCompletedResponsePool;
		protected ObjectPool<ValidatorProtocol1.THSCompletedResponseOperation> THSCompletedResponsePool;

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

		private RecursiveAsyncLock triggerKeyLocker = new RecursiveAsyncLock();
		private RecursiveAsyncLock keyHashLocker = new RecursiveAsyncLock();
		private RecursiveAsyncLock keyHash2Locker = new RecursiveAsyncLock();
		private RecursiveAsyncLock codesLocker = new RecursiveAsyncLock();
		private RecursiveAsyncLock assignedDetailsLocker = new RecursiveAsyncLock();

		public virtual async Task<ValidatorProtocol1.CodeTranslationResponseOperation> HandleCodeTranslationWorkflow(ValidatorProtocol1.CodeTranslationRequestOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			if(operation == null || operation.Index == 0 || operation.ValidatorCode == null || operation.ValidatorCode.IsZero || operation.Appointment == DateTime.MinValue) {
				return null;
			}

			try {
				
				DateTime appointment = DateTime.SpecifyKind(operation.Appointment, DateTimeKind.Utc);

				if(!this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckAppointmentExistsAndStarted(appointment)) {
					if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
						NLog.LoggingBatcher.Verbose($"{nameof(this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted)} for appointment {appointment} failed in request code");

						return null;
					}
				}

				AppointmentSurrogateDetails appointmentDetails = this.ActiveAppointmentDetails.GetOrAdd(appointment, new AppointmentSurrogateDetails());

				if(!appointmentDetails.AssignedIndicesCacheSet) {
					await this.LoadAssignedIndicesCache(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(!appointmentDetails.AssignedIndicesCache.ContainsKey(operation.Index)) {
					NLog.LoggingBatcher.Verbose($"Requester index {operation.Index} is not assigned to validator. failed in {nameof(this.HandleCodeTranslationWorkflow)}");

					return null;
				}

				var resultOperation = this.CodeTranslationResponsePool.GetObject();

				LockContext lockContext = null;

				if(!appointmentDetails.AppointmentKeySet) {
					await this.LoadTriggerKey(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(!appointmentDetails.AppointmentKeySet) {
					await this.LoadTriggerKey(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(appointmentDetails.KeyHash == 0) {
					await this.LoadKeyHash(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(appointmentDetails.RequesterStatuses.TryGetValue(operation.Index, out var entry) && entry.HasFlag(AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.CodeTranslation)) {
					// this requester already did this
					NLog.LoggingBatcher.Verbose($"Requester already did this for index {operation.Index} in {nameof(this.HandleCodeTranslationWorkflow)}");

					return null;
				}

				resultOperation.ValidatorCode = AppointmentUtils.ValidatorRestoreKeyCode(appointment, appointmentDetails.KeyHash, operation.ValidatorCode.Branch(), this.Stride);

				// ok, this requester is valid
				await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordRequestSecretCode(operation.Appointment, operation.Index, operation.ValidatorCode).ConfigureAwait(false);

				NLog.LoggingBatcher.Verbose($"Received a valid puzzle Code Translation request from Index {operation.Index} for appointment {appointment}");

				appointmentDetails.RequesterStatuses.TryAdd(operation.Index, AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.CodeTranslation);

				return resultOperation;
			} finally {
			}
		}

		public virtual async Task<ValidatorProtocol1.TriggerSessionResponseOperation> HandleTriggerSessionWorkflow(ValidatorProtocol1.TriggerSessionOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			if(operation == null || operation.Index == 0 || operation.SecretCode == 0 || operation.Appointment == DateTime.MinValue) {
				return null;
			}

			try {
				
				DateTime appointment = DateTime.SpecifyKind(operation.Appointment, DateTimeKind.Utc);

				if(!this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckAppointmentExistsAndStarted(appointment)) {
					if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
						NLog.LoggingBatcher.Verbose($"{nameof(this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted)} for appointment {appointment} failed in trigger");

						return null;
					}
				}

				LockContext lockContext = null;

				AppointmentSurrogateDetails appointmentDetails = this.ActiveAppointmentDetails.GetOrAdd(appointment, new AppointmentSurrogateDetails());

				if(!appointmentDetails.AssignedIndicesCacheSet) {
					await this.LoadAssignedIndicesCache(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(!appointmentDetails.AssignedIndicesCache.ContainsKey(operation.Index)) {
					NLog.LoggingBatcher.Verbose($"Requester index {operation.Index} is not assigned to validator. failed in {nameof(this.HandleTriggerSessionWorkflow)}");

					return null;
				}

				if(!appointmentDetails.AppointmentKeySet) {
					await this.LoadTriggerKey(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(appointmentDetails.KeyHash2 == 0) {
					await this.LoadKeyHash2(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(appointmentDetails.RequesterStatuses.TryGetValue(operation.Index, out var entry) && entry.HasFlag(AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.TriggerSession)) {
					// this requester already did this
					NLog.LoggingBatcher.Verbose($"Requester already did this for index {operation.Index} in {nameof(this.HandlePuzzleCompletedWorkflow)}");

					return null;
				}

				int codeHash = AppointmentUtils.GenerateValidatorSecretCodeHash(operation.SecretCode, appointment, appointmentDetails.AppointmentKey, this.GetValidatorAccountId, appointmentDetails.KeyHash2, this.Stride);

				if(!appointmentDetails.CodeCacheSet) {
					await this.LoadAssignedCodesCache(appointment, appointmentDetails).ConfigureAwait(false);
				}

				bool valid = false;

				int secretCodeL2 = 0;

				if(appointmentDetails.CodesCache.TryGetValue(codeHash, out var codes)) {
					var codeEntry = codes.SingleOrDefault(e => e.index == operation.Index);

					valid = codeEntry != default;
					secretCodeL2 = codeEntry.secretCodeL2;
				}

				if(!valid) {
					NLog.LoggingBatcher.Verbose($"Code hash {codeHash} from secret code {operation.SecretCode} not found in codes list for index {operation.Index}");

					return null;
				}

				var resultOperation = this.TriggerSessionResponsePool.GetObject();

				await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordTriggerPuzzle(operation.Appointment, operation.Index, operation.SecretCode).ConfigureAwait(false);

				//lets offer the secret level 2 code, proof that they have begun
				resultOperation.SecretCodeL2 = secretCodeL2;

				NLog.LoggingBatcher.Verbose($"Received a valid puzzle trigger request from Index {operation.Index} for appointment {appointment}");

				appointmentDetails.RequesterStatuses.AddOrUpdate(operation.Index, key => AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.TriggerSession, (key, value) => value | AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.TriggerSession);

				return resultOperation;
			} finally {
				
			}
		}

		public virtual async Task<ValidatorProtocol1.PuzzleCompletedResponseOperation> HandlePuzzleCompletedWorkflow(ValidatorProtocol1.PuzzleCompletedOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			if(operation == null || operation.Index == 0 || !operation.Results.Any() || operation.Results.Any(e => e.Value == null || e.Value.IsZero) || operation.Appointment == DateTime.MinValue) {
				return null;
			}

			try {
				
				DateTime appointment = DateTime.SpecifyKind(operation.Appointment, DateTimeKind.Utc);

				if(!this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckAppointmentExistsAndStarted(appointment)) {
					if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
						NLog.LoggingBatcher.Verbose($"{nameof(this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted)} for appointment {appointment} failed in complete session");

						return null;
					}
				}

				AppointmentSurrogateDetails appointmentDetails = this.ActiveAppointmentDetails.GetOrAdd(appointment, new AppointmentSurrogateDetails());

				if(!appointmentDetails.AssignedIndicesCacheSet) {
					await this.LoadAssignedIndicesCache(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(!appointmentDetails.AssignedIndicesCache.ContainsKey(operation.Index)) {
					NLog.LoggingBatcher.Verbose($"Requester index {operation.Index} is not assigned to validator. failed in {nameof(this.HandlePuzzleCompletedWorkflow)}");

					return null;
				}

				if(!appointmentDetails.RequesterStatuses.TryGetValue(operation.Index, out var entry) || entry.HasFlag(AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.PuzzleCompleted)) {
					// this requester already did this
					NLog.LoggingBatcher.Verbose($"Requester already did this for index {operation.Index} in {nameof(this.HandlePuzzleCompletedWorkflow)}");

					return null;
				}

				var resultOperation = this.PuzzleCompletedResponsePool.GetObject();

				bool recorded = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordCompletePuzzle(operation.Appointment, operation.Index, operation.Results).ConfigureAwait(false);

				if(!recorded) {
					NLog.LoggingBatcher.Verbose($"Failed to record completed puzzle in complete session for index {operation.Index}");

					return null;
				}

				resultOperation.Result = true;

				NLog.LoggingBatcher.Verbose($"Received a valid puzzle completed request from Index {operation.Index} for appointment {appointment}");

				appointmentDetails.RequesterStatuses[operation.Index] |= AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.PuzzleCompleted;

				return resultOperation;
			} finally {
				
			}
		}

		public virtual async Task<ValidatorProtocol1.THSCompletedResponseOperation> HandleTHSCompletedWorkflow(ValidatorProtocol1.THSCompletedOperation operation) {
			if(!this.IsValidator) {
				throw new InvalidOperationException();
			}

			if(operation == null || operation.Index == 0 || operation.THSResults == null || operation.THSResults.IsZero || operation.Appointment == DateTime.MinValue) {
				
#if MAINNET_LAUNCH_CODE

				// to accomodate mobiles that are not up to date yet
				if(operation.THSResults == null) {
					var resultOperation2 = this.THSCompletedResponsePool.GetObject();
					resultOperation2.Result = true;
					return resultOperation2;
				}
#else
we have to remove this code!!
#endif
				return null;
			}

			try {
				
				DateTime appointment = DateTime.SpecifyKind(operation.Appointment, DateTimeKind.Utc);

				if(!this.ActiveAppointmentDetails.ContainsKey(appointment)) {
					NLog.LoggingBatcher.Verbose($"Appointment {appointment} not found in complete session");

					return null;
				}

				if(!this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.CheckAppointmentExistsAndStarted(appointment)) {
					if(!await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted(appointment).ConfigureAwait(false)) {
						NLog.LoggingBatcher.Verbose($"{nameof(this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.AppointmentExistsAndStarted)} for appointment {appointment} failed in complete session");

						return null;
					}
				}

				AppointmentSurrogateDetails appointmentDetails = this.ActiveAppointmentDetails.GetOrAdd(appointment, new AppointmentSurrogateDetails());

				if(!appointmentDetails.AssignedIndicesCacheSet) {
					await this.LoadAssignedIndicesCache(appointment, appointmentDetails).ConfigureAwait(false);
				}

				if(!appointmentDetails.AssignedIndicesCache.ContainsKey(operation.Index)) {
					NLog.LoggingBatcher.Verbose($"Requester index {operation.Index} is not assigned to validator. failed in {nameof(this.HandleTHSCompletedWorkflow)}");

					return null;
				}

				if(!appointmentDetails.RequesterStatuses.TryGetValue(operation.Index, out var entry) || entry.HasFlag(AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.THSCompleted)) {
					// this requester already did this
					NLog.LoggingBatcher.Verbose($"Requester already did this for index {operation.Index} in {nameof(this.HandleTHSCompletedWorkflow)}");

					return null;
				}

				var resultOperation = this.THSCompletedResponsePool.GetObject();

				bool recorded = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.RecordCompleteTHS(operation.Appointment, operation.Index, operation.THSResults).ConfigureAwait(false);

				if(!recorded) {
					NLog.LoggingBatcher.Verbose($"Failed to record completed puzzle in complete session for index {operation.Index}");

					return null;
				}

				resultOperation.Result = true;
				NLog.LoggingBatcher.Verbose($"Received a valid puzzle completed request from Index {operation.Index} for appointment {appointment}");

				appointmentDetails.RequesterStatuses[operation.Index] |= AppointmentSurrogateDetails.AppointmentValidationWorkflowSteps.THSCompleted;

				return resultOperation;
			} finally {
				
			}
		}

		protected async Task LoadAssignedCodesCache(DateTime appointment, AppointmentSurrogateDetails appointmentDetails) {
			if(!appointmentDetails.CodeCacheSet) {
				using(await this.codesLocker.LockAsync().ConfigureAwait(false)) {
					if(!appointmentDetails.CodeCacheSet) {
						appointmentDetails.CodesCache = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetValidatorAssignedCodes(appointment).ConfigureAwait(false);
					}
				}
			}
		}

		protected async Task LoadAssignedIndicesCache(DateTime appointment, AppointmentSurrogateDetails appointmentDetails) {
			if(!appointmentDetails.AssignedIndicesCacheSet) {
				using(await this.assignedDetailsLocker.LockAsync().ConfigureAwait(false)) {
					if(!appointmentDetails.AssignedIndicesCacheSet) {
						appointmentDetails.AssignedIndicesCache = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetValidatorAssignedIndices(appointment).ConfigureAwait(false);
					}
				}
			}
		}

		protected async Task LoadTriggerKey(DateTime appointment, AppointmentSurrogateDetails appointmentDetails) {

			if(!appointmentDetails.AppointmentKeySet) {
				using(await this.triggerKeyLocker.LockAsync().ConfigureAwait(false)) {
					if(!appointmentDetails.AppointmentKeySet) {
						appointmentDetails.AppointmentKey = await Repeater.RepeatAsync(async () => {

							var key = await this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase.GetAppointmentKey(appointment).ConfigureAwait(false);

							if(key == null || key.IsZero) {
								throw new ApplicationException("No trigger key found");
							}

							return key;
						}, 5).ConfigureAwait(false);
					}
				}
			}
		}

		protected async Task LoadKeyHash(DateTime appointment, AppointmentSurrogateDetails appointmentDetails) {

			if(appointmentDetails.KeyHash == 0) {
				using(await this.keyHashLocker.LockAsync().ConfigureAwait(false)) {
					if(appointmentDetails.KeyHash == 0) {
						byte ordinal = AppointmentUtils.GetKeyOrdinal(this.GetValidatorAccountId.ToLongRepresentation());

						appointmentDetails.KeyHash = Repeater.Repeat(() => {

							LockContext lockContext = null;
							var keyHash = this.GetValidatorKeyHash(ordinal, appointment, lockContext);

							if(keyHash == 0) {
								throw new ApplicationException();
							}

							return keyHash;
						}, 5);
					}
				}
			}
		}

		protected async Task LoadKeyHash2(DateTime appointment, AppointmentSurrogateDetails appointmentDetails) {

			if(appointmentDetails.KeyHash2 == 0) {
				using(await this.keyHash2Locker.LockAsync().ConfigureAwait(false)) {
					if(appointmentDetails.KeyHash2 == 0) {
						byte ordinal = AppointmentUtils.GetKeyOrdinal2(this.GetValidatorAccountId.ToLongRepresentation());

						appointmentDetails.KeyHash2 = Repeater.Repeat(() => {

							LockContext lockContext = null;
							var keyHash = this.GetValidatorKeyHash(ordinal, appointment, lockContext);

							if(keyHash == 0) {
								throw new ApplicationException();
							}

							return keyHash;
						}, 5);
					}
				}
			}
		}

		protected virtual SafeArrayHandle GetPublicKey(byte ordinal, LockContext lockContext) {
			lock(this.publicKeyLocker) {
				if(!this.publicKeys.TryGetValue(ordinal, out var entry)) {
					using(IWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(ordinal, lockContext).WaitAndUnwrapException()) {
						entry = key.PublicKey.Clone();
						this.publicKeys.TryAdd(ordinal, entry);
					}
				}

				return entry.Clone();
			}
		}

		protected virtual long GetValidatorKeyHash(byte ordinal, DateTime appointment, LockContext lockContext) {
			using SafeArrayHandle publicKey = this.GetPublicKey(ordinal, lockContext);

			return AppointmentUtils.GetValidatorKeyHash(publicKey, appointment);
		}
	}

	public class AppointmentSurrogateDetails {

		[Flags]
		public enum AppointmentValidationWorkflowSteps : byte {
			None = 0,
			CodeTranslation = 1 << 0,
			TriggerSession = 1 << 1,
			PuzzleCompleted = 1 << 2,
			THSCompleted = 1 << 3
		}

		private SafeArrayHandle appointmentKey;

		public SafeArrayHandle AppointmentKey {
			get => Volatile.Read(ref this.appointmentKey);
			set {
				Volatile.Write(ref this.appointmentKey, value);
				this.AppointmentKeySet = value != null && !value.IsZero;
			}
		}

		private long appointmentKeySet;

		public bool AppointmentKeySet {
			get => Interlocked.Read(ref this.appointmentKeySet) == 1;
			private set => Interlocked.Exchange(ref this.appointmentKeySet, value ? 1 : 0);
		}

		private long keyHash;

		public long KeyHash {
			get => Interlocked.Read(ref this.keyHash);
			set => Interlocked.Exchange(ref this.keyHash, value);
		}

		private long keyHash2;

		public long KeyHash2 {
			get => Interlocked.Read(ref this.keyHash2);
			set => Interlocked.Exchange(ref this.keyHash2, value);
		}

		public ConcurrentDictionary<long, AppointmentValidationWorkflowSteps> RequesterStatuses { get; } = new ConcurrentDictionary<long, AppointmentValidationWorkflowSteps>();

		private ConcurrentDictionary<int, (int secretCodeL2, long index)[]> codesCache;

		public ConcurrentDictionary<int, (int secretCodeL2, long index)[]> CodesCache {
			get => Volatile.Read(ref this.codesCache);
			set {
				Volatile.Write(ref this.codesCache, value);
				this.CodeCacheSet = value != null;
			}
		}

		private long codeCacheSet;

		public bool CodeCacheSet {
			get => Interlocked.Read(ref this.codeCacheSet) == 1;
			private set => Interlocked.Exchange(ref this.codeCacheSet, (this.CodesCache?.Any() ?? false) && value ? 1 : 0);
		}

		private ConcurrentDictionary<int, int> assignedIndicesCache;

		public ConcurrentDictionary<int, int> AssignedIndicesCache {
			get => Volatile.Read(ref this.assignedIndicesCache);
			set {
				Volatile.Write(ref this.assignedIndicesCache, value);
				this.AssignedIndicesCacheSet = value != null;
			}
		}

		private long assignedIndicesCacheSet;

		public bool AssignedIndicesCacheSet {
			get => Interlocked.Read(ref this.assignedIndicesCacheSet) == 1;
			private set => Interlocked.Exchange(ref this.assignedIndicesCacheSet, (this.assignedIndicesCache?.Any() ?? false) && value ? 1 : 0);
		}
	}
}