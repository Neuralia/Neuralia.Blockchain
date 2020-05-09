using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Serilog;

namespace Neuralia.Blockchains.Core.Network {

	public struct IPV4CIDRRange {
		public IPV4CIDRRange(byte[] components) {
			this.Components = new byte[4];

			this.Components[0] = components[0];
			this.Components[1] = components[1];
			this.Components[2] = components[2];
			this.Components[3] = components[3];

			this.Range = components[4];
		}

		public bool Equals(IPV4CIDRRange other) {
			return this.Components.SequenceEqual(other.Components);
		}

		public override bool Equals(object obj) {
			return obj is IPV4CIDRRange other && this.Equals(other);
		}

		public override int GetHashCode() {
			return this.Components != null ? this.Components.GetHashCode() : 0;
		}

		public byte[] Components { get; }
		public byte Range { get; }

		public override string ToString() {
			return $"{this.Components[0]}.{this.Components[1]}.{this.Components[2]}.{this.Components[3]}/{this.Range}";
		}
	}

	public struct IPV6CIDRRange {
		public IPV6CIDRRange(byte[] components) {
			this.Components = new byte[16];

			for(int i = 0; i < 16; i++) {
				this.Components[i] = components[i];
			}

			this.Prefix = components[16];
		}

		public IPV6CIDRRange(byte[] components, byte prefix) {
			this.Components = new byte[16];

			for(int i = 0; i < 16; i++) {
				this.Components[i] = components[i];
			}

			this.Prefix = prefix;
		}

		public bool Equals(IPV6CIDRRange other) {
			return this.Components.SequenceEqual(other.Components);
		}

		public override bool Equals(object obj) {
			return obj is IPV6CIDRRange other && this.Equals(other);
		}

		public override int GetHashCode() {
			return this.Components != null ? this.Components.GetHashCode() : 0;
		}

		public byte[] Components { get; }
		public byte Prefix { get; }

		public override string ToString() {

			return new IPAddress(this.Components).MapToIPv6() + $"/{this.Prefix}";
		}
	}

	public static class IPUtils {

		public static Guid IPtoGuid(string ipaddress) {
			NodeAddressInfo node = new NodeAddressInfo(ipaddress, 80, NodeInfo.Unknown);

			return IPtoGuid(node.Address);
		}

		public static Guid IPtoGuid(byte[] bytes) {

			NodeAddressInfo node = new NodeAddressInfo(new IPAddress(bytes), 80, NodeInfo.Unknown);
			Span<byte> bytes2 = stackalloc byte[16];
			node.Address.GetAddressBytes().CopyTo(bytes2);

			return new Guid(bytes);
		}

		public static Guid IPtoGuid(IPAddress ipaddress) {

			NodeAddressInfo node = new NodeAddressInfo(ipaddress, 80, NodeInfo.Unknown);
			Span<byte> bytes = stackalloc byte[16];

			Span<byte> addressSpan = node.Address.GetAddressBytes().AsSpan();

			if(addressSpan.Length == 16) {
				addressSpan.Slice(0, 16).CopyTo(bytes);
			} else if(addressSpan.Length == 4) {
				addressSpan.CopyTo(bytes.Slice(12, 4));
			} else {
				throw new ApplicationException("Invalid IP address bytes.");
			}

			return new Guid(bytes);
		}

		public static byte[] GuidToBytes(Guid guid) {

			byte[] bytes = new byte[16];
			guid.TryWriteBytes(bytes);

			return bytes;
		}

		public static IPAddress GuidToIP(Guid guid) {

			Span<byte> bytes = stackalloc byte[16];
			guid.TryWriteBytes(bytes);

			return new IPAddress(bytes);
		}

		public static string GuidToIPstring(Guid guid) {

			NodeAddressInfo node = new NodeAddressInfo(GuidToIP(guid), 80, NodeInfo.Unknown);

			return node.Ip; // always as IpV6
		}

		/// <summary>
		///     tells us if a certain address is in the provided range
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="address"></param>
		/// <returns></returns>
		public static bool IsValid(string entry, NodeAddressInfo address) {

			if(string.IsNullOrWhiteSpace(entry)) {
				return false;
			}

			if(IsCIDRV4(entry)) {
				(IPAddress lower, IPAddress upper) range = GetIPV4CIDRRange(entry);

				if(IsIPV4InCIDRRange(address.Address.MapToIPv4(), range)) {
					return true;
				}
			} else if(IsIPV4(entry)) {
				if(Equals(address.Address.MapToIPv4(), new NodeAddressInfo(entry, NodeInfo.Unknown).Address.MapToIPv4())) {
					return true;
				}
			} else if(IsIPV6(entry)) {
				if(Equals(address.Address.MapToIPv6(), new NodeAddressInfo(entry, NodeInfo.Unknown).Address.MapToIPv6())) {
					return true;
				}
			} else {
				try {
					foreach(IPAddress hostAddress in Dns.GetHostAddresses(entry)) {

						if(Equals(address.Address, new NodeAddressInfo(hostAddress, NodeInfo.Unknown).Address)) {
							return true;
						}
					}
				} catch {

				}
			}

			return false;
		}
		
