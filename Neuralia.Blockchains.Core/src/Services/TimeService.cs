using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Services {
	public interface ITimeService {
		DateTime CurrentRealTime { get; }

		long CurrentRealTimeTicks { get; }

		void InitTime();

		DateTime GetDateTime(long ticks);

		TimeSpan GetTimeDifference(long timestamp, DateTime time, DateTime chainInception);

		bool WithinAcceptableRange(DateTime timestamp, TimeSpan acceptableTimeRange);

		bool WithinAcceptableRange(long timestamp, DateTime chainInception, TimeSpan acceptableTimeRange);
		bool WithinThsAcceptableRange(long timestamp, DateTime chainInception, TimeSpan acceptableTimeRange, bool addExpectedTHSBuffer, THSRulesSetDescriptor rulesSetDescriptor);
		

		DateTime GetTransactionDateTime(long timestamp, DateTime chainInception);

		long GetChainDateTimeOffset(DateTime chainInception);

		DateTime GetTimestampDateTime(long timestamp, DateTime chainInception);
		TimeSpan GetTimestampSpan(long timestamp, DateTime chainInception);
		
		
	}

	public class TimeService : ITimeService {

		private Timer timer;

		public void InitTime() {

			this.timer = new Timer(state => {

				try {
					//default Windows time server
					List<string> ntpServers = new List<string>();

					//TODO: add more time servers
					ntpServers.Add("pool.ntp.org");
					ntpServers.Add("time.nist.gov");
					ntpServers.Add("time.google.com");
					ntpServers.Add("time2.google.com");
					ntpServers.Add("time.windows.com");

					// mix them up, to get a new one every time.
					ntpServers.Shuffle();

					// NTP message size - 16 bytes of the digest (RFC 2030)
					byte[] ntpData = new byte[48];
					bool succeeded = false;
					
					foreach(string ntpServer in ntpServers) {

						
						try {
							IPAddress[] addresses = Dns.GetHostEntry(ntpServer).AddressList;

							foreach(var address in addresses) {

								Array.Clear(ntpData, 0, ntpData.Length);

								//Setting the Leap Indicator, SoftwareVersion Number and Mode values
								ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3, Mode = 3 (Client Mode)
								
								try {
									//The UDP port number assigned to NTP is 123
									IPEndPoint ipEndPoint = new IPEndPoint(address, 123);

									//NTP uses UDP

									using(var tcpSocket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)) {
										tcpSocket.ExclusiveAddressUse = true;

										//Stops code hang if NTP is transactioned
										tcpSocket.ReceiveTimeout = 1000;
										tcpSocket.SendTimeout = 1000;

										tcpSocket.Connect(ipEndPoint);

										tcpSocket.Send(ntpData);
										tcpSocket.Receive(ntpData);
										tcpSocket.Close();
									}

									// seems that it worked
									succeeded = true;

									break;
								} catch(Exception e) {
									// just continue
									NLog.Default.Verbose(e, $"Failed to query ntp server '{ntpServer}' at address {address}:123.");
								}
							}

							if(succeeded) {
								break;
							}
						} catch(Exception e) {
							// failed to reach the NTP server
							NLog.Default.Verbose(e, $"Failed to query ntp server '{ntpServer}'.");
						}
					}

					if(!succeeded) {
						NLog.Default.Error("Failed to query ALL ntp servers. this could be problematic, you could be rejected by the network if your time is off by too much.");

						return;
					}

					//Get the seconds part
					ulong nominator = (ulong) ntpData[40] << 24 | (ulong) ntpData[41] << 16 | (ulong) ntpData[42] << 8 | ntpData[43];
					ulong fraction = (ulong) ntpData[44] << 24 | (ulong) ntpData[45] << 16 | (ulong) ntpData[46] << 8 | ntpData[47];

					var milliseconds = (nominator * 1000) + ((fraction * 1000) / 0x100000000L);

					//**UTC** time
					DateTime networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long) milliseconds);

					// compare the time of the time servers and ours. we should not be too far
					TimeSpan delta = (networkDateTime - DateTime.UtcNow).Duration();

					if(delta > TimeSpan.FromMinutes(30)) {
						// this is very serious, we are very out of sync with true time. we cant continue.
						//TODO: how can we improve this?
						NLog.Default.Fatal($"The time server tells us that current UTC time is {networkDateTime} and we have a local UTC time of {DateTime.UtcNow}. The difference is too big; we could be set in the wrong timezone. check your time settings to continue.");
						Thread.Sleep(1000);
						Environment.Exit(0);
					}
					
					DateTimeEx.SetTime(networkDateTime);
					
				} catch(Exception ex) {
					//TODO: do something?
					NLog.Default.Error(ex, "Timer exception");
				}
			}, this, TimeSpan.FromSeconds(1), TimeSpan.FromHours(10));
		}

		/// <summary>
		///     make sure a timestamp is within a range
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public bool WithinAcceptableRange(DateTime timestamp, TimeSpan acceptableTimeRange) {
			DateTime utcTimestamp = timestamp.ToUniversalTime();

			return (utcTimestamp > (this.CurrentRealTime - acceptableTimeRange)) && (utcTimestamp < (this.CurrentRealTime + acceptableTimeRange));

		}

		public bool WithinAcceptableRange(long timestamp, DateTime chainInception, TimeSpan acceptableTimeRange) {

			return this.WithinAcceptableRange(this.GetTimestampDateTime(timestamp, chainInception), acceptableTimeRange);
		}

		public bool WithinThsAcceptableRange(DateTime timestamp, TimeSpan acceptableTimeRange, bool addExpectedTHSBuffer, THSRulesSetDescriptor rulesSetDescriptor) {
			DateTime utcTimestamp = timestamp.ToUniversalTime();
			
			// here we give 10x the target timespan of the THS. it should be more than enough
			if(addExpectedTHSBuffer) {
				utcTimestamp += (rulesSetDescriptor.TargetTimespan * 10);
			}

			return (utcTimestamp > (this.CurrentRealTime - acceptableTimeRange)) && (utcTimestamp < (this.CurrentRealTime + acceptableTimeRange));
		}
		
		public bool WithinThsAcceptableRange(long timestamp, DateTime chainInception, TimeSpan acceptableTimeRange, bool addExpectedTHSBuffer, THSRulesSetDescriptor rulesSetDescriptor) {
			return this.WithinThsAcceptableRange(this.GetTimestampDateTime(timestamp, chainInception), acceptableTimeRange, addExpectedTHSBuffer, rulesSetDescriptor);
		}

		public DateTime CurrentRealTime => DateTimeEx.CurrentTime;

		public long CurrentRealTimeTicks => this.CurrentRealTime.Ticks;

		public DateTime GetDateTime(long ticks) {
			return new DateTime(ticks, DateTimeKind.Utc);
		}

		/// <summary>
		///     Convert a timestamp offset ince inception to a complete datetime
		/// </summary>
		/// <param name="timestamp"></param>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public DateTime GetTransactionDateTime(long timestamp, DateTime chainInception) {
			return this.GetTimestampDateTime(timestamp, chainInception);
		}

		/// <summary>
		///     Get the amout of seconds since the chain inception
		/// </summary>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public long GetChainDateTimeOffset(DateTime chainInception) {

			this.ValidateChainInception(chainInception);

			return (long) (this.CurrentRealTime - chainInception).TotalSeconds;
		}

		/// <summary>
		///     Get the absolute datetime from a timestamp relative to the chainInception
		/// </summary>
		/// <param name="chainInception"></param>
		/// <returns></returns>
		public DateTime GetTimestampDateTime(long timestamp, DateTime chainInception) {

			this.ValidateChainInception(chainInception);

			if(chainInception.Kind != DateTimeKind.Utc) {
				throw new ApplicationException("Chain inception should always be in UTC");
			}

			return (chainInception + TimeSpan.FromSeconds(timestamp)).ToUniversalTime();
		}

		public TimeSpan GetTimestampSpan(long timestamp, DateTime chainInception) {
			this.ValidateChainInception(chainInception);

			if(chainInception.Kind != DateTimeKind.Utc) {
				throw new ApplicationException("Chain inception should always be in UTC");
			}

			return TimeSpan.FromSeconds(timestamp);
		}

		public TimeSpan GetTimeDifference(long timestamp, DateTime time, DateTime chainInception) {
			DateTime rebuiltTime = this.GetTimestampDateTime(timestamp, chainInception);

			return time.ToUniversalTime() - rebuiltTime;
		}

		public static string FormatDateTimeStandardUtc(DateTime dateTime) {
			return dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
		}
		
		public static string FormatDateTimeStandardLocal(DateTime dateTime) {
			return dateTime.ToLocalTime().ToString("o", CultureInfo.InvariantCulture);
		}

		protected void ValidateChainInception(DateTime chainInception) {
			if(chainInception == DateTimeEx.MinValue) {
				throw new ApplicationException("Chain inception is not set");
			}
		}

		private uint SwapEndianness(ulong x) {
			return (uint) (((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24));
		}
	}
}