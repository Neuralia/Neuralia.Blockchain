using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Extensions;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Serialization;
using RestSharp;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	
	/// <summary>
	/// Utility class to test a port
	/// </summary>
	public static class PortTester {

		public const byte BYTES_PER_REQUESTER = 5;

		[Flags]
		public enum TcpTestParameter:byte {
			Failed = 0,
			Success = 1 << 0,
			RequestCallback = 1 << 1,
			
			CallbackAttempted = 1 << 2,
			CallbackSucceeded = 1 << 3,
			
			Ipv6 = 1 << 6,
			IsValidator = 1 << 7
			              
		}
		
		public enum TcpTestPorts {
			P2p = 1,Validator = 2, ValidatorHttp = 3
		}

		[Flags]
		public enum TcpTestResult:byte {
			Failed = 0,
			Connected = 1 << 0,
			CallbackAttempted = 1 << 1,
			CallbackSucceeded = 1 << 2,
		}
		
		public static async Task<TcpTestResult> TestPort(TcpTestPorts testPort, bool callback, bool serverRunning) {

			try {
				int port = 0;

				if(testPort == TcpTestPorts.P2p) {
					port = GlobalSettings.ApplicationSettings.Port;
				}
				else if(testPort == TcpTestPorts.Validator) {
					port = GlobalSettings.ApplicationSettings.ValidatorPort;
				}
				else if(testPort == TcpTestPorts.ValidatorHttp) {
					port = GlobalSettings.ApplicationSettings.ValidatorHttpPort;
				}
				
				NLog.Default.Information($"Testing for port {port}.");

				List<NodeAddressInfo> addresses = new List<NodeAddressInfo>();

				if(!string.IsNullOrWhiteSpace(GlobalSettings.ApplicationSettings.PortTestIpOverride)) {
					
					addresses.Add(new NodeAddressInfo(GlobalSettings.ApplicationSettings.PortTestIpOverride, NodeInfo.Unknown));
				} else {
					string testerDns = GlobalSettings.ApplicationSettings.PortTestDns;

					var hosts = Dns.GetHostEntry(testerDns);

					if(hosts.AddressList.Any()) {

						foreach(var address in hosts.AddressList) {
							addresses.Add(new NodeAddressInfo(address, NodeInfo.Unknown));

						}
					}
				}
				
				IPMode ipMode = IPMode.IPv4;

				RESTValidatorServer validationHttpServer = null;
				TcpValidatorServer validationServer = null;
				TcpServer tcpServer = null;
				
				try {
					if(callback) {
						// gotta start a server
						if(!serverRunning) {

							
							if(testPort == TcpTestPorts.P2p) {
								tcpServer = new TcpServer(ipMode, GlobalSettings.ApplicationSettings.Port, (e,c) => {
								});
								
								tcpServer.Start();
							}
							else if(testPort == TcpTestPorts.Validator) {
								
								validationServer = new TcpValidatorServer(3, ipMode, GlobalSettings.ApplicationSettings.ValidatorPort);
								validationServer.Initialize();

								validationServer.Start();
							}
							else if(testPort == TcpTestPorts.ValidatorHttp) {
#if NET5_0
								validationHttpServer = new RESTValidatorServer(GlobalSettings.ApplicationSettings.ValidatorHttpPort, RESTValidatorServer.ServerModes.Test);
								validationHttpServer.Start();
#endif
							}
						}
					}
					
					if(addresses.Any()) {

						foreach(var address in addresses) {
							
							TcpTestResult result = await Repeater.RepeatAsync(() => {

								return ConnectToTester(address, testPort, callback, ipMode);
							}).ConfigureAwait(false);

							if(result.HasFlag(TcpTestResult.Connected)) {
								return result;
							}
						}
					}
				} finally {
					try {
						validationServer?.Stop();
						validationServer?.Dispose();
					} catch {
						
					}
					
					try {
#if NET5_0
						validationHttpServer?.Stop();
						validationHttpServer?.Dispose();
#endif
					} catch {
						
					}
					
					try {
						tcpServer?.Stop();
						tcpServer?.Dispose();
					} catch {
						
					}
				}

			} catch(Exception ex) {
				// do nothing, we got our answer
				NLog.Default.Error(ex, "Failed to test port");
			} 
			return TcpTestResult.Failed;
		} 
		
		private static async Task<TcpTestResult> ConnectToTester(NodeAddressInfo node, TcpTestPorts testPort, bool callback, IPMode ipMode) {

			Socket socket = null;
			try {

				int port = 0;

				if(testPort == TcpTestPorts.P2p) {
					port = GlobalsService.DEFAULT_PORT;
				}
				else if(testPort == TcpTestPorts.Validator) {
					port = GlobalsService.DEFAULT_VALIDATOR_PORT;
				}
				else if(testPort == TcpTestPorts.ValidatorHttp) {
					port = 5003;//GlobalsService.DEFAULT_VALIDATOR_HTTP_PORT;
				}
				
				// make sure we are in the right ip format
				var endpoint = new IPEndPoint(node.AdjustedAddress, port);

				TimeSpan timeout = TimeSpan.FromSeconds(10);
				Task<TcpTestResult> task;
				if(testPort == TcpTestPorts.ValidatorHttp) {

					task = Task.Run(async () => {
						RestUtility restUtility = new RestUtility(GlobalSettings.ApplicationSettings, RestUtility.Modes.XwwwFormUrlencoded);
					
						Dictionary<string, object> parameters = new Dictionary<string, object>();

						TcpTestParameter operation = TcpTestParameter.Failed;
						if(callback) {
							operation |= TcpTestParameter.RequestCallback;
						}

						parameters.Add("operations", (int)operation);
						
						restUtility.Timeout = timeout;
						IRestResponse result = await restUtility.Post(GetUrl(endpoint.Address, port), "appointments-test/test", parameters).ConfigureAwait(false);

						TcpTestResult connectionResult = TcpTestResult.Failed; 
						// ok, check the result
						if(result.StatusCode != 0) {
							connectionResult |= TcpTestResult.Connected;
						}
						if(result.StatusCode == HttpStatusCode.OK) {

							if(!string.IsNullOrWhiteSpace(result.Content)) {

								connectionResult = (TcpTestResult)int.Parse(result.Content);
							}
						}

						return connectionResult;
						
					});
				} else {

					//TODO: this can be made more efficient by releasing the thread but keeping the timeout.

					bool success = false;

					(socket, success) = await SocketExtensions.ConnectSocket(new NetworkEndPoint(endpoint), s => {

						s.InitializeSocketParametersFast(BYTES_PER_REQUESTER);
					}, 3).ConfigureAwait(false);

					if(!success) {
						return TcpTestResult.Failed;
					}
					TcpTestResult connectionResult = (byte) TcpTestResult.Failed;

					try {
						if(!socket.Connected) {
							return connectionResult;
						}

						connectionResult = TcpTestResult.Connected;
						TcpTestParameter pingBytes = TcpTestParameter.Success;

						byte[] bytes = new byte[callback ? BYTES_PER_REQUESTER : 1];

						if(callback) {
							pingBytes |= TcpTestParameter.RequestCallback;

							if(ipMode.HasFlag(IPMode.IPv6)) {
								pingBytes |= TcpTestParameter.Ipv6;
							}

							bytes = new byte[BYTES_PER_REQUESTER];


							if(testPort == TcpTestPorts.P2p) {
								port = GlobalSettings.ApplicationSettings.Port;
							} else if(testPort == TcpTestPorts.Validator) {
								port = GlobalSettings.ApplicationSettings.ValidatorPort;
							}

							TypeSerializer.Serialize(port, bytes.AsSpan(1, 4));
						}

						if(testPort == TcpTestPorts.Validator) {
							pingBytes |= TcpTestParameter.IsValidator;
						}

						bytes[0] = (byte) pingBytes;

						int sent = socket.Send(bytes);

						if(socket.Connected && (sent <= 5)) {

							byte[] rbytes = new byte[1];
							int received = socket.Receive(rbytes);

							if(received == 1) {
								return (TcpTestResult) rbytes[0];
							}
						}
					} catch(Exception ex) {

					}

					return connectionResult;
				}
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

			return TcpTestResult.Failed;
		}
		
		private static string GetUrl(IPAddress address, int port) {
			return new UriBuilder("http", address.ToString(), port).Uri.ToString();
		}
	}
}