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
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	public interface IResourceAccessScheduler<T> : IDisposableExtended {
		void ScheduleRead(Action<T> action);
		K ScheduleRead<K>(Func<T, K> action);
		
		void ScheduleWrite(Action<T> action);
		K ScheduleWrite<K>(Func<T, K> action);
	}

	/// <summary>
	///     This class is meant to be an insulator of all accesses to the filesystem. The read access to the index
	///     are thread safe, but only if there is no
	///     write at the same time. Thus, we have to ensure that a write never happens during reads, but multiple reads can
	///     happen at the same time. this scheduler does exactly that.
	///     it is meant to be used statically, one per chain and is thread safe.
	/// </summary>
	public class SimpleResourceAccessScheduler<T> : IResourceAccessScheduler<T> {


		private readonly ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim();

		public SimpleResourceAccessScheduler(T fileProvider) {
			this.FileProvider = fileProvider;
		}

		public T FileProvider { get; }

		public void ScheduleRead(Action<T> action) {
			this.readerWriterLock.EnterReadLock();
			try {
				action(this.FileProvider);
			}
			finally
			{
				this.readerWriterLock.ExitReadLock();
			}
		}

		public K ScheduleRead<K>(Func<T, K> action) {
			
			this.readerWriterLock.EnterReadLock();
			try {
				return action(this.FileProvider);
			}
			finally
			{
				this.readerWriterLock.ExitReadLock();
			}
		}

		public void ScheduleWrite(Action<T> action) {
			bool res = readerWriterLock.IsWriteLockHeld;
			this.readerWriterLock.EnterWriteLock();
			try
			{
				action(this.FileProvider);
			}
			finally
			{
				this.readerWriterLock.ExitWriteLock();
			}
		}

		public K ScheduleWrite<K>(Func<T, K> action) {
			bool res = readerWriterLock.IsWriteLockHeld;
			this.readerWriterLock.EnterWriteLock();
			try
			{
				return action(this.FileProvider);
			}
			finally
			{
				this.readerWriterLock.ExitWriteLock();
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
						this.readerWriterLock?.Dispose();
					} catch(Exception ex) {
						Log.Verbose("error occured", ex);
					}

				} catch(Exception ex) {

				} 
			}
			this.IsDisposed = true;
		}

		~SimpleResourceAccessScheduler() {
			this.Dispose(false);
		}

	#endregion
	}
}