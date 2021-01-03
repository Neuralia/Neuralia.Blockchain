using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Network {

	/// <summary>
	///     a very simple and light weight but effective rate limiting controller
	/// </summary>
	public sealed class IPMarshall {

		public enum QuarantineReason {
			Cleared,
			RateLimited,
			ConnectionBroken,
			FailedHandshake,
			PermanentBan,
			AppSettingsBlacklist,
			ValidationFailed,
			DynamicBlacklist,
			NonConnectable
		}

		public const string TAG = "[" + nameof(IPMarshall) + "]";
		private readonly ConcurrentDictionary<IPAddress, QuarantinedInfo> blacklist = new();
		private readonly int blacklistStrikes = 5;
		private readonly TimeSpan cleanupPeriod = TimeSpan.FromSeconds(20);
		private readonly int connectionRefusalStrikes = 100;
		private readonly ConcurrentDictionary<IPAddress, QuarantinedInfo> greylist = new();
		private readonly ConcurrentDictionary<IPAddress, (bool whitelisted, AppSettingsBase.WhitelistedNode.AcceptanceTypes acceptanceType)> isWhiteList = new();
		private readonly TimeSpan minPeriodBetweenConnections = TimeSpan.FromSeconds(10);
		private readonly object NextClearanceCheckMutex = new();
		private readonly int rateLimitGraceStrikes;
		private readonly int rateLimitStrikes = 5;
		private readonly TimeSpan ratLimitTimePenalty = TimeSpan.FromMinutes(5);
		private readonly ConcurrentDictionary<IPAddress, QuarantinedInfo> watchlist = new();

		private DateTime NextCleanCheck = DateTimeEx.CurrentTime;

		/// <summary>
		///     Check an entry and ensure that it is within the rate limiting limit. return true if it can connect, otherwise false
		///     to reject the connection
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public bool RequestIncomingConnectionClearance(IPAddress ip) {

			if(this.IsWhiteList(ip, out AppSettingsBase.WhitelistedNode.AcceptanceTypes acceptanceType)) {
				NLog.LoggingBatcher.Information($"{TAG} ignoring acceptanceType", NLog.LoggerTypes.Connections);

				return true;
			}

			DateTime now = DateTimeEx.CurrentTime;

			bool accepted = false;

			// should we perform a cleaning?
			this.PeriodicWatchListsUpdate(now);

			// return early if blacklisted
			QuarantinedInfo info = null;

			if(this.blacklist.TryGetValue(ip, out info)) {
				Interlocked.Increment(ref info.refusalCount);

				return false;
			}

			//rate limiter logic
			if(this.watchlist.TryGetValue(ip, out info)) {
				lock(info) {
					accepted = info.Expiry < now;

					if(accepted) {

						info.Expiry = now + this.minPeriodBetweenConnections;
						info.rateLimitStrikes = 0;
						info.rateLimitGraceStrikes = 0;

					} else if(info.rateLimitGraceStrikes < this.rateLimitGraceStrikes) {
						info.rateLimitGraceStrikes++;
						accepted = true;
					} else {
						info.rateLimitStrikes++;

						if(info.rateLimitStrikes >= this.rateLimitStrikes) {

							this.watchlist.TryRemove(ip, out _);

							// that's it, this peer is blacklist
							if(!this.blacklist.TryAdd(ip, info)) {
								NLog.LoggingBatcher.Error($"{TAG}: unexpectedly already in blacklist", NLog.LoggerTypes.Connections);
							}

							info.blacklistStrikes++;

							if(info.blacklistStrikes >= this.blacklistStrikes) {
								NLog.LoggingBatcher.Warning($"{TAG} Permanently banning {ip}: {info.blacklistStrikes} blacklist strikes" + ", consider adding this ip to your firewall.", NLog.LoggerTypes.Connections);
								info.Reason = QuarantineReason.PermanentBan;
								info.Expiry = DateTimeEx.MaxValue;
							} else {
								info.Reason = QuarantineReason.RateLimited;
								info.Expiry = DateTimeEx.CurrentTime.Add(this.ratLimitTimePenalty);
							}
						}
					}
				}
			} else {
				//new ip, add it to watchlist
				info = new QuarantinedInfo {Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};

				if(!this.watchlist.TryAdd(ip, info)) {
					NLog.LoggingBatcher.Warning($"{TAG} Node {ip} is already in watchlist ({info.Reason}, expiry {info.Expiry}), this is an unexpected race condition, not very worrying..", NLog.LoggerTypes.Connections);
				}

				accepted = true;
			}

			return accepted;
		}

		public bool IsWhiteList(IPAddress ip, out AppSettingsBase.WhitelistedNode.AcceptanceTypes acceptanceType) {
			acceptanceType = default;

			if(!this.isWhiteList.TryGetValue(ip, out var pair)) {
				if((GlobalSettings.ApplicationSettings != null) && (GlobalSettings.ApplicationSettings.Whitelist != null)) {
					lock(GlobalSettings.ApplicationSettings.Whitelist) {
						foreach(AppSettingsBase.WhitelistedNode node in GlobalSettings.ApplicationSettings.Whitelist) {
							acceptanceType = node.AcceptanceType;

							if(node.CIDR) {
								if(IPUtils.IsIPV4InCIDRRange(ip, node.Ip)) {
									NLog.LoggingBatcher.Information($"{TAG} {ip} matches whitelisted CIDR IP range '{node.Ip}', AcceptanceType: '{node.AcceptanceType}'.");
									this.isWhiteList.TryAdd(ip, (true, acceptanceType));

									return true;
								}
							} else if(IPAddress.TryParse(node.Ip, out IPAddress? ipAddress)) {
								if(ipAddress.MapToIPv6().Equals(ip.MapToIPv6())) {
									NLog.LoggingBatcher.Information($"{TAG} {ip} has a match in the whitelist, AcceptanceType: '{node.AcceptanceType}'");
									this.isWhiteList.TryAdd(ip, (true, acceptanceType));

									return true;
								}
							}
						}
					}
				}

				this.isWhiteList.TryAdd(ip, (false, acceptanceType));

				return false;
			}

			acceptanceType = pair.Item2;

			return pair.Item1;
		}

		private void PeriodicWatchListsUpdate(DateTime now, bool force = false) {
			bool shouldUpdate = force;

			if(!shouldUpdate) {
				lock(this.NextClearanceCheckMutex) {
					shouldUpdate = this.NextCleanCheck < now;
				}
			}

			if(shouldUpdate) {
				foreach(KeyValuePair<IPAddress, QuarantinedInfo> expired in this.blacklist.Where(e => e.Value.Expiry < now).ToArray()) {
					lock(expired.Value) {
						if(expired.Value.refusalCount >= this.connectionRefusalStrikes) {
							NLog.LoggingBatcher.Warning($"{TAG} Permanently banning {expired.Key}: {expired.Value.refusalCount} refused connection attempts" + ", consider adding this ip to your firewall.");
							expired.Value.Reason = QuarantineReason.PermanentBan;
							expired.Value.Expiry = DateTimeEx.MaxValue; //won't enter in this scope anymore

							continue;
						}

						this.blacklist.TryRemove(expired.Key, out QuarantinedInfo removed);
						removed.Reason = QuarantineReason.Cleared;
						removed.Expiry = now.Add(this.minPeriodBetweenConnections);

						if(!this.watchlist.TryAdd(expired.Key, removed)) {
							NLog.LoggingBatcher.Error($"{TAG}: unexpectedly already in watchlist");
						}
					}
				}

				lock(this.NextClearanceCheckMutex) {
					this.NextCleanCheck = DateTimeEx.CurrentTime.Add(this.cleanupPeriod);
				}
			}
		}

		public bool IsQuarantined(IPAddress ip, out DateTime expiry, out QuarantineReason reason) {
			this.PeriodicWatchListsUpdate(DateTimeEx.CurrentTime, true); //we need up to date results

			if(this.blacklist.TryGetValue(ip, out QuarantinedInfo info)) {
				lock(info) {
					expiry = info.Expiry;
					reason = info.Reason;
				}

				return true;
			}

			reason = QuarantineReason.Cleared;
			expiry = DateTimeEx.MinValue;

			return false;
		}

		public bool IsQuarantined(IPAddress ip) {
			return this.IsQuarantined(ip, out _, out _);
		}

		public bool IsQuarantined(IPAddress ip, out DateTime expiry) {
			return this.IsQuarantined(ip, out expiry, out _);
		}
		

		public void Quarantine(IPAddress ip, QuarantineReason reason, DateTime expiry, string details = "", double graceStrikes = 0.0, TimeSpan graceObservationPeriod = default) {
			if(this.IsWhiteList(ip, out AppSettingsBase.WhitelistedNode.AcceptanceTypes acceptanceType)) {

				NLog.LoggingBatcher.Error($"{TAG} Trying to Quarantine whitelisted node {ip}, aborting");

				return;
			}

			this.PeriodicWatchListsUpdate(DateTimeEx.CurrentTime, true);

			QuarantinedInfo info = null;

			if(this.blacklist.TryGetValue(ip, out info)) {
				NLog.LoggingBatcher.Warning($"{TAG} Node {ip} is already blacklisted ({info.Reason}, expiry {info.Expiry}), updating...");
			}

			if(graceStrikes > 0) {
				if(!this.greylist.TryGetValue(ip, out info)) {
					info = new QuarantinedInfo {Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};

					if(!this.greylist.TryAdd(ip, info)) {
						NLog.LoggingBatcher.Warning($"{TAG} Node {ip} is already in greylist ({info.Reason}, expiry {info.Expiry}), this is an unexpected race condition, not very worrying..");
					}
				}

				if(!info.WarningEvents.TryGetValue(reason, out QuarantinedInfo.Events strikes)) {
					strikes = new QuarantinedInfo.Events();

					if(!info.WarningEvents.TryAdd(reason, strikes)) {
						NLog.LoggingBatcher.Warning($"{TAG} Node {ip} is already in warning events ({info.Reason}, expiry {info.Expiry}), this is an unexpected race condition, not very worrying..");
					}
				}

				DateTime now = DateTimeEx.CurrentTime;

				strikes.AddEvent(now);

				if(graceObservationPeriod == default) {
					graceObservationPeriod = TimeSpan.MaxValue;
				}

				double count = strikes.CountEventsWithinObservationPeriod(now, graceObservationPeriod);

				string message = $"{TAG} {nameof(this.Quarantine)}: quarantine warnings strikes for ip {ip} and reason {reason} is now {count:0.00} out of {graceStrikes} over a {graceObservationPeriod} period";

				if(count <= graceStrikes) {
					NLog.LoggingBatcher.Verbose($"{message}, tolerating...");

					return;
				}

				NLog.LoggingBatcher.Verbose($"{message}, blacklisting...");
				this.greylist.TryRemove(ip, out _);
			}

			if(this.watchlist.TryGetValue(ip, out info)) {
				this.watchlist.TryRemove(ip, out _);
			} else {
				info = new QuarantinedInfo {Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};
			}

			if(!this.blacklist.TryAdd(ip, info)) {
				info = this.blacklist[ip];
			}

			info.Expiry = expiry;
			info.Reason = reason;

			this.LogQuarantine(ip, info, details);
		}

		private void LogQuarantine(IPAddress ip, QuarantinedInfo info, string details) {
			string reason = $"{info.Reason}";

			if(details.Length > 0) {
				reason = $"{reason}: {details}";
			}

			NLog.LoggingBatcher.Verbose($"{TAG} Placing {ip} in quarantine ({reason}), {info.blacklistStrikes} strikes, expires at {info.Expiry}.");
		}

		private class QuarantinedInfo {

			public readonly ConcurrentDictionary<QuarantineReason, Events> WarningEvents = new();
			public int blacklistStrikes;
			public DateTime Expiry = DateTimeEx.MinValue;

			public int rateLimitGraceStrikes;
			public int rateLimitStrikes;
			public QuarantineReason Reason = QuarantineReason.Cleared;
			public int refusalCount;

			public class Events {
				private readonly object eventsMutex = new();
				private FixedQueue<DateTime> events { get; } = new(100);

				public void AddEvent(DateTime time) {
					lock(this.eventsMutex) {
						this.events.Enqueue(time);
					}
				}

				public double CountEventsWithinObservationPeriod(DateTime referenceTime, TimeSpan observationPeriod, double outOfTimeSpanBase = 0.95) {
					double value = 0.0;

					IEnumerable<DateTime> eventsCopy;

					lock(this.eventsMutex) {
						eventsCopy = this.events.ToImmutableArray();
					}

					foreach(DateTime timestamp in eventsCopy) {
						if(timestamp < referenceTime.Subtract(observationPeriod)) {
							value += 1;
						} else //out of observation period
						{
							value += Math.Pow(outOfTimeSpanBase, Math.Max(1.0, (referenceTime - timestamp).TotalSeconds));
						}
					}

					return value;
				}
			}
		}

	#region Singleton

		static IPMarshall() {
		}

		public IPMarshall(int rateLimitStrikes = 5, int rateLimitGraceStrikes = 0, int blacklistStrikes = 5, int connectionRefusalStrikes = 100, double minPeriodBetweenConnections = 10, double ratLimitTimePenalty = 5 * 60, double cleanupPeriod = 20) {
			this.rateLimitStrikes = rateLimitStrikes;
			this.rateLimitGraceStrikes = rateLimitGraceStrikes;
			this.blacklistStrikes = blacklistStrikes;
			this.connectionRefusalStrikes = connectionRefusalStrikes;
			this.minPeriodBetweenConnections = TimeSpan.FromSeconds(minPeriodBetweenConnections);
			this.ratLimitTimePenalty = TimeSpan.FromSeconds(ratLimitTimePenalty);
			this.cleanupPeriod = TimeSpan.FromSeconds(cleanupPeriod);

			if(GlobalSettings.ApplicationSettings != null) {
				foreach(AppSettingsBase.Node node in GlobalSettings.ApplicationSettings.Blacklist) {
					if(IPAddress.TryParse(node.Ip, out IPAddress? ip)) {
						this.blacklist.TryAdd(ip, new QuarantinedInfo {Expiry = DateTimeEx.MaxValue, Reason = QuarantineReason.AppSettingsBlacklist});
					}
				}
			}
		}

		public static IPMarshall Instance { get; } = new();

		public static IPMarshall ValidationInstance { get; } = new(0, 17 // 3 connections per validation period * 2 tries * 3 for CGNAT shared IPs = 18, so 17 after the first one
			, 5, 100 // once in blacklist, the number of times befor we permanently blacklist you
			, 60 // The validation period duration TODO: use some application-wide constant 
			, (7 * 24 * 60 * 60) - (5 * 60) //7 days - 5min
		);

	#endregion

	}

}