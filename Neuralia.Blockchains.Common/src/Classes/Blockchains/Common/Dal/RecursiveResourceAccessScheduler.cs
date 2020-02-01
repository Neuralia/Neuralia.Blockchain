using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	public interface IRecursiveResourceAccessScheduler<T> : IDisposableExtended {
		
		bool ScheduleReadNoWait(Action<T> action);
		
		bool ScheduleRead(Action<T> action, int timeout = 60);
		(K result, bool success) ScheduleRead<K>(Func<T, K> action, int timeout = 60);
		
		bool ScheduleWrite(Action<T> action, int timeout = 60);
		(K result, bool success) ScheduleWrite<K>(Func<T, K> action, int timeout = 60);
		
		bool ScheduleRead(Action<T> action, TimeSpan timeout);
		(K result, bool success) ScheduleRead<K>(Func<T, K> action, TimeSpan timeout);
		
		bool ScheduleWrite(Action<T> action, TimeSpan timeout);
		(K result, bool success)ScheduleWrite<K>(Func<T, K> action, TimeSpan timeout);

		bool ThreadLockInProgress { get; }
		int CurrentActiveThread { get; }
		bool IsActiveTransactionThread(int lookupThreadId);
		bool IsCurrentActiveTransactionThread { get; }
	}

	/// <summary>
	///     this is a more elaborate resource exclusion which permits recursive access.
	/// </summary>
	public class RecursiveResourceAccessScheduler<T> : IRecursiveResourceAccessScheduler<T> {


		private int threadId = 0;
		
		private readonly ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

		public RecursiveResourceAccessScheduler(T component) {
			this.Component = component;
		}

		public T Component { get; }

		public bool ScheduleRead(Action<T> action, int timeout = 60) {
			return this.ScheduleRead(action, TimeSpan.FromSeconds(timeout));
		}

		public bool ScheduleRead(Action<T> action, TimeSpan timeout) {
			
			if(!this.readerWriterLock.TryEnterReadLock((int)timeout.TotalMilliseconds)) {
				return false;
			}

			try {
				action(this.Component);

				return true;

			} finally {
				this.readerWriterLock.ExitReadLock();
			}
		}

		public bool ScheduleReadNoWait(Action<T> action) {
			return this.ScheduleRead(action, TimeSpan.FromMilliseconds(1));
		}
		public (K result, bool success) ScheduleReadNoWait<K>(Func<T, K> action) {
			return this.ScheduleRead(action, TimeSpan.FromMilliseconds(1));
		}
		
		public (K result, bool success) ScheduleRead<K>(Func<T, K> action, int timeout = 60) {
			return this.ScheduleRead(action, TimeSpan.FromSeconds(timeout));
		}

		public (K result, bool success) ScheduleRead<K>(Func<T, K> action, TimeSpan timeout) {

			if(!this.readerWriterLock.TryEnterReadLock((int)timeout.TotalMilliseconds)) {
				return (default, false);
			}

			try {
				return (action(this.Component), true);
			} finally {
				this.readerWriterLock.ExitReadLock();
			}
		}
		
		public bool ScheduleWrite(Action<T> action, int timeout = 60) {
			return this.ScheduleWrite(action, TimeSpan.FromSeconds(timeout));
		}

		public bool ScheduleWrite(Action<T> action, TimeSpan timeout) {
			
			if(!this.readerWriterLock.TryEnterWriteLock((int)timeout.TotalMilliseconds)) {
				return false;
			}

			//TODO: this is not atomic with the above enter write. is it a problem?  can we find a better way
			Interlocked.Exchange(ref this.threadId, Thread.CurrentThread.ManagedThreadId);
			try {
				action(this.Component);

				return true;

			} finally {
				Interlocked.Exchange(ref this.threadId, 0);
				this.readerWriterLock.ExitWriteLock();
			}
		}

		public (K result, bool success) ScheduleWrite<K>(Func<T, K> action, int timeout = 60) {
			return this.ScheduleWrite(action, TimeSpan.FromSeconds(timeout));
		}
		
		public (K result, bool success) ScheduleWrite<K>(Func<T, K> action, TimeSpan timeout) {
			if(!this.readerWriterLock.TryEnterWriteLock((int)timeout.TotalMilliseconds)) {
				return (default, false);
			}
			Interlocked.Exchange(ref this.threadId, Thread.CurrentThread.ManagedThreadId);
			try {
				return (action(this.Component), true);
			} finally {
				Interlocked.Exchange(ref this.threadId, 0);
				this.readerWriterLock.ExitWriteLock();
			}
		}

		public int CurrentActiveThread => Interlocked.CompareExchange(ref this.threadId, 0, 0);
		public bool ThreadLockInProgress => this.CurrentActiveThread != 0;

		public bool IsCurrentActiveTransactionThread => this.IsActiveTransactionThread(Thread.CurrentThread.ManagedThreadId);
		
		public bool IsActiveTransactionThread(int lookupThreadId) {
			return this.CurrentActiveThread == lookupThreadId;
		}

	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				try {
					try {
						this.readerWriterLock?.Dispose();
					} catch(Exception ex) {
						Log.Verbose("error occured", ex);
					}

				} catch(Exception ex) {

				} 
			}
			this.IsDisposed = true;
		}

		~RecursiveResourceAccessScheduler() {
			this.Dispose(false);
		}

	#endregion
	}
}