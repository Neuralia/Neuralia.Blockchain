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
		private readonly ConcurrentDictionary<ReadTicket, bool> activeReadTasks = new ConcurrentDictionary<ReadTicket, bool>();

		private readonly TaskFactory factory = new TaskFactory();

		/// <summary>
		///     This is the list of read requests that are differred (waiting) for a read request to happen
		/// </summary>
		public readonly ConcurrentQueue<ReadTicket> requestedReads = new ConcurrentQueue<ReadTicket>();

		/// <summary>
		///     These are the write requests awaiting to occur or are happening.
		/// </summary>
		public readonly ConcurrentQueue<Ticket> requestedWrites = new ConcurrentQueue<Ticket>();

		private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

		private readonly object mainCollectionsLocker = new object();
		public ResourceAccessScheduler(T fileProvider, IFileSystem fileSystem) : base(10) {
			this.FileProvider = fileProvider;
		}

		public T FileProvider { get; }

		public void ScheduleRead(Action<T> action) {
			this.ScheduleReadEvent(action, ticket => {
				this.requestedReads.Enqueue(ticket);
			});
		}

		public void ScheduleWrite(Action<T> action) {
			this.ScheduleEvent(action, ticket => {
				this.requestedWrites.Enqueue(ticket);
			});
		}

		protected override void ProcessLoop() {

			
			try {
				// clear the completed tasks

				// we can only start writes when all the active reads are done
				if(!this.activeReadTasks.Any()) {
					// this is simple, we always give priority to writes. lets see if there are any
					while(this.requestedWrites.TryDequeue(out Ticket ticket)) {

						try {
							Ticket ticket1 = ticket;

							if(ticket1.IsDispose) {
								continue;
							}

							Repeater.Repeat(() => {
								ticket1.action(this.FileProvider);
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
				if(this.requestedWrites.IsEmpty) {
					List<Ticket> tickets = new List<Ticket>();
					while((this.requestedWrites.IsEmpty) && this.requestedReads.TryDequeue(out ReadTicket ticket)) {
						
						// run the read in parallel. it is thread safe with other reads
						ReadTicket ticket2 = ticket;
						
						if(ticket2.IsDispose) {
							continue;
						}

						ticket.Completed += () => {
							this.activeReadTasks.RemoveSafe(ticket);
						};
						
						this.activeReadTasks.AddSafe(ticket, true);
						
						ticket.Activate();

						
					}

					// if there is nothing, we sleep to save resources until more requests come in
					bool empty = true;

					lock(this.mainCollectionsLocker) {
						empty = (this.requestedWrites.IsEmpty) && (this.requestedReads.IsEmpty);
					}

					if(empty) {
						if(this.resetEvent.Wait(TimeSpan.FromSeconds(1))) {
							this.resetEvent.Reset();
						}
					}
				}
			} catch(Exception ex) {
				Log.Error(ex, "error occured while accessing a scheduled resource.");
			}
		}

		public K ScheduleRead<K>(Func<T, K> action) {
			return this.ScheduleReadEvent(action, ticket => {
				lock(this.mainCollectionsLocker) {
					this.requestedReads.Enqueue(ticket);
				}
			});
		}

		public K ScheduleWrite<K>(Func<T, K> action) {
			return this.ScheduleEvent(action, ticket => {
				lock(this.mainCollectionsLocker) {
					this.requestedWrites.Enqueue(ticket);
				}
			});
		}

		private void ScheduleEvent(Action<T> action, Action<Ticket> enqueueAction) {
			using(Ticket ticket = new Ticket()) {
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
		}

		private K ScheduleEvent<K>(Func<T, K> action, Action<Ticket> enqueueAction) {
			using(Ticket ticket = new Ticket()) {
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
		}
		
		private void ScheduleReadEvent(Action<T> action, Action<ReadTicket> enqueueAction) {
			using(ReadTicket ticket = new ReadTicket()) {
				ticket.action = action;

				enqueueAction(ticket);

				// wake up the wait if it is sleeping
				this.resetEvent.Set();

				ticket.WaitTurn();

				try {
					ticket.action(this.FileProvider);
				} finally {
					ticket.Complete();
				}
				
				if(ticket.Error) {
					//TODO: is this the proper behavior??
					ticket.exception.Throw();
				}
			}
		}
		
		private K ScheduleReadEvent<K>(Func<T, K> action, Action<ReadTicket> enqueueAction) {
			using(ReadTicket ticket = new ReadTicket()) {
				K result = default;

				ticket.action = entry => {

					result = action(entry);
				};

				enqueueAction(ticket);

				// wake up the wait if it is sleeping
				this.resetEvent.Set();

				ticket.WaitTurn();

				try {
					ticket.action(this.FileProvider);
				} finally {
					ticket.Complete();
				}

				if(ticket.Error) {
					//TODO: is this the proper behavior??
					ticket.exception.Throw();
				}

				return result;
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
			public Action<T> action;
			public ExceptionDispatchInfo exception;

			public bool Success => this.exception == null;
			public bool Error => !this.Success;

			public void WaitCompletion() {

				this.resetEvent.Wait();
			}

			public void Complete() {
				this.resetEvent.Set();
			}
		}
		
		public class ReadTicket : IDisposable {

			public bool IsDispose { get; private set; }

			public void Dispose() {
				if(!this.IsDispose) {
					this.IsDispose = true;
					this.resetEvent?.Dispose();
				}
			}

			private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
			public Action<T> action;
			public ExceptionDispatchInfo exception;
			public event Action Completed;

			public bool Success => this.exception == null;
			public bool Error => !this.Success;

			public void WaitTurn() {

				this.resetEvent.Wait();
			}

			public void Complete() {
				this.Completed?.Invoke();
			}
			public void Activate() {
				this.resetEvent.Set();
			}
		}
	}
}