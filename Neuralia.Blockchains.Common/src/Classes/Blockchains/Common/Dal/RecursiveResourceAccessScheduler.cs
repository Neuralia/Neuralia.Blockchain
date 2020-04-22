using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	public interface IRecursiveResourceAccessScheduler<T> : IDisposableExtended {
		Task<K>                        ScheduleRead<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60);
		Task<K>                        ScheduleRead<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null);
		Task<K>                        ScheduleReadNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext);
		Task<(K result, bool success)> ScheduleReadSucceededNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null);
		Task<(K result, bool success)> ScheduleReadSucceeded<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60);
		Task<(K result, bool success)> ScheduleReadSucceeded<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null);
		Task<K>                        ScheduleWrite<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60);
		Task<K>                        ScheduleWrite<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null);
		Task<K>                        ScheduleWriteNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext);
		Task<(K result, bool success)> ScheduleWriteSucceededNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null);
		Task<(K result, bool success)> ScheduleWriteSucceeded<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60);
		Task<(K result, bool success)> ScheduleWriteSucceeded<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null);
	}

	/// <summary>
	///     this is a more elaborate resource exclusion which permits recursive access.
	/// </summary>
	public class RecursiveResourceAccessScheduler<T> : IRecursiveResourceAccessScheduler<T> {
		
		private readonly RecursiveAsyncReaderWriterLock readerWriterLock = new RecursiveAsyncReaderWriterLock();

		public RecursiveResourceAccessScheduler(T component) {
			this.Component = component;
		}

		public T Component { get; }

		public Task<K> ScheduleRead<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60) {
			return this.ScheduleRead(action, TimeSpan.FromSeconds(timeout),lockContext);
		}

		public async Task<K> ScheduleRead<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null) {

			if(action == null) {
				return default;
			}

			using(var handle = await this.readerWriterLock.ReaderLockAsync(lockContext,timeout).ConfigureAwait(false)) {

				return await action(this.Component, handle).ConfigureAwait(false);
			}
		}

		public Task<K> ScheduleReadNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext) {
			return this.ScheduleRead<K>(action, TimeSpan.FromMilliseconds(1000), lockContext);
		}
		
		public  Task<(K result, bool success)> ScheduleReadSucceededNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null) {
			return this.ScheduleReadSucceeded(action, TimeSpan.FromMilliseconds(1000), lockContext);
		}
		
		public  Task<(K result, bool success)> ScheduleReadSucceeded<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60) {
			return this.ScheduleReadSucceeded(action, TimeSpan.FromSeconds(timeout), lockContext);
		}

		public async Task<(K result, bool success)> ScheduleReadSucceeded<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null) {
			
			try {
				return (await ScheduleRead(action, timeout, lockContext).ConfigureAwait(false), true);

			} catch(LockTimeoutException ex) {
				// do nothing
				return (default, false);
			}
		}
		
		public Task<K> ScheduleWrite<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60) {
			return this.ScheduleWrite(action, TimeSpan.FromSeconds(timeout),lockContext);
		}

		public async Task<K> ScheduleWrite<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null) {

			if(action == null) {
				return default;
			}

			using var handle = await this.readerWriterLock.WriterLockAsync(lockContext, timeout).ConfigureAwait(false);

			return await action(this.Component, handle).ConfigureAwait(false);
		}

		public Task<K> ScheduleWriteNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext) {
			return this.ScheduleWrite<K>(action, TimeSpan.FromMilliseconds(1), lockContext);
		}
		
		public Task<(K result, bool success)> ScheduleWriteSucceededNoWait<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null) {
			return this.ScheduleWriteSucceeded(action, TimeSpan.FromMilliseconds(1), lockContext);
		}
		
		public Task<(K result, bool success)> ScheduleWriteSucceeded<K>(Func<T, LockContext, Task<K>> action, LockContext lockContext = null, int timeout = 60) {
			return this.ScheduleWriteSucceeded(action, TimeSpan.FromSeconds(timeout), lockContext);
		}

		public async Task<(K result, bool success)> ScheduleWriteSucceeded<K>(Func<T, LockContext, Task<K>> action, TimeSpan timeout, LockContext lockContext = null) {
			
			try {
				return (await ScheduleWrite(action, timeout, lockContext).ConfigureAwait(false), true);

			} catch(LockTimeoutException ex) {
				// do nothing
				return (default, false);
			}
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