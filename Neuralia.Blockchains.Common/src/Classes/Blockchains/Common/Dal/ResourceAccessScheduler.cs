using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	public interface IResourceAccessScheduler<T> : ILoopThread<ResourceAccessScheduler<T>> {
		void ScheduleRead(Action<T> action);
		void ScheduleWrite(Action<T> action);
	}

	/// <summary>
	///     This class is meant to be an insulator of all accesses to the filesystem. The read access to the index
	///     are thread safe, but only if there is no
	///     write at the same time. Thus, we have to ensure that a write never happens during reads, but multiple reads can
	///     happen at the same time. this scheduler does exactly that.
	///     it is meant to be used statically, one per chain and is thread safe.
	/// </summary>
	public class ResourceAccessScheduler<T> : LoopThread<ResourceAccessScheduler<T>>, IResourceAccessScheduler<T> {

		/// <summary>
		///     sicne reads are threads safe, we can run them in parallel
		/// </summary>
		private readonly List<Task> activeReadTasks = new List<Task>();

		private readonly TaskFactory factory = new TaskFactory();

		/// <summary>
		///     This is the list of read requests that are differred (waiting) for a read request to happen
		/// </summary>
		public readonly ConcurrentQueue<Ticket> requestedReads = new ConcurrentQueue<Ticket>();

		/// <summary>
		///     These are the write requests awaiting to occur or are happening.
		/// </summary>
		public readonly ConcurrentQueue<Ticket> requestedWrites = new ConcurrentQueue<Ticket>();

		private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);

		public ResourceAccessScheduler(T fileProvider, IFileSystem fileSystem) : base(10) {
			this.FileProvider = fileProvider;
		}

		public T FileProvider { get; }

		public void ScheduleRead(Action<T> action) {
			this.ScheduleEvent(action, ticket => {
				this.requestedReads.Enqueue(ticket);
			});
		}

		public void ScheduleWrite(Action<T> action) {
			this.ScheduleEvent(action, ticket => {
				this.requestedWrites.Enqueue(ticket);
			});
		}

		protected override void ProcessLoop() {

			// clear the completed tasks
			foreach(Task task in this.activeReadTasks.Where(t => t.IsCompleted).ToArray()) {
				this.activeReadTasks.Remove(task);
			}

			// we can only start writes when all the active reads are done
			if(this.activeReadTasks.Count == 0) {
				// this is simple, we always give priority to writes. lets see if there are any
				while(this.requestedWrites.TryDequeue(out Ticket ticket)) {

					try {
						Repeater.Repeat(() => {
							ticket.action(this.FileProvider);
						});
					} catch(Exception ex) {
						ticket.exception = ExceptionDispatchInfo.Capture(ex);

						//TODO: what to do here?
						Log.Error(ex, "failed to access blockchain files");
					} finally {
						ticket.Complete();
					}

				}
			}

			// run the reads in parallel while there are no write requests
			if(this.requestedWrites.Count == 0) {
				while((this.requestedWrites.Count == 0) && this.requestedReads.TryDequeue(out Ticket ticket)) {

					Ticket ticket1 = ticket;

					// run the read in parallel. it is thread safe with other reads
					Task t = this.factory.StartNew(() => {
						try {
							Repeater.Repeat(() => {
								ticket.action(this.FileProvider);
							});
						} catch(Exception ex) {
							ticket.exception = ExceptionDispatchInfo.Capture(ex);

							//TODO: what to do here?
							Log.Error(ex, "failed to access blockchain files");
						} finally {
							//warn the calling thread that the query is over, and it can continue on it's way.
							ticket.Complete();
						}
					});

					this.activeReadTasks.Add(t);
				}

				// if there is nothing, we sleep to save resources until more requests come in
				if((this.requestedWrites.Count == 0) && (this.requestedReads.Count == 0)) {
					this.resetEvent.WaitOne(TimeSpan.FromSeconds(5));
				}
			}
		}

		public K ScheduleRead<K>(Func<T, K> action) {
			return this.ScheduleEvent(action, ticket => {
				this.requestedReads.Enqueue(ticket);
			});
		}

		public K ScheduleWrite<K>(Func<T, K> action) {
			return this.ScheduleEvent(action, ticket => {
				this.requestedWrites.Enqueue(ticket);
			});
		}

		private void ScheduleEvent(Action<T> action, Action<Ticket> enqueueAction) {
			Ticket ticket = new Ticket();
			ticket.action = action;

			enqueueAction(ticket);

			// wake up the wait if it is sleeping
			this.resetEvent.Set();

			ticket.WaitCompletion();

			if(ticket.Error) {
				//TODO: is this the proper behavior??
				ticket.exception.Throw();
			}
		}

		private K ScheduleEvent<K>(Func<T, K> action, Action<Ticket> enqueueAction) {
			Ticket ticket = new Ticket();
			K result = default;

			ticket.action = entry => {

				result = action(entry);
			};

			enqueueAction(ticket);

			// wake up the wait if it is sleeping
			this.resetEvent.Set();

			ticket.WaitCompletion();

			if(ticket.Error) {
				//TODO: is this the proper behavior??
				ticket.exception.Throw();
			}

			return result;
		}

		public class Ticket {

			private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
			public Action<T> action;
			public ExceptionDispatchInfo exception;

			public bool Success => this.exception == null;
			public bool Error => !this.Success;

			public void WaitCompletion() {

				this.resetEvent.WaitOne();
			}

			public void Complete() {
				this.resetEvent.Set();
			}
		}
	}
}