using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	public interface IThreadExclusionResourceAccessScheduler : ILoopThread<ThreadExclusionResourceAccessScheduler> {
		bool ScheduleRead(IWalletProvider walletProvider, Action<IWalletProvider> action, bool allowWait = true);
		void ScheduleWrite(IWalletProvider walletProvider, Action<IWalletProvider> action);
	}

	/// <summary>
	///     This class is meant to be an insulator, allowing reads while there is no write, and givign writes exclusive access.
	///     It can also lock the access per thread and pass it on to child threads.
	/// </summary>
	/// <remarks>
	///     CAREFUL!!!  this thread lock is NOT compatible with async actors!  it can cause deadlocks. make sure all is
	///     synchronous inside a locked thread
	/// </remarks>
	public class ThreadExclusionResourceAccessScheduler : LoopThread<ThreadExclusionResourceAccessScheduler>, IThreadExclusionResourceAccessScheduler {
		private readonly HashSet<int> friendlyThreads = new HashSet<int>();

		public override void Stop() {

			this.timeoutActive = false;
			this.timeoutTimerResetEvent.Set();

			base.Stop();
		}

		private readonly Stack<int> lockThreadStack = new Stack<int>();

		/// <summary>
		///     This is the list of read requests that are differred (waiting) for a read request to happen
		/// </summary>
		public readonly ConcurrentDictionary<Guid, Ticket> requestedReads = new ConcurrentDictionary<Guid, Ticket>();

		public readonly ConcurrentDictionary<Guid, Ticket> requestedWrites = new ConcurrentDictionary<Guid, Ticket>();

		private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
		private readonly ManualResetEventSlim writeResetEvent = new ManualResetEventSlim(false);
		private readonly object mainCollectionsLocker = new object();

		private int activeReadTasks;

		private int currentActiveThread;
		private bool isWriting;
		private CancellationToken? token;

		/// <summary>
		/// The timeout timer used to ensure we can timeout properly
		/// </summary>
		private readonly Task timetoutTimer;

		private readonly ManualResetEventSlim timeoutTimerResetEvent = new ManualResetEventSlim(false);
		protected override void DisposeAll() {
			try {
				this.timeoutTimerResetEvent?.Dispose();
			} catch {
				
			}
			base.DisposeAll();
		}

		private int inLock = 0;

		private static readonly TimeSpan timeoutLocked = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan timeoutNotLocked = TimeSpan.FromSeconds(5);
		private bool timeoutActive = true;
		private CancellationTokenSource timeoutCancellationSource;
		private DateTime? lockExpiration;

		public ThreadExclusionResourceAccessScheduler() : base(30) {

			this.timetoutTimer = new Task(this.ThreadLockTimeoutAction, this.CancelNeuralium, TaskCreationOptions.LongRunning);
			this.timetoutTimer.Start();
		}

		public CancellationToken? Token => this.token;
		public CancellationToken ActiveToken {
			get {
				var cancellationToken = this.token;

				if(cancellationToken != null) {
					return cancellationToken.Value;
				}
				throw new ArgumentNullException();
			}
		}

		public bool HasActiveToken => this.Token.HasValue;

		private int ActiveReadTasks {
			get {
				lock(this.locker) {
					return this.activeReadTasks;
				}
			}
			set {
				lock(this.locker) {
					this.activeReadTasks = value;
				}
			}
		}

		public int CurrentActiveThread {
			get {
				lock(this.locker) {
					return this.currentActiveThread;
				}

			}
			private set {
				lock(this.locker) {
					this.currentActiveThread = value;
				}
			}
		}

		public bool ThreadLockInProgress => this.CurrentActiveThread != 0;

		public bool IsWriting {
			get {
				lock(this.locker) {
					return this.isWriting;
				}
			}
		}

		public bool TheadAllowed {

			get {
				lock(this.locker) {
					return !this.ThreadLockInProgress || (this.CurrentActiveThread == Thread.CurrentThread.ManagedThreadId) || this.friendlyThreads.Contains(Thread.CurrentThread.ManagedThreadId);
				}
			}
		}

		private bool IsLocked {
			get {
				lock(this.locker) {
					return this.ThreadLockInProgress && !this.TheadAllowed;
				}
			}
		}

		private bool CanRead {
			get {
				lock(this.locker) {

					var writes = this.requestedWrites.Values.ToArray();

					// first, determine if we have any usable writes
					bool hasWritesRequested = this.WritesRemaining(writes);

					return !this.IsLocked && !this.IsWriting && !hasWritesRequested;
				}
			}
		}

		public bool ScheduleRead(IWalletProvider walletProvider, Action<IWalletProvider> action, bool allowWait = true) {
			using(Ticket ticket = new Ticket()) {

				if(this.CanRead) {
					this.PerformRead(walletProvider, action, ticket);

					return true;
				}

				if(!allowWait) {
					return false;
				}

				// ok, if we get here, its locked. no choice but to wait
				lock(this.mainCollectionsLocker) {
					this.requestedReads.AddSafe(ticket.Id, ticket);
				}

				this.resetEvent.Set();

				ticket.WaitForTurn();

				this.PerformRead(walletProvider, action, ticket);

				return true;
			}
		}

		public void ScheduleWrite(IWalletProvider walletProvider, Action<IWalletProvider> action) {
			using(Ticket ticket = new Ticket()) {

				lock(this.mainCollectionsLocker) {
					this.requestedWrites.AddSafe(ticket.Id, ticket);
				}

				this.resetEvent.Set();

				ticket.WaitForTurn();

				try {
					action(walletProvider);
				} finally {
					ticket.Completed();
				}
			}
		}

		protected bool ThreadLockedFilter(Ticket t) {
			lock(this.locker) {
				return (t.ThreadId == this.CurrentActiveThread) || this.friendlyThreads.Contains(t.ThreadId);
			}
		}

		protected bool WritesRemaining(Ticket[] availableWrites) {
			if(availableWrites.Any()) {
				if(this.ThreadLockInProgress) {
					return availableWrites.Any(this.ThreadLockedFilter);
				}

				return true;
			}

			return false;
		}

		protected override void ProcessLoop() {

			bool loop = false;

			do {
				loop = false;
				bool hasWritesRequested = false;

				try {

					var writes = this.requestedWrites.Values.ToArray();

					// first, determine if we have any usable writes
					hasWritesRequested = this.WritesRemaining(writes);

					lock(this.locker) {
						if(this.ActiveReadTasks <= 0) {
							this.ActiveReadTasks = 0;
						}

						if((this.ActiveReadTasks == 0) && hasWritesRequested) {
							this.isWriting = true;
						}
					}

					// run the ones that can run in this context
					if(this.IsWriting) {
						// this is simple, we always give priority to writes. lets see if there are any

						if(this.ThreadLockInProgress) {
							writes = writes.Where(this.ThreadLockedFilter).ToArray();
						}

						Ticket ticket = writes.OrderBy(t => t.TimeStamp).FirstOrDefault();

						// this will sleep and continue once the ticket is completed
						this.StartWritingTicket(ticket);
					}
				} finally {
					lock(this.locker) {
						this.isWriting = false;
					}
				}

				// refresh our writes request status
				var remainingWites = this.requestedWrites.Values.ToArray();

				// determine if we have any usable writes
				hasWritesRequested = this.WritesRemaining(remainingWites);

				// run the reads in parallel while there are no write requests or if they can run in the crrent context
				if(!hasWritesRequested) {
					// we can allow the reads that work in the current context

					var reads = this.requestedReads.Values.ToArray();

					if(this.ThreadLockInProgress) {
						reads = reads.Where(this.ThreadLockedFilter).ToArray();
					}

					foreach(Ticket request in reads) {
						this.requestedReads.RemoveSafe(request.Id);

						request.Start();
					}
				}

				remainingWites = this.requestedWrites.Values.ToArray();

				// determine if we have any usable writes
				hasWritesRequested = this.WritesRemaining(remainingWites);

				// if there is nothing, we sleep to save resources until more requests come in
				if(hasWritesRequested) {
					loop = true;
				} else {

					bool empty = true;

					lock(this.mainCollectionsLocker) {
						empty = this.requestedReads.IsEmpty && this.requestedWrites.IsEmpty;
					}

					if(empty) {
						this.resetEvent.Wait(TimeSpan.FromSeconds(1));
						this.resetEvent.Reset();
					}
				}

			} while(loop);
		}

		private void StartWritingTicket(Ticket ticket) {
			if(ticket != null) {

				ticket.OnCompleted += () => {
					// we are done, lets remove it
					this.requestedWrites.RemoveSafe(ticket.Id);

					// and trigger the completion
					this.writeResetEvent.Set();
				};

				ticket.Start();

				// for writes, we completely free until it is completed
				if(this.TheadAllowed && this.HasActiveToken) {
					this.writeResetEvent.Wait(this.ActiveToken);
				} else {
					this.writeResetEvent.Wait();
				}
				this.writeResetEvent.Reset();
			}
		}

		public (K result, bool completed) ScheduleRead<K>(IWalletProvider walletProvider, Func<IWalletProvider, K> action, bool allowWait = true) {

			using(Ticket ticket = new Ticket()) {

				if(this.CanRead) {
					return (this.PerformRead(walletProvider, action, ticket), true);
				}

				if(!allowWait) {
					return (default, false);
				}

				// ok, if we get here, its locked. no choice but to wait
				lock(this.mainCollectionsLocker) {
					this.requestedReads.AddSafe(ticket.Id, ticket);
				}

				this.resetEvent.Set();

				ticket.WaitForTurn();

				return (this.PerformRead(walletProvider, action, ticket), true);
			}
		}

		private K PerformRead<K>(IWalletProvider walletProvider, Func<IWalletProvider, K> action, Ticket ticket) {

			this.ActiveReadTasks += 1;

			try {
				return action(walletProvider);
			} finally {
				this.ActiveReadTasks -= 1;

				ticket.Completed();
			}
		}

		private void PerformRead(IWalletProvider walletProvider, Action<IWalletProvider> action, Ticket ticket) {

			this.ActiveReadTasks += 1;

			try {
				action(walletProvider);
			} finally {
				this.ActiveReadTasks -= 1;

				ticket.Completed();
			}
		}

		public K ScheduleWrite<K>(IWalletProvider walletProvider, Func<IWalletProvider, K> action) {
			using(Ticket ticket = new Ticket()) {

				lock(this.mainCollectionsLocker) {
					this.requestedWrites.AddSafe(ticket.Id, ticket);
				}

				this.resetEvent.Set();

				ticket.WaitForTurn();

				K result;

				try {
					result = action(walletProvider);
				} finally {
					ticket.Completed();
				}

				return result;
			}
		}

		private void ThreadLockTimeoutAction() {

			while(this.timeoutActive) {
				bool locked = Interlocked.CompareExchange(ref this.inLock, 0, 0) != 0;

				if(locked) {

					if(this.lockExpiration.HasValue && DateTime.UtcNow > this.lockExpiration.Value) {
						// ok, this is a timeout. lets call the task cancellation
						this.timeoutCancellationSource?.Cancel();
						this.lockExpiration = null;
					}
				}

				this.timeoutTimerResetEvent.Wait(locked ? timeoutLocked : timeoutNotLocked);
				this.timeoutTimerResetEvent.Reset();
			}
		}

		public void PerformThreadLock(IWalletProvider walletProvider, Action<IWalletProvider, CancellationToken> action, int timeout = 60) {

			using(CancellationTokenSource source = new CancellationTokenSource()) {

				try {
					this.timeoutCancellationSource = source;
					this.lockExpiration = DateTime.UtcNow + TimeSpan.FromSeconds(timeout);

					Interlocked.Increment(ref this.inLock);

					this.timeoutTimerResetEvent.Set();

					try {
						this.ScheduleWrite(walletProvider, prov => {
							this.CurrentActiveThread = Thread.CurrentThread.ManagedThreadId;
							this.lockThreadStack.Push(this.CurrentActiveThread);
							this.token = source.Token;
						});

						action(walletProvider, source.Token);

					} finally {
						if(!this.IsLocked) {

							// should be it, but just in case we set it explicitely so we can close it all off
							lock(this.locker) {
								this.CurrentActiveThread = Thread.CurrentThread.ManagedThreadId;
							}

							try {
								this.ScheduleWrite(walletProvider, prov => {
									this.CurrentActiveThread = 0;
									this.lockThreadStack.Clear();
									this.token = null;
								});
							} catch {
								// thats bad, we will force it
								lock(this.locker) {
									this.CurrentActiveThread = 0;
									this.lockThreadStack.Clear();
									this.token = null;
								}
							}
						}
					}

				} catch(OperationCanceledException opex) {
					Log.Error(opex, "Thread Exclusion operation has timed out. Force disposed of the lock!");
				} finally {
					this.timeoutCancellationSource = null;
					this.lockExpiration = null;
					
					Interlocked.Decrement(ref this.inLock);
				}
			}

		}

		public void AllowChildThreadLock(Action action) {
			// make the current thread the exclusive access one

			lock(this.locker) {
				this.lockThreadStack.Push(this.CurrentActiveThread);
				this.CurrentActiveThread = Thread.CurrentThread.ManagedThreadId;
			}

			try {
				action();
			} finally {
				lock(this.locker) {
					this.CurrentActiveThread = this.lockThreadStack.Pop();
				}
			}
		}

		public void RequestFriendlyAccess(Action action) {

			int threadId = Thread.CurrentThread.ManagedThreadId;

			// add it if there is a transaction or not
			lock(this.locker) {
				if(!this.friendlyThreads.Contains(threadId)) {
					this.friendlyThreads.Add(threadId);
				}
			}

			try {
				action();
			} finally {
				this.RemoveFriendlyThread(threadId);
			}
		}

		public void WhitelistFriendlyThread(int threadId) {
			if((threadId == 0) || !this.ThreadLockInProgress) {
				return;
			}

			lock(this.locker) {
				if(!this.friendlyThreads.Contains(threadId)) {
					this.friendlyThreads.Add(threadId);
				}
			}
		}

		public void RemoveFriendlyThread(int threadId) {

			lock(this.locker) {
				if(this.friendlyThreads.Contains(threadId)) {
					this.friendlyThreads.Remove(threadId);
				}
			}
		}

		public class Ticket : IDisposable {
			public bool IsDispose { get; private set; }

			public void Dispose() {
				if(!this.IsDispose) {
					this.IsDispose = true;
					this.resetEvent?.Dispose();
				}
			}

			private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

			public Ticket() {
				this.ThreadId = Thread.CurrentThread.ManagedThreadId;
				this.TimeStamp = DateTime.UtcNow;
			}

			public Guid Id { get; } = Guid.NewGuid();
			public int ThreadId { get; }
			public DateTime TimeStamp { get; }

			public void WaitForTurn() {

				if(!this.resetEvent.Wait(TimeSpan.FromSeconds(60))) {
					// ok, seems we timeout, we will simply signify we are not ready
					throw new NotReadyForProcessingException();
				}
			}

			public void Start() {
				this.resetEvent.Set();
			}

			public void Completed() {
				this.OnCompleted?.Invoke();
			}

			public event Action OnCompleted;
		}
	}
}