		public static IPMode GetIPMode(string ipaddress) {
			return GetIPMode(new NodeAddressInfo(ipaddress, 80, NodeInfo.Unknown));
		}

		public static IPMode GetIPMode(byte[] bytes) {

			return GetIPMode(new IPAddress(bytes));
		}
		
		public static IPMode GetIPMode(Guid guid) {

			return GetIPMode(GuidToIP(guid));
		}

		public static IPMode GetIPMode(IPAddress ipaddress) {
			
			return GetIPMode(new NodeAddressInfo(ipaddress, 80, NodeInfo.Unknown));
		}

		public static IPMode GetIPMode(NodeAddressInfo nodeInfo) {

			return nodeInfo.IsIpV4 ? IPMode.IPv4 : IPMode.IPv6;
		}
	#region IPV4

		
		public static List<IPV4CIDRRange> GetDefaultV4Ranges() {
			List<IPV4CIDRRange> ranges = new List<IPV4CIDRRange>();

			ranges.Add(IPV4ToCIDRComponents("10.0.0.0/8"));
			ranges.Add(IPV4ToCIDRComponents("172.16.0.0/12"));
			ranges.Add(IPV4ToCIDRComponents("192.168.0.0/16"));
			ranges.Add(IPV4ToCIDRComponents("127.0.0.0/8"));
			ranges.Add(IPV4ToCIDRComponents("127.0.0.0/8"));

			return ranges;
		}

		
		public static IPV4CIDRRange GenerateCIDRV4Range(IPAddress address, IPAddress netMask) {

			byte[] maskComponents = IPV4ToComponents(netMask);
			byte[] addressComponents = IPV4ToComponents(address);

			byte bitCount = (byte) string.Join("", maskComponents.Select(c => Convert.ToString(c, 2))).ToCharArray().Count(c => c == '1');

			for(int i = 0; i < 4; i++) {
				if(maskComponents[i] == 0) {
					addressComponents[i] = 0;
				}
			}

			return IPV4ComponentsToCIDR(addressComponents, bitCount);
		}

		public static byte[] IPV4ToComponents(IPAddress address) {
			return IPV4ToComponents(address.MapToIPv4().ToString());

		}

		public static byte[] IPV4ToComponents(string address) {
			string[] components = address.Split('.');

			byte[] results = new byte[4];

			results[0] = (byte) (Convert.ToUInt32(components[0]) & 0xff);
			results[1] = (byte) (Convert.ToUInt32(components[1]) & 0xff);
			results[2] = (byte) (Convert.ToUInt32(components[2]) & 0xff);
			results[3] = (byte) (Convert.ToUInt32(components[3]) & 0xff);

			return results;
		}

		public static IPV4CIDRRange IPV4ToCIDRComponents(string address) {
			string[] components = address.Split('.', '/');

			byte[] results = new byte[5];

			results[0] = (byte) (Convert.ToUInt32(components[0]) & 0xff);
			results[1] = (byte) (Convert.ToUInt32(components[1]) & 0xff);
			results[2] = (byte) (Convert.ToUInt32(components[2]) & 0xff);
			results[3] = (byte) (Convert.ToUInt32(components[3]) & 0xff);

			if(results.Length == 5) {
				results[4] = Convert.ToByte(components[4]);
			}

			return new IPV4CIDRRange(results);
		}

		public static IPV4CIDRRange IPV4ComponentsToCIDR(byte[] components, byte range) {
			byte[] results = new byte[5];

			results[0] = components[0];
			results[1] = components[1];
			results[2] = components[2];
			results[3] = components[3];
			results[4] = range;

			return new IPV4CIDRRange(results);
		}

		public static uint IPV4ToNumber(byte[] components) {
			return (Convert.ToUInt32(components[0]) << 24) | (Convert.ToUInt32(components[1]) << 16) | (Convert.ToUInt32(components[2]) << 8) | Convert.ToUInt32(components[3]);

		}

