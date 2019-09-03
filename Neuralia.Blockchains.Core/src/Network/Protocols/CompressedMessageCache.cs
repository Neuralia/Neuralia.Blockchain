using System;
using Microsoft.Extensions.Caching.Memory;

namespace Neuralia.Blockchains.Core.Network.Protocols {

	/// <summary>
	///     A special cache to store compressed messages by their uncompressed hash. this way, we can reuse messages if they
	///     are sent multiple times
	/// </summary>
	public sealed class CompressedMessageCache {

		public const long MAXIMUM_CACHE_SIZE = 100000000; // 100 megabytes

		//https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-2.1
		private readonly IMemoryCache cache;
		private readonly object locker = new object();

		private int usedSize;

		public void AddMessageEntry(MessageInstance messageInstance) {

			messageInstance.IsCached = (this.usedSize + messageInstance.Size) < MAXIMUM_CACHE_SIZE;

			if(messageInstance.IsCached) {
				MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()

					// Keep in cache for this time, reset time if accessed.
					.SetAbsoluteExpiration(TimeSpan.FromHours(1)).SetSlidingExpiration(TimeSpan.FromMinutes(1)).RegisterPostEvictionCallback((key, value, reason, state) => {
						//TODO: do something when the item is evicted from cache     
						// lets clean up its memory
						if(value is MessageInstance instance) {
							this.usedSize -= instance.Size;
							instance.Dispose();
						}
					});

				lock(this.locker) {
					this.cache.Set(messageInstance.Hash, messageInstance, cacheEntryOptions);
					this.usedSize += messageInstance.Size;
				}
			}
		}

		public bool Exists(long hash) {
			lock(this.locker) {
				return this.cache.TryGetValue(hash, out MessageInstance message);
			}
		}

		public MessageInstance Get(long hash) {
			lock(this.locker) {
				if(!this.cache.TryGetValue(hash, out MessageInstance message)) {
					return null;
				}

				return message;
			}
		}

	#region Singleton

		static CompressedMessageCache() {
		}

		private CompressedMessageCache() {
			MemoryCacheOptions options = new MemoryCacheOptions();

			//options.ExpirationScanFrequency = TimeSpan.FromMilliseconds(500);

			this.cache = new MemoryCache(options);
		}

		public static CompressedMessageCache Instance { get; } = new CompressedMessageCache();

	#endregion

	}

}