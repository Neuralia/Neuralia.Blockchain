﻿using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Extensions;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Threading;

namespace Neuralia.Blockchains.Core.Network {
	public static class SocketExtensions {

		private static readonly byte[] Empty = new byte[1];

		/// <summary>
		/// </summary>
		/// <remarks>
		///     The time value sets the timeout since data was last sent. Then it attempts to send and receive a keep-alive
		///     packet. If it fails it retries 10 times (number hardcoded since Vista AFAIK) in the interval specified before
		///     deciding the connection is dead. So the above values would result in 2+10*1 = 12 second detection. After that any
		///     read / wrtie / poll operations should fail on the socket.
		/// </remarks>
		/// <param name="instance"></param>
		/// <param name="KeepAliveTime"></param>
		/// <param name="KeepAliveInterval"></param>
		public static void SetSocketKeepAliveValues(this Socket socket, int KeepAliveTime, int KeepAliveInterval) {
			//KeepAliveTime: default value is 2hr
			//KeepAliveInterval: default value is 1s and Detect 5 times

			//the native structure
			//struct tcp_keepalive {
			//ULONG onoff;
			//ULONG keepalivetime;
			//ULONG keepaliveinterval;
			//};

			int size = Marshal.SizeOf(new uint());
			byte[] inOptionValues = new byte[size * 3]; // 4 * 3 = 12
			bool OnOff = true;

			BitConverter.GetBytes((uint) (OnOff ? 1 : 0)).CopyTo(inOptionValues, 0);
			BitConverter.GetBytes((uint) KeepAliveTime).CopyTo(inOptionValues, size);
			BitConverter.GetBytes((uint) KeepAliveInterval).CopyTo(inOptionValues, size * 2);

			// windows only 
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
			}

			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
		}

		public static Socket CreateServerSocket(IPMode ipMode, Action<Socket> initialize) {

			Socket listener = null;
			if(ipMode.HasFlag(IPMode.IPv6)) {
				if(!TcpConnection.IPv6Supported) {
					throw new P2pException("IPV6 not supported!", P2pException.Direction.Receive, P2pException.Severity.Casual);
				}
				listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
				bool hasIpv4 = ipMode.HasFlag(IPMode.IPv4);
				listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, hasIpv4);

				if(hasIpv4) {
					listener.DualMode = true;
				}
			} else {
				listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			}

			initialize(listener);
			
