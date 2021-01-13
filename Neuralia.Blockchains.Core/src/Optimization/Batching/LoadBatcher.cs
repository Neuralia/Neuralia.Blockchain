using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Pools;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	
	/// <summary>
	/// A tool to prevent potential logging contention by batching them together and releasing them under lower load
	/// </summary>
	public sealed class LoadBatcher : IDisposableExtended {
		
		private Timer pollingTimer;

		/// <summary>
		/// maximum number if instances over which we consider to be in overload
		/// </summary>
		public const int OVERLOAD_FACTOR = 100;
		
		/// <summary>
		/// rate of increase factor where we are in danger of overload
		/// </summary>
		public const int OVERLOAD_INCREASE_RATE = 5;
		
		private static readonly TimeSpan PollRate = TimeSpan.FromSeconds(10);
		private bool Running => Interlocked.CompareExchange(ref this.users, 0, 0) != 0;

		private ConcurrentDictionary<string, IBatchingProviderBase> providers = new ConcurrentDictionary<string, IBatchingProviderBase>();

		public T GetProvider<T>(string key) where T  :class, IBatchingProviderBase{
			return this.providers[key] as T;
		}
		
		public void Start() {

			var startingCount = Interlocked.CompareExchange(ref this.users, 0, 0);

			if(startingCount == 0) {
				NLog.Default.Information($"Starting load batcher for providers [{string.Join(',', providers.Keys)}].");

				foreach(var entry in this.providers) {
					entry.Value.SetRunning(true);
				}
			} else {
				NLog.Default.Information($"Incrementing load batcher ref count from {startingCount} for providers [{string.Join(',', providers.Keys)}].");
			}

			Interlocked.Increment(ref this.users);
			this.SetTimer();
		}

		public async Task Stop() {
			
			var startingCount = Interlocked.CompareExchange(ref this.users, 0, 0);

			Interlocked.Decrement(ref this.users);

			if(!this.Running) {
				try {
					this.StopTimer();
				} catch {

				}

				foreach(var entry in this.providers) {
					entry.Value.SetRunning(false);
				}

				try {
					await Flush(true, true).ConfigureAwait(false);
				} catch {

				}
				NLog.Default.Information($"Stopped load batcher for providers [{string.Join(',', providers.Keys)}].");
			} else {
				NLog.Default.Information($"Decrementing load batcher ref count from {startingCount} for providers [{string.Join(',', providers.Keys)}].");
			}
		}
		
		private void SetTimer() {

			this.StopTimer();
			this.pollingTimer = new Timer(async state => {

				try {
					await Flush().ConfigureAwait(false);
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to flush messages on timer event");
				}

			}, this, PollRate, PollRate);
		}

		private void StopTimer() {
			try {
				this.pollingTimer?.Dispose();
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to dispose timer");
			} finally {
				this.pollingTimer = null;
			}
		}

		private long users = 0;
		
		private long previousInstancesSnapshot = 0;
		private long currentInstancesSnapshot = 0;
		private long currentInstances = 0;
		

		public void IncrementInstances() {
			Interlocked.Increment(ref this.currentInstances);
		}
		
		public void DecrementInstances() {
			Interlocked.Decrement(ref this.currentInstances);
		}
		
		private void TakeSnapshot() {
			this.previousInstancesSnapshot = this.currentInstancesSnapshot;
			this.currentInstancesSnapshot = Interlocked.CompareExchange(ref this.currentInstances, 0,0);
		}
		
		/// <summary>
		/// if the system is not in overloaded state, we flush the message cache, otherwise we hold on to it a bit more until the load is more acceptable
		/// </summary>
		public async Task Flush(bool force = false, bool finishing = false) {
			
			// first determine if we have an overload
			this.TakeSnapshot();

			if(!force) {
				if(this.currentInstancesSnapshot > OVERLOAD_FACTOR) {
					// it is too heavy, better wait a bit
					return;
				}

				double rateOfIncrease = this.currentInstancesSnapshot;

				if(this.previousInstancesSnapshot != 0) {
					rateOfIncrease /= this.previousInstancesSnapshot;
				}

				if(rateOfIncrease > OVERLOAD_INCREASE_RATE) {
					// Although we are bellow the overload rate, the rate of increase is too fast, we will wait a bit more.
					return;
				}
			}

			if(this.providers.Any(p => p.Value.HasEntries)) {
				
				try {
					this.StopTimer();
					
					foreach(var entry in this.providers) {
						if(!entry.Value.HasEntries) {
							NLog.Default.Information($"Batching Provider {entry.Key} has no entries");
						} else {
							NLog.Default.Information($"Flushing load batching Provider {entry.Key}");

							try {
								await entry.Value.Flush(finishing).ConfigureAwait(false);
							} catch(Exception ex) {
								NLog.Default.Error(ex, $"Failed to flush provider {entry.Key}.");
							}
						}
					}
				} finally {
					if(!finishing) {
						this.SetTimer();
					}
				}
			}
			
		}

		

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				this.Stop().WaitAndUnwrapException();

				this.IsDisposed = true;
			}
		}

		~LoadBatcher() {
			this.Dispose(false);
		}

	#endregion

	#region Singleton

		static LoadBatcher() {
		}

		private LoadBatcher() {

			LoggingBatcherProvider loggingBatcherProvider = new LoggingBatcherProvider();
			this.providers.TryAdd(loggingBatcherProvider.Key, loggingBatcherProvider);
			
			OperationBatchingProvider operationBatchingProvider = new OperationBatchingProvider();
			this.providers.TryAdd(operationBatchingProvider.Key, operationBatchingProvider);
		}

		public static LoadBatcher Instance { get; } = new LoadBatcher();

	#endregion
	}
}