		public static IPAddress IPV4NumberToAddress(uint ip) {
			byte[] components = new byte[4];
			components[3] = (byte) (ip & 0xff);
			components[2] = (byte) ((ip >> 8) & 0xff);
			components[1] = (byte) ((ip >> 16) & 0xff);
			components[0] = (byte) (ip >> 24);

			return new IPAddress(components);
		}

		public static bool GetIPV4CIDRRange(IPAddress address, List<IPV4CIDRRange> ranges) {
			foreach(IPV4CIDRRange range in ranges) {

				(IPAddress lower, IPAddress upper) rangeComponents = IPUtils.GetIPV4CIDRRange(range);

				if(IPUtils.IsIPV4InCIDRRange(address.MapToIPv4(), rangeComponents)) {
					return true;
				}
			}

			return false;
		}
		
		public static (IPAddress lower, IPAddress upper) GetIPV4CIDRRange(string ip) {
			return GetIPV4CIDRRange(IPV4ToCIDRComponents(ip));
		}

		public static (IPAddress lower, IPAddress upper) GetIPV4CIDRRange(IPV4CIDRRange CIDRRange) {

			uint ipnum = IPV4ToNumber(CIDRRange.Components);

			int maskbits = Convert.ToInt32(CIDRRange.Range);
			uint mask = 0xffffffff;
			mask <<= 32 - maskbits;

			uint start = ipnum & mask;
			uint end = ipnum | ~mask;

			return (IPV4NumberToAddress(start), IPV4NumberToAddress(end));
		}

		public static bool IsIPV4InCIDRRange(IPAddress address, IPV4CIDRRange CIDRRange) {

			return IsIPV4InCIDRRange(address.MapToIPv4(), GetIPV4CIDRRange(CIDRRange)) ;
		}
		
		public static bool IsIPV4InCIDRRange(IPAddress address, string range) {

			return IsIPV4InCIDRRange(address, GetIPV4CIDRRange(range)) ;
		}

		public static bool IsIPV4InCIDRRange(IPAddress address, (IPAddress lowerInclusive, IPAddress upperInclusive) range) {
			(IPAddress lowerInclusive, IPAddress upperInclusive) = range;

			return IsIPV4InCIDRRange(address, lowerInclusive, upperInclusive);
		}

		public static bool IsIPV4InCIDRRange(IPAddress address, IPAddress lowerInclusive, IPAddress upperInclusive) {
			if(address.AddressFamily != lowerInclusive.AddressFamily) {
				return false;
			}

			byte[] lowerBytes = lowerInclusive.GetAddressBytes();
			byte[] upperBytes = upperInclusive.GetAddressBytes();

			byte[] addressBytes = address.GetAddressBytes();

			bool lowerBoundary = true, upperBoundary = true;

			for(int i = 0; (i < lowerBytes.Length) && (lowerBoundary || upperBoundary); i++) {
				if((lowerBoundary && (addressBytes[i] < lowerBytes[i])) || (upperBoundary && (addressBytes[i] > upperBytes[i]))) {
					return false;
				}

				lowerBoundary &= addressBytes[i] == lowerBytes[i];
				upperBoundary &= addressBytes[i] == upperBytes[i];
			}

			return true;
		}

		public static bool IsCIDRV4(string ip) {
			Regex regexCIDRV4 = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(\/(3[0-2]|[1-2][0-9]|[0-9]))$");

			return regexCIDRV4.Match(ip).Success;
		}

		public static bool IsIPV4(string ip) {

			if(!IPAddress.TryParse(ip, out IPAddress result)) {
				return false;
			}

			return IsIPV4(result);
		}
		
		public static bool IsIPV4(Guid guid) {
			return IsIPV4(GuidToIP(guid));
		}
		
		public static bool IsIPV4(IPAddress ip) {
			
			return new NodeAddressInfo(ip, NodeInfo.Unknown).IsIpV4;
		}

		public static string TranslateDnsToIP(string host) {
			bool isIp = IsIPV4(host) || IsIPV6(host);

			if(!isIp) {
				try {
					return Repeater.Repeat(() => {
						string source = host;

						string resultIp = host;

						if(source.ToLower() == "localhost") {
							resultIp = "127.0.0.1";
						} else {
							IPHostEntry dnsResult = Dns.GetHostEntry(source);

							source = dnsResult.AddressList.First().MapToIPv4().ToString();

							resultIp = source;
						}

						return resultIp;
					});
				} catch(Exception ex) {
					NLog.Default.Error(ex, $"Failed to translate host ip for name '{host}'.");
				}
			}

			return host;
		}

		public static Uri TranslateHostDnsToIPUri(string url) {
			Uri uri = new Uri(url);

			string result = TranslateDnsToIP(uri.Host);

			if(result != uri.Host) {
				NLog.Default.Information($"DNS '{uri.Host}' was converted to IP '{result}'.");

				UriBuilder builder = new UriBuilder(uri);
				builder.Host = result;

				return builder.Uri;
			}

			return uri;
		}

	#endregion

	#region IpV6

		//TODO: IPV6 CIDR range generation

		public static List<IPV6CIDRRange> GetDefaultV6Ranges() {
			List<IPV6CIDRRange> ranges = new List<IPV6CIDRRange>();

			ranges.Add(IPV6ToCIDRComponents("::/128"));
			ranges.Add(IPV6ToCIDRComponents("::1/128"));
			ranges.Add(IPV6ToCIDRComponents("FF00::/8"));
			ranges.Add(IPV6ToCIDRComponents("FE80::/10"));

			return ranges;
		}

		public static byte[] IPV6ToComponents(string address) {
			return IPAddress.Parse(address).GetAddressBytes();
		}

		public static IPV6CIDRRange IPV6ToCIDRComponents(string address) {
			string[] parts = address.Split('/');

			return new IPV6CIDRRange(IPV6ToComponents(parts[0]), byte.Parse(parts[1]));
		}

		public static string GetIPV6Root(byte[] components, byte prefix) {

			// not the fastest way, but it works
			IEnumerable<string> bytes = components.Select(c => Convert.ToString(c, 2).PadLeft(8, '0'));

			IEnumerable<char> pieces = string.Join("", bytes).ToCharArray().Take(prefix);

			return string.Join("", pieces);
		}
		
		// public static bool IsIPV6InCIDRRange(IPAddress address, IPV6CIDRRange CIDRRange) {
		//
		// 	return IsIPV6InCIDRRange(address.MapToIPv6(), IPV6ToCIDRComponents(CIDRRange)) ;
		// }
		//
		// public static bool IsIPV6InCIDRRange(IPAddress address, string range) {
		//
		// 	return IsIPV6InCIDRRange(address, GetIPV4CIDRRange(range)) ;
		// }
		//
		// public static bool IsIPV6InCIDRRange(IPAddress address, (IPAddress lowerInclusive, IPAddress upperInclusive) range) {
		// 	(IPAddress lowerInclusive, IPAddress upperInclusive) = range;
		//
		// 	return IsIPV6InCIDRRange(address, lowerInclusive, upperInclusive);
		// }
		
		public static bool IsIPV6InCIDRRange(IPAddress address, IPV6CIDRRange range) {
			byte[] components = address.GetAddressBytes();

			string rangeRoot = GetIPV6Root(range.Components, range.Prefix);
			string addressRoot = GetIPV6Root(components, range.Prefix);

			return rangeRoot == addressRoot;
		}

		public static bool IsCIDRV6(string ip) {
			Regex regexCIDRV6 = new Regex(@"^s*((([0-9A-Fa-f]{1,4}:){7}([0-9A-Fa-f]{1,4}|:))|(([0-9A-Fa-f]{1,4}:){6}(:[0-9A-Fa-f]{1,4}|((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3})|:))|(([0-9A-Fa-f]{1,4}:){5}(((:[0-9A-Fa-f]{1,4}){1,2})|:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3})|:))|(([0-9A-Fa-f]{1,4}:){4}(((:[0-9A-Fa-f]{1,4}){1,3})|((:[0-9A-Fa-f]{1,4})?:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){3}(((:[0-9A-Fa-f]{1,4}){1,4})|((:[0-9A-Fa-f]{1,4}){0,2}:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){2}(((:[0-9A-Fa-f]{1,4}){1,5})|((:[0-9A-Fa-f]{1,4}){0,3}:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){1}(((:[0-9A-Fa-f]{1,4}){1,6})|((:[0-9A-Fa-f]{1,4}){0,4}:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3}))|:))|(:(((:[0-9A-Fa-f]{1,4}){1,7})|((:[0-9A-Fa-f]{1,4}){0,5}:((25[0-5]|2[0-4]d|1dd|[1-9]?d)(.(25[0-5]|2[0-4]d|1dd|[1-9]?d)){3}))|:)))(%.+)?s*");

			return regexCIDRV6.Match(ip).Success;
		}

		
		public static bool IsIPV6(IPAddress ip) {
			
			return new NodeAddressInfo(ip, NodeInfo.Unknown).IsIpV6;
		}
		
		public static bool IsIPV6(string ip) {

			if(!IPAddress.TryParse(ip, out IPAddress result)) {
				return false;
			}

			return IsIPV6(result);
		}
		
		public static bool IsIPV6(Guid guid) {
			return IsIPV6(GuidToIP(guid));
		}


	#endregion

	}
}