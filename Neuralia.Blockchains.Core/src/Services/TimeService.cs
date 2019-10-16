using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Serilog;

namespace Neuralia.Blockchains.Core.Services {
	public interface ITimeService {
		DateTime CurrentRealTime { get; }

		long CurrentRealTimeTicks { get; }

		TimeSpan GetAcceptableRange { get; }
		void InitTime();

		DateTime GetDateTime(long ticks);

		TimeSpan GetTimeDifference(long timestamp, DateTime time, DateTime chainInception);

		bool WithinAcceptableRange(DateTime timestamp);

		bool WithinAcceptableRange(long timestamp, DateTime chainInception);

		DateTime GetTransactionDateTime(long timestamp, DateTime chainInception);

		long GetChainDateTimeOffset(DateTime chainInception);

		DateTime GetTimestampDateTime(long timestamp, DateTime chainInception);
	}

	public class TimeService : ITimeService {

		private DateTime networkDateTime;
		private TimeSpan timeDelta;

		public static string FormatDateTimeStandardUtc(DateTime dateTime) {
			return dateTime.ToUniversalTime().ToString("o");
		}
		
		public void InitTime() {
			//default Windows time server
			var ntpServers = new List<string>();

			//TODO: add more time servers
			ntpServers.Add("pool.ntp.org");
			ntpServers.Add("time-a-g.nist.gov");
			ntpServers.Add("time-b-g.nist.gov");
			ntpServers.Add("time-a-b.nist.gov");
			ntpServers.Add("time.nist.gov");
			ntpServers.Add("utcnist.colorado.edu");
			ntpServers.Add("time.google.com");
			ntpServers.Add("time2.google.com");
			ntpServers.Add("time.windows.com");

			// mix them up, to get a new one every time.
			ntpServers.Shuffle();

			// NTP message size - 16 bytes of the digest (RFC 2030)
			var ntpData = new byte[48];
			bool succeeded = false;

			foreach(string ntpServer in ntpServers) {

				try {

					Array.Clear(ntpData, 0, ntpData.Length);

					//Setting the Leap Indicator, SoftwareVersion Number and Mode values
					ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

					var addresses = Dns.GetHostEntry(ntpServer).AddressList;

					//The UDP port number assigned to NTP is 123
					IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], 123);

					//NTP uses UDP
					using(Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
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
					// failed to reach the NTP server
					Log.Verbose(e, $"Failed to query ntp server '{ntpServer}'.");
				}
			}

			if(!succeeded) {
				Log.Error("Failed to query ALL ntp servers. this could be problematic, you could be rejected by the network if your time is off by too much.");

				return;
			}

			//TODO: rewrite this

			//Offset to get to the "Transmit Value" field (time at which the reply 
			//departed the server for the client, in 64-bit timestamp format."
			const byte serverReplyTime = 40;

			//Get the seconds part
			ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

			//Get the seconds fraction
			ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

			//Convert From big-endian to little-endian
			intPart = this.SwapEndianness(intPart);
			fractPart = this.SwapEndianness(fractPart);

			ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

			//**UTC** time
			this.networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long) milliseconds);

			this.timeDelta = this.networkDateTime.Subtract(DateTime.UtcNow);
		}

		/// <summary>
		///     make sure a timestamp is within a range
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public bool WithinAcceptableRange(DateTime timestamp) {
			DateTime utcTimestamp = timestamp.ToUniversalTime();
			
			return ((this.CurrentRealTime - this.GetAcceptableRange) < utcTimestamp) && (utcTimestamp < (this.CurrentRealTime + this.GetAcceptableRange));

		}

		public TimeSpan GetAcceptableRange => TimeSpan.FromMinutes(GlobalSettings.ApplicationSettings.AcceptableTimeRange);

		public bool WithinAcceptableRange(long timestamp, DateTime chainInception) {

			return this.WithinAcceptableRange(this.GetTimestampDateTime(timestamp, chainInception));
		}

		public DateTime CurrentRealTime => DateTime.UtcNow.Add(this.timeDelta);

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
			return chainInception + TimeSpan.FromSeconds(timestamp);
		}

		public TimeSpan GetTimeDifference(long timestamp, DateTime time, DateTime chainInception) {
			DateTime rebuiltTime = this.GetTimestampDateTime(timestamp, chainInception);

			return time.ToUniversalTime() - rebuiltTime;
		}

		protected void ValidateChainInception(DateTime chainInception) {
			if(chainInception == DateTime.MinValue) {
				throw new ApplicationException("Chain inception is not set");
			}
		}

		private uint SwapEndianness(ulong x) {
			return (uint) (((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24));
		}
	}
}