using System;
using Microsoft.Extensions.Caching.Memory;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;

namespace Neuralia.Blockchains.Core.Network {

	/// <summary>
	///     A special cache used to store big messages while they go through their exchange process
	/// </summary>
	public sealed class LargeMessageCaches<T>
		where T : class, ISplitMessageEntry {

		//https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-2.1
		private readonly IMemoryCache cache;
		private readonly object locker = new object();

		public void AddEntry(T bigMessage) {

			MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()

				// Keep in cache for this time, reset time if accessed.
				.SetAbsoluteExpiration(TimeSpan.FromHours(1)).SetSlidingExpiration(TimeSpan.FromMinutes(1)).RegisterPostEvictionCallback((key, value, reason, state) => {
					//TODO: do something when the item is evicted from cache     
					// lets clean up its memory
				});

			lock(this.locker) {
				this.cache.Set(bigMessage.Hash, bigMessage, cacheEntryOptions);
			}
		}

		public bool Exists(long hash) {
			lock(this.locker) {
				return this.cache.TryGetValue(hash, out T message);
			}
		}

		public T Get(long hash) {
			lock(this.locker) {
				if(!this.cache.TryGetValue(hash, out T message)) {
					return null;
				}

				return message;
			}
		}

	#region Singleton

		static LargeMessageCaches() {
		}

		private LargeMessageCaches() {
			MemoryCacheOptions options = new MemoryCacheOptions();

			//options.ExpirationScanFrequency = TimeSpan.FromMilliseconds(500);

			this.cache = new MemoryCache(options);
		}

		public static LargeMessageCaches<ISplitMessageEntry> SendCaches { get; } = new LargeMessageCaches<ISplitMessageEntry>();

		public static LargeMessageCaches<ISplitMessageEntry> ReceiveCaches { get; } = new LargeMessageCaches<ISplitMessageEntry>();

	#endregion

	}

	public static class MessageCaches {
		public static LargeMessageCaches<ISplitMessageEntry> SendCaches => LargeMessageCaches<ISplitMessageEntry>.SendCaches;

		//TODO: eventually, receive cache could serialize to a temp file. to resume downloads if the app dies.
		public static LargeMessageCaches<ISplitMessageEntry> ReceiveCaches => LargeMessageCaches<ISplitMessageEntry>.ReceiveCaches;
	}
}