using System;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Core.Tools {

	public class ManagedTimer : ManagedTimer<object> {

		public ManagedTimer(TimeSpan startDelay, TimeSpan period) : base(startDelay, period) {
		}

		public ManagedTimer(Func<object, Task> timerEvent, TimeSpan startDelay, TimeSpan period) : base(timerEvent, startDelay, period) {
		}
	}
	/// <summary>
	/// a utility class to help improve timer behavior
	/// </summary>
	public class ManagedTimer<T> : IDisposableExtended{
		private readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();
		private Timer timer;
		private TimeSpan startDelay;
		private TimeSpan period;
		private long sessionId;

		public ManagedTimer(TimeSpan startDelay, TimeSpan period) {
			this.startDelay = startDelay;
			this.period = period;
		}

		public ManagedTimer(Func<T, Task> timerEvent, TimeSpan startDelay, TimeSpan period) : this(startDelay, period) {
			this.TimerEvent = timerEvent;
		}
		
		public Func<T, Task> TimerEvent;

		public void Start(object state = null) {
			this.Stop();
			this.timer = new Timer(this.Elapsed, state, this.startDelay, this.period);
		}

		public void Stop(TimeSpan? timeout = null) {

			if(!timeout.HasValue) {
				timeout = TimeSpan.FromSeconds(60);
			}

			lock(this.locker) {
				if(this.timer != null) {
					try {
						using ManualResetEvent waitHandle = new(false);

						if(this.timer.Dispose(waitHandle)) {
							if(!waitHandle.WaitOne(timeout.Value)) {
								throw new TimeoutException("Failed to wait for timeout stop");
							}
						}

						waitHandle.Close();
					} catch {

					}

					this.timer = null;
				}
			}
		}

		private async void Elapsed(object state) {

			long id = Interlocked.Read(ref this.sessionId);

			using(await this.locker.LockAsync().ConfigureAwait(false)) {
				if(this.timer == null || id != Interlocked.Read(ref this.sessionId)) {
					return;
				}

				try {
					this.timer.Change(Timeout.Infinite, Timeout.Infinite);

					if(this.TimerEvent != null) {
						await this.TimerEvent(state!=default?(T)state:default).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Timer exception");
				}
				finally {
					// increment the session id to avoid repeats
					Interlocked.Increment(ref this.sessionId);

					if(Interlocked.Read(ref this.sessionId) == long.MaxValue) {
						Interlocked.Exchange(ref this.sessionId, 0);
					}
					
					this.timer?.Change(this.period, this.period);
				}
			}
		}

	#region dispose

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {

				try {
					this.Stop();
				} catch(Exception ex) {
				}

				this.TimerEvent = null;
			}

			this.IsDisposed = true;
		}

		~ManagedTimer() {
			this.Dispose(false);
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}