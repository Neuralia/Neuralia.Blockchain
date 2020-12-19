using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Network {

	/// <summary>
	///     a very simple and light weight but effective rate limiting controller
	/// </summary>
	public sealed class IPMarshall
	{
		public const string TAG = "[" + nameof(IPMarshall) + "]";
		private readonly int rateLimitStrikes = 5;
		private readonly int rateLimitGraceStrikes = 0;
		private readonly int blacklistStrikes = 5;
		private readonly int connectionRefusalStrikes = 100;
		private readonly TimeSpan minPeriodBetweenConnections = TimeSpan.FromSeconds(10);
		private readonly TimeSpan ratLimitTimePenalty = TimeSpan.FromMinutes(5);
		private readonly TimeSpan cleanupPeriod = TimeSpan.FromSeconds(20);
		private readonly Dictionary<IPAddress, QuarantinedInfo> blacklist = new Dictionary<IPAddress, QuarantinedInfo>();
		private readonly Dictionary<IPAddress, QuarantinedInfo> greylist = new Dictionary<IPAddress, QuarantinedInfo>();
		private readonly Dictionary<IPAddress, QuarantinedInfo> watchlist = new Dictionary<IPAddress, QuarantinedInfo>();

		private readonly object locker = new object();

		private DateTime NextCleanCheck = DateTimeEx.CurrentTime;

		/// <summary>
		///     Check an entry and ensure that it is within the rate limiting limit. return true if it can connect, otherwise false
		///     to reject the connection
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public bool RequestIncomingConnectionClearance(IPAddress ip)
		{

			if (this.IsWhiteList(ip, out var acceptanceType))
			{
				NLog.Connections.Information($"{TAG} ignoring acceptanceType");
				return true;
			}
			
			var now = DateTimeEx.CurrentTime;

			bool accepted = false;

			lock(this.locker) {

				// should we perform a cleaning?
				this.PeriodicWatchListsUpdate(now);

				// return early if blacklisted
				QuarantinedInfo info = null;
				if(this.blacklist.TryGetValue(ip, out info)){
					info.refusalCount++;
					return false;
				}

				//rate limiter logic
				if(this.watchlist.TryGetValue(ip, out info))
				{
					accepted = info.Expiry < now;
					
					if(accepted) {
						
						info.Expiry = now.Add(this.minPeriodBetweenConnections);
						
						info.rateLimitStrikes = 0;
						info.rateLimitGraceStrikes = 0;

					} else if (info.rateLimitGraceStrikes < this.rateLimitGraceStrikes)
					{
						info.rateLimitGraceStrikes++;
						accepted = true;
					} else {
						info.rateLimitStrikes++;

						if(info.rateLimitStrikes >= this.rateLimitStrikes) {
							
							this.watchlist.Remove(ip);
							// that's it, this peer is blacklist
							if(!this.blacklist.TryAdd(ip, info))
								NLog.Connections.Error($"{TAG}: unexpectedly already in blacklist");
							info.blacklistStrikes++;
							if (info.blacklistStrikes >= this.blacklistStrikes) {
								NLog.Connections.Warning(
									$"{TAG} Permanently banning {ip}: {info.blacklistStrikes} blacklist strikes"
									+ ", consider adding this ip to your firewall.");
								info.Reason = QuarantineReason.PermanentBan;
								info.Expiry = DateTimeEx.MaxValue;
							} else {
								info.Reason = QuarantineReason.RateLimited;
								info.Expiry = DateTimeEx.CurrentTime.Add(this.ratLimitTimePenalty);
							}
						}
					}
				} else { 
					//new ip, add it to watchlist
					info = new QuarantinedInfo{Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};
					this.watchlist.Add(ip, info);
					accepted = true;
				}
			}

			return accepted;
		}

		public bool IsWhiteList(IPAddress ip, out AppSettingsBase.WhitelistedNode.AcceptanceTypes acceptanceType)
		{
			acceptanceType = default(AppSettingsBase.WhitelistedNode.AcceptanceTypes);
			if (GlobalSettings.ApplicationSettings != null)
			{
				foreach (var node in GlobalSettings.ApplicationSettings.Whitelist)
				{
					acceptanceType = node.AcceptanceType;
					
					if(node.CIDR)
					{
						if (IPUtils.IsIPV4InCIDRRange(ip, node.Ip))
						{
							NLog.Default.Information($"{TAG} {ip} matches whitelisted CIDR IP range '{node.Ip}', AcceptanceType: '{node.AcceptanceType}'.");
							return true;
						}
					} else if (IPAddress.TryParse(node.Ip, out var ipAddress))
					{
						if (ipAddress.MapToIPv6().Equals(ip.MapToIPv6()))
						{
							NLog.Default.Information($"{TAG} {ip} has a match in the whitelist, AcceptanceType: '{node.AcceptanceType}'");
							return true;
						}
					}
				}
			}
			return false;
		}

		private void PeriodicWatchListsUpdate(DateTime now, bool force = false)
		{
			if (force || this.NextCleanCheck < now)
			{
				foreach (KeyValuePair<IPAddress, QuarantinedInfo> expired in this.blacklist.Where(
					e => (e.Value.Expiry < now)).ToArray())
				{
					if (expired.Value.refusalCount >= this.connectionRefusalStrikes)
					{
						NLog.Connections.Warning(
							$"{TAG} Permanently banning {expired.Key}: {expired.Value.refusalCount} refused connection attempts"
							+ ", consider adding this ip to your firewall.");
						expired.Value.Reason = QuarantineReason.PermanentBan;
						expired.Value.Expiry = DateTimeEx.MaxValue; //won't enter in this scope anymore
						continue;
					}

					this.blacklist.Remove(expired.Key);
					expired.Value.Reason = QuarantineReason.Cleared;
					expired.Value.Expiry = now.Add(this.minPeriodBetweenConnections);
					if (!this.watchlist.TryAdd(expired.Key, expired.Value))
						NLog.Connections.Error($"{TAG}: unexpectedly already in watchlist");
				}

				this.NextCleanCheck = DateTimeEx.CurrentTime.Add(this.cleanupPeriod);
			}
		}

		public bool IsQuarantined(IPAddress ip, out DateTime expiry, out QuarantineReason reason)
		{
			lock (this.locker)
			{
				this.PeriodicWatchListsUpdate(DateTimeEx.CurrentTime, true); //we need up to date results
				
				if (this.blacklist.TryGetValue(ip, out var info))
				{
					expiry = info.Expiry;
					reason = info.Reason;
					return true;
				}
			}

			reason = QuarantineReason.Cleared;
			expiry = DateTimeEx.MinValue;
			return false;
		}
		public bool IsQuarantined(IPAddress ip)
		{
			return this.IsQuarantined(ip, out _, out _);
		}
		public bool IsQuarantined(IPAddress ip, out DateTime expiry)
		{
			return this.IsQuarantined(ip, out expiry, out _);
		}
		
		public void Quarantine(IPAddress ip, QuarantineReason reason, DateTime expiry, string details = "", double graceStrikes = 0.0, TimeSpan graceObservationPeriod = default(TimeSpan))
		{
			lock (this.locker)
			{
				if (this.IsWhiteList(ip, out var acceptanceType))
				{
					NLog.Connections.Error($"{TAG} Trying to Quarantine whitelisted node {ip}, aborting");
					return;
				}

				this.PeriodicWatchListsUpdate(DateTimeEx.CurrentTime, true);
				
				QuarantinedInfo info = null;
				
				if(this.blacklist.TryGetValue(ip, out info))
					NLog.Connections.Warning($"{TAG} Node {ip} is already blacklisted ({info.Reason}, expiry {info.Expiry}), updating...");
				
				
				if (graceStrikes > 0)
				{
					if (!this.greylist.TryGetValue(ip, out info))
					{
						info = new QuarantinedInfo{Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};
						this.greylist.Add(ip, info);
					}
					
					if (!info.WarningEvents.TryGetValue(reason, out var strikes))
					{
						strikes = new QuarantinedInfo.Events{};
						info.WarningEvents.Add(reason, strikes);
					}

					var now = DateTimeEx.CurrentTime;
					
					strikes.AddEvent(now);

					if (graceObservationPeriod == default(TimeSpan))
						graceObservationPeriod = TimeSpan.MaxValue;

					var count = strikes.CountEventsWithinObservationPeriod(now, graceObservationPeriod);

					var message =
						$"{TAG} {nameof(this.Quarantine)}: quarantine warnings strikes for ip {ip} and reason {reason} is now {count:0.00} out of {graceStrikes} over a {graceObservationPeriod} period";
					
					if (count <= graceStrikes)
					{
						NLog.Connections.Verbose($"{message}, tolerating...");
						return;
					}
					
					NLog.Connections.Verbose($"{message}, blacklisting...");
					this.greylist.Remove(ip);
				}
				
				if (this.watchlist.TryGetValue(ip, out info))
					this.watchlist.Remove(ip);
				else
					info = new QuarantinedInfo{Expiry = DateTimeEx.CurrentTime.Add(this.minPeriodBetweenConnections)};
				
				if (!this.blacklist.TryAdd(ip, info))
					info = this.blacklist[ip];

				info.Expiry = expiry;
				info.Reason = reason;

				this.LogQuarantine(ip, info, details);
			}
		}
		public enum QuarantineReason
		{
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
		private void LogQuarantine(IPAddress ip, QuarantinedInfo info, string details)
		{
			string reason = $"{info.Reason}";

			if (details.Length > 0)
				reason = $"{reason}: {details}";
			
			NLog.Connections.Verbose(
				$"{TAG} Placing {ip} in quarantine ({reason}), {info.blacklistStrikes} strikes, expires at {info.Expiry}.");
		}
		private class QuarantinedInfo {
			public DateTime Expiry = DateTimeEx.MinValue;
			public QuarantineReason Reason = QuarantineReason.Cleared;
			public int rateLimitGraceStrikes = 0;
			public int rateLimitStrikes = 0;
			public int blacklistStrikes = 0;
			public int refusalCount = 0;

			public class Events
			{
				private FixedQueue<DateTime> events { get; } = new FixedQueue<DateTime>(100);
				
				public void AddEvent(DateTime time)
				{
					this.events.Enqueue(time);
				}

				public double CountEventsWithinObservationPeriod(DateTime referenceTime, TimeSpan observationPeriod, double outOfTimeSpanBase = 0.95)
				{
					double value = 0.0;
					foreach (var timestamp in this.events)
					{
						if (timestamp < referenceTime.Subtract(observationPeriod))
							value += 1;
						else //out of observation period
							value += Math.Pow(outOfTimeSpanBase, Math.Max(1.0, (referenceTime - timestamp).TotalSeconds));
					}
					return value;
				}
			}
			public readonly Dictionary<QuarantineReason, Events> WarningEvents = new Dictionary<QuarantineReason, Events>();
		}

	#region Singleton

		static IPMarshall() {
		}

		public IPMarshall(int rateLimitStrikes = 5, 
			int rateLimitGraceStrikes = 0,
			int blacklistStrikes = 5,
			int connectionRefusalStrikes = 100,
			double minPeriodBetweenConnections = 10,
			double ratLimitTimePenalty = 5*60,
			double cleanupPeriod = 20) {
			this.rateLimitStrikes = rateLimitStrikes;
			this.rateLimitGraceStrikes = rateLimitGraceStrikes;
			this.blacklistStrikes = blacklistStrikes;
			this.connectionRefusalStrikes = connectionRefusalStrikes;
			this.minPeriodBetweenConnections = TimeSpan.FromSeconds(minPeriodBetweenConnections);
			this.ratLimitTimePenalty = TimeSpan.FromSeconds(ratLimitTimePenalty);
			this.cleanupPeriod = TimeSpan.FromSeconds(cleanupPeriod);
			if (GlobalSettings.ApplicationSettings != null)
				foreach (var node in GlobalSettings.ApplicationSettings.Blacklist)
				{
					if(IPAddress.TryParse(node.Ip, out var ip))
						this.blacklist.TryAdd(ip, new QuarantinedInfo{Expiry = DateTimeEx.MaxValue, Reason = QuarantineReason.AppSettingsBlacklist});
				}
			}

		public static IPMarshall Instance {get;} = new IPMarshall();
		public static IPMarshall ValidationInstance{get;} = new IPMarshall( 
			  0
			, 17 // 3 connections per validation period * 2 tries * 3 for CGNAT shared IPs = 18, so 17 after the first one
			, 5 
			, 100   // once in blacklist, the number of times befor we permanently blacklist you
			, 60 // The validation period duration TODO: use some application-wide constant 
			, 7*24*60*60 - (5*60)  //7 days - 5min
			, 20);

	#endregion

	}

}