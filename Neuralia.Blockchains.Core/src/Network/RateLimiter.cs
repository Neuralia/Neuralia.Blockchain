using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Core.Network {
	
	/// <summary>
	/// a very simple and light weight but effective rate limiting controller
	/// </summary>
	public sealed class RateLimiter {

		private readonly Dictionary<string, RateLimiterInfo> limited = new Dictionary<string, RateLimiterInfo>();
		private readonly Dictionary<string, BlacklistedInfo> blacklisted = new Dictionary<string, BlacklistedInfo>();
		private static readonly TimeSpan RATE_LIMIT = TimeSpan.FromSeconds(13);
		private static readonly TimeSpan BLACKLIST_TIME = TimeSpan.FromMinutes(5);
		private const int BLACKLIST_STRIKES = 5;
		
		private readonly object locker = new object();
		
		private class RateLimiterInfo {
			public DateTime LastAcccess = DateTime.UtcNow;
			public int strikes = 0;
		}

		private class BlacklistedInfo {
			public DateTime LastAcccess = DateTime.UtcNow;
			
		}

		private DateTime CleanCheck = DateTime.UtcNow;
		
		/// <summary>
		/// Check an entry and ensure that it is within the rate limiting limit. return true if it can connect, otherwise false to reject the connection
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public bool CheckEntryCanConnect(IPAddress address) {

			NodeAddressInfo info = new NodeAddressInfo(address, NodeInfo.Unknown);
			string ip = info.Ip;
			
			bool connetable = false;
			
			lock(this.locker) {

				// should we perform a cleaning?
				if(this.CleanCheck.Add(TimeSpan.FromSeconds(20)) < DateTime.UtcNow) {
					foreach(var expired in this.blacklisted.Where(e => e.Key != ip && e.Value.LastAcccess.Add(BLACKLIST_TIME) < DateTime.UtcNow).ToArray()) {
						this.blacklisted.Remove(expired.Key);
					}

					foreach(var expired in this.limited.Where(e => e.Key != ip && e.Value.LastAcccess.Add(RATE_LIMIT) < DateTime.UtcNow).ToArray()) {
						this.limited.Remove(expired.Key);
					}
					this.CleanCheck = DateTime.UtcNow;
				}

				if(this.blacklisted.ContainsKey(ip)) {
					this.blacklisted[ip].LastAcccess = DateTime.UtcNow;
					return false;
				}

				if(this.limited.ContainsKey(ip)) {
					RateLimiterInfo limiterInfo = this.limited[ip];
					connetable = limiterInfo.LastAcccess.Add(RATE_LIMIT) < DateTime.UtcNow;
					limiterInfo.LastAcccess = DateTime.UtcNow;

					if(!connetable) {
						limiterInfo.strikes++;

						if(limiterInfo.strikes >= BLACKLIST_STRIKES) {
							// thats it, this peer is blacklisted
							if(this.blacklisted.ContainsKey(ip)) {
								this.blacklisted[ip].LastAcccess = DateTime.UtcNow;
							} else {
								this.blacklisted.Add(ip, new BlacklistedInfo());
							}
						}
					}
				} else {
					this.limited.Add(ip, new RateLimiterInfo());
					connetable = true;
				}
			}
			
			return connetable;
		}
		
	#region Singleton

		static RateLimiter() {
		}

		private RateLimiter() {
		}

		public static RateLimiter Instance { get; } = new RateLimiter();

	#endregion

	}

}