using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Neuralia.Blockchains.Core.P2p.Connections;

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
			var inOptionValues = new byte[size * 3]; // 4 * 3 = 12
			bool OnOff = true;

			BitConverter.GetBytes((uint) (OnOff ? 1 : 0)).CopyTo(inOptionValues, 0);
			BitConverter.GetBytes((uint) KeepAliveTime).CopyTo(inOptionValues, size);
			BitConverter.GetBytes((uint) KeepAliveInterval).CopyTo(inOptionValues, size * 2);

			// windows only 
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
			}

			//TODO: at the moment of this writing, linux and macos do not support keep alive interval. code is almost comitted, check this again later
			//https://github.com/dotnet/corefx/issues/25040
			//https://github.com/dotnet/corefx/pull/29963
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				// implement this when Portable support for TCP keepalive is functional
				//socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, true);
			}
		}

		public static void InitializeSocketParameters(this Socket socket) {
			// Ensure a keep alive to know if connection is still active
			socket.SetSocketKeepAliveValues(2000, 1000);

			// Disable the Nagle Algorithm for this tcp socket.
			socket.NoDelay = true;

			// The socket will linger for 3 seconds after 
			// Socket.Close is called.
			socket.LingerState = new LingerOption(true, 3);

			// Don't allow another socket to bind to this port.
			socket.ExclusiveAddressUse = true;

			// Set the receive buffer size to 8k
			socket.ReceiveBufferSize = 8192;

			// Set the timeout for synchronous receive methods to 
			// 1 second (1000 milliseconds.)
			socket.ReceiveTimeout = 1000;

			// Set the send buffer size to 8k.
			socket.SendBufferSize = 8192;

			// Set the timeout for synchronous send methods
			// to 1 second (1000 milliseconds.)			
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
		public static bool IsReallyConnected(this Socket socket, TcpConnectionInformation connectionInfortmation) {

			// first, check the active connections and simply check the state of the active connections
			bool success = false;

			if(connectionInfortmation != null) {
				TcpState stateOfConnection = connectionInfortmation.State;

				if(stateOfConnection == TcpState.Established) {
					success = true;
				}
			}

			if((connectionInfortmation != null) && !success) {
				return false;
			}

			bool blockingState = false;

			try {
				// next, we try send a 0 byte array and see if we are still connected
				blockingState = socket.Blocking;

				socket.Blocking = false;
				socket.Send(Empty, 0, 0);

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

			// finally, poll the sock to see if we are still connected
			try {
				return !((socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0)) || !socket.Connected);
			} catch {
				return false;
			}
		}
	}
}