			// seems to be needed in case the listener is not completely disposed yet (on linux and MacOs)
			//https://github.com/dotnet/corefx/issues/24562
			//TODO: this is a bug fix, and maybe in the future we may not need the below anymore.
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
			}

			return listener;
		}

		/// <summary>
		/// Attempt to parallel shoot either a single socket for ipv6, or the 2 types for ipv4 and return the first that connects, clean up the rest
		/// </summary>
		/// <param name="endpoint"></param>
		/// <param name="setParameters"></param>
		/// <param name="timeout"></param>
		/// <param name="connectCallback"></param>
		/// <returns></returns>
		/// <exception cref="P2pException"></exception>
		public static async Task<(Socket socket, bool success)> ConnectSocket(NetworkEndPoint endpoint, Action<Socket> setParameters, int timeout = 5, Func<Socket, IPEndPoint, Task> connectCallback = null) {
			
			void ClearSocket(Socket s) {
				try {
					s?.Shutdown(SocketShutdown.Both);
				} catch {
				}

				try {
					s?.Dispose();
				} catch {
				}
			}
			
			void Clear(Task<Socket> t) {
				if(t.IsCompleted && t.Result != null) {
					ClearSocket(t.Result);
				}
			}


			bool ipv6Supported = TcpConnection.IPv6Supported && GlobalSettings.ApplicationSettings.IPProtocolClient.HasFlag(IPMode.IPv6);
			bool ipv4 = NodeAddressInfo.IsAddressIpV4Analog(endpoint);
			bool doubleConnections = GlobalSettings.ApplicationSettings.EnableDoubleSocketConnections && ipv6Supported && ipv4;

			if(!ipv4 && !ipv6Supported) {
				NLog.Default.Debug("IPv6 not supported!");

				return default;
			}
			else if(ipv4 && !GlobalSettings.ApplicationSettings.IPProtocolClient.HasFlag(IPMode.IPv4)) {
				NLog.Default.Debug("IPv4 not supported!");

				return default;
			}
			(Socket socket, IPEndPoint adjustedEndpoint) PrepareSocket(bool second = false) {

				try {
					Socket socket = null;

					IPEndPoint adjustedEndpoint = endpoint.EndPoint;

					if(ipv4 && !second) {
						socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

						if(NodeAddressInfo.IsAddressIpv4MappedToIpV6(endpoint)) {
							adjustedEndpoint = new IPEndPoint(NodeAddressInfo.GetAddressIpV4(endpoint), endpoint.EndPoint.Port);
						}
					} else {
						if(!ipv6Supported) {
							NLog.Default.Debug("IPv6 not supported!");

							return default;
						}

						socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
						socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, !ipv4);
					}

					setParameters(socket);

					return (socket, adjustedEndpoint);
				} catch(Exception) {

				}

				return default;
			}

			ClosureWrapper<bool> results = false;

			if(connectCallback == null) {
				connectCallback = (s, a) => {
					return Task.Factory.FromAsync(s.BeginConnect, s.EndConnect, a, null);
				};
			}

			ClosureWrapper<AsyncManualResetEventSlim> manualResetEventSlim = new ClosureWrapper<AsyncManualResetEventSlim>();
			
			Task<Socket> task1 = Task.Run(async () => {
				Socket socket = null;
				IPEndPoint adjustedEndpoint = null;
				int graceTimeout = 1;

				try {
					(socket, adjustedEndpoint) = PrepareSocket(false);
					
					if(socket == null) {
						return default;
					}
					
					var connect = connectCallback(socket, adjustedEndpoint);
					bool success = await connect.HandleTimeout(TimeSpan.FromSeconds(doubleConnections ? graceTimeout : timeout)).ConfigureAwait(false);

					if(success || !doubleConnections) {
						results.Value = true;

						if(!(success && socket.Connected)) {
							ClearSocket(socket);
							socket = null;
						}
					}
					
					manualResetEventSlim.Value?.Set();

					if(results.Value || socket == null) {
						return socket;
					}

					success = await connect.HandleTimeout(TimeSpan.FromSeconds(Math.Max(timeout - graceTimeout, 1))).ConfigureAwait(false);

					if(success && socket.Connected) {
						return socket;
					}
				} catch {
				}

				ClearSocket(socket);

				return default;
			});

			if(!ipv4 || !doubleConnections || !TcpConnection.IPv6Supported) {
				var result = await task1.ConfigureAwait(false);

				return (result, result != null);
			} else {
				using(manualResetEventSlim.Value = new AsyncManualResetEventSlim()){
				
					await manualResetEventSlim.Value.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
				}

				if(results.Value) {
					var result = await task1.ConfigureAwait(false);
					if(!(result != null && result.Connected)) {
						ClearSocket(result);
						result = null;
					}
					return (result, result != null);
				}

				// ok, lets double shoot!
				var task2 = Task.Run(async () => {
					
					Socket socket = null;
					IPEndPoint adjustedEndpoint = null;

					try {
						(socket, adjustedEndpoint) = PrepareSocket(true);

						if(socket == null) {
							return default;
						}
						var connect = connectCallback(socket, adjustedEndpoint);
						bool success = await connect.HandleTimeout(TimeSpan.FromSeconds(timeout)).ConfigureAwait(false);

						if(success && socket.Connected) {
							return socket;
						}
					} catch {
					}

					ClearSocket(socket);

					return default;
				});

				Task<Socket> resultTask = await Task.WhenAny(task1, task2).ConfigureAwait(false);

				Task<Socket> otherTask = resultTask == task1 ? task2 : task1;
				
				if(resultTask.Result != null && resultTask.Result.Connected) {
					
					if(otherTask.IsCompleted) {
						Clear(otherTask);
					} else {
						var final = otherTask.ContinueWith(t => {
							Clear(t);
						});
					}

					return (resultTask.Result, true);
				} else {
					Clear(resultTask);
				}
				
				var lastSocket = await otherTask.ConfigureAwait(false);

				if(lastSocket != null && lastSocket.Connected) {
					return (lastSocket, true);
				}
				Clear(otherTask);
			}

			return default;
		}

		public static void InitializeSocketParameters(this Socket socket) {

			// Ensure a keep alive to know if connection is still active
			socket.SetSocketKeepAliveValues(2000, 1000);
			socket.NoDelay = false;
			socket.LingerState = new LingerOption(true, 10);
			socket.ExclusiveAddressUse = true;
			socket.ReceiveBufferSize = 8192;
			socket.ReceiveTimeout = 3000;
			socket.SendBufferSize = 8192;
			socket.SendTimeout = 3000;
		}

		// configured for very short sessions
		public static void InitializeSocketParametersFast(this Socket socket, int buffer) {

			// Disable the Nagle Algorithm for this tcp socket.
			socket.NoDelay = true;
			socket.LingerState = new LingerOption(false, 0);
			socket.ExclusiveAddressUse = true;
			socket.ReceiveBufferSize = buffer;
			socket.ReceiveTimeout = 1000;
			socket.SendBufferSize = buffer;
			socket.SendTimeout = 1000;
		}

		/// <summary>
		///     get the active connection information
		/// </summary>
		/// <param name="socket"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public static TcpConnectionInformation GetActiveConnectionInformation(this Socket socket) {
			IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();

			TcpConnectionInformation[] tcpConnections = null;

			try {
				tcpConnections = ipProperties.GetActiveTcpConnections().Where(x => {

					try {
						if(!socket.Connected) {
							return false;
						}

						bool localEndpointSame = socket.LocalEndPoint is IPEndPoint ep && (x.LocalEndPoint.Port == ep.Port);
						bool removeEndpointSame = socket.RemoteEndPoint is IPEndPoint ep2 && (x.RemoteEndPoint.Port == ep2.Port);

						// sicne the address does not always match (especially localhost in ipv6), we check for ports
						return (NodeAddressInfo.PrepareAddress(x.LocalEndPoint).Equals(socket.LocalEndPoint) && NodeAddressInfo.PrepareAddress(x.RemoteEndPoint).Equals(socket.RemoteEndPoint)) || (localEndpointSame && removeEndpointSame);
					} catch {
						return false;
					}
				}).ToArray();
			} catch {
				return null;
			}

			if(tcpConnections.Length == 0) {
				return null;
			}

			if(tcpConnections.Length == 1) {
				return tcpConnections.Single();
			}

			throw new ApplicationException("Multiple connections found!");
		}

		/// <summary>
		///     Tells us if the socket is connected. Combined with the keep alive, we should have a fairly reliable ability to know
		///     if it is actively connected.
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static bool IsReallyConnected(this Socket socket) {
			return IsReallyConnected(socket, out var latency);
		}

		public static bool IsReallyConnected(this Socket socket, out TimeSpan latency) {
			latency = TimeSpan.Zero;

			if(!socket.Connected) {
				return false;
			}

			// first, check the active connections and simply check the state of the active connections
			bool success = false;

			// if(connectionInformation != null) {
			// 	TcpState stateOfConnection = connectionInformation.State;
			//
			// 	if(stateOfConnection == TcpState.Established) {
			// 		success = true;
			// 	}
			// }
			//
			// if((connectionInformation != null) && !success) {
			// 	return false;
			// }

			bool blockingState = false;
			DateTime start = DateTimeEx.CurrentTime;

			try {
				// next, we try send a 0 byte array and see if we are still connected
				blockingState = socket.Blocking;

				socket.Blocking = false;
				socket.Send(Empty, 0, 0);
				latency = DateTimeEx.CurrentTime - start;

				success = true;
			} catch(ObjectDisposedException oe) {
				return false;
			} catch(SocketException e) {
				// 10035 == WSAEWOULDBLOCK
				if(e.NativeErrorCode.Equals(10035)) {
					//Still Connected, but the Send would block
					success = true;
				} else {
					return false;
				}
			} finally {
				try {
					socket.Blocking = blockingState;
				} catch(ObjectDisposedException oe) {
					success = false;
				}
			}

			if(!success) {
				return false;
			}

			// after this, we updated the status of connected, so we can give it a try
			if(!socket.Connected) {
				return false;
			}

			// sometimes the empty send technique does not work (with network streams notably), so we do a poll to confirm.
			try {
				start = DateTimeEx.CurrentTime;
				success = !(socket.Poll(1, SelectMode.SelectRead) && (socket.Available == 0));
				latency = (latency + (DateTimeEx.CurrentTime - start)) / 2;
			} catch(SocketException) {
				return false;
			}

			if(!success) {
				return false;
			}

			if(!socket.Connected) {
				return false;
			}

			// note: the bellow technique does not work for us. the connection might have no data and be perfectly valid
			// // finally, poll the sock to see if we are still connected
			// try {
			// 	return !((socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0)) || !socket.Connected);
			// } catch {
			// 	return false;
			// }

			return true;
		}
	}
}