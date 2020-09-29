using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Extensions;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	
	/// <summary>
	/// Utility class to test a port
	/// </summary>
	public static class PortTester {
		public static async Task<bool> TestPort() {

			try {
				
				NLog.Default.Information($"Testing for port {GlobalSettings.ApplicationSettings.Port}.");
				
				string testerDns = GlobalSettings.ApplicationSettings.PortTestDns;

				var hosts = Dns.GetHostEntry(testerDns);
				
				if(hosts.AddressList.Any()) {

					foreach(var address in hosts.AddressList) {

						ClosureWrapper<NodeAddressInfo> node = new NodeAddressInfo(address, NodeInfo.Unknown);

						bool success = await Repeater.RepeatAsync(() => {

							return ConnectToTester(node);
						}).ConfigureAwait(false);

						if(success) {
							return true;
						}
					}
				}

			} catch(Exception ex) {
				// do nothing, we got our answer
				NLog.Default.Error(ex, "Failed to test port");
			} 
			return false;
		} 
		
		private static async Task<bool> ConnectToTester(NodeAddressInfo node) {

			Socket socket = null;
			try {
				
				// make sure we are in the right ip format
				var endpoint = new IPEndPoint(node.AdjustedAddress, GlobalsService.DEFAULT_PORT);

				if(NodeAddressInfo.IsAddressIpV4(endpoint.Address)) {
					socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				} else {
					if(!Socket.OSSupportsIPv6) {
						throw new ApplicationException();
					}

					socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
					socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
				}

				socket.InitializeSocketParameters();

				Task<bool> task = Task.Factory.FromAsync(socket.BeginConnect(endpoint, null, null), result => {
					try {
						socket.ReceiveTimeout = 10_000;
						byte[] bytes = new[] { (byte)1};
						if(socket.Connected && (socket.Send(bytes) == 1)) {

							bytes[0] = 0;
							int received = socket.Receive(bytes);

							if(received == 1 && bytes[0] == 1) {
								return true;
							}
						}
					} catch {

					}

					return false;
				}, TaskCreationOptions.None);

				return await task.HandleTimeout(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

			} catch(Exception ex) {
				// do nothing, we got our answer

			} finally {

				try {
					socket?.Shutdown(SocketShutdown.Both);
				} catch {
					// do nothing, we got our answer
				}

				try {
					socket?.Dispose();
				} catch {
					// do nothing, we got our answer
				}
			}

			return false;
		}
		
	}
}