using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network {

	public interface ITcpServer {
		/// <summary>
		///     The local end point the listener is listening for new clients on.
		/// </summary>
		EndPoint EndPoint { get; }

		/// <summary>
		///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
		/// </summary>
		IPMode IPMode { get; }

		bool IsDisposed { get; }
		event TcpServer.MessageBytesReceived NewConnection;

		/// <summary>
		///     called when we receive a new request for a connection
		/// </summary>
		event Action<ITcpConnection> NewConnectionRequestReceived;

		void Close();

		/// <summary>
		///     Call to dispose of the connection listener.
		/// </summary>
		void Dispose();

		void Start();

		void Stop();
	}

	/// <summary>
	///     Listens for new TCP connections and creates TCPConnections for them.
	/// </summary>
	/// <inheritdoc />
	public class TcpServer : ITcpServer {

		public delegate Task MessageBytesReceived(TcpServer listener, ITcpConnection connection, SafeArrayHandle buffer);

		protected readonly List<ITcpConnection> connections = new List<ITcpConnection>();

		private readonly TcpConnection.ExceptionOccured exceptionCallback;

		/// <summary>
		///     The socket listening for connections.
		/// </summary>
		private readonly Socket listener;

		private readonly object locker = new object();

		private readonly ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters;

		public TcpServer(NetworkEndPoint endPoint, TcpConnection.ExceptionOccured exceptionCallback, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) {
			this.exceptionCallback = exceptionCallback;
			this.EndPoint = endPoint.EndPoint;

			if(protocolMessageFilters == null) {
				this.protocolMessageFilters = TcpConnection.ProtocolMessageTypes.All;
			} else {
				this.protocolMessageFilters = protocolMessageFilters;
			}
			
			if(!Socket.OSSupportsIPv6 && endPoint.IPMode.HasFlag(IPMode.IPv6)) {
				throw new P2pException("IPV6 not supported!", P2pException.Direction.Receive, P2pException.Severity.Casual);
			}
			
			if(TcpConnection.IPv6Supported && !GlobalSettings.ApplicationSettings.ForceIpv4Socket) {
				this.listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

				if(endPoint.IPMode.HasFlag(IPMode.IPv4)) {
					this.IPMode = IPMode.Both;
					this.listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
					this.listener.DualMode = true;
					
				} else {
					this.IPMode = IPMode.IPv6;
					this.listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
				}
			} else {
				this.IPMode = IPMode.IPv4;
				this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			}

			this.listener.InitializeSocketParameters();

			// seems to be needed in case the listener is not completely disposed yet (on linux and MacOs)
			//https://github.com/dotnet/corefx/issues/24562
			//TODO: this is a bug fix, and maybe in the future we may not need the below anymore.
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				this.listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				this.listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
			}
		}

		/// <summary>
		///     The local end point the listener is listening for new clients on.
		/// </summary>
		public EndPoint EndPoint { get; }

		/// <summary>
		///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
		/// </summary>
		public IPMode IPMode { get; }

		public bool IsDisposed { get; private set; }

		public event MessageBytesReceived NewConnection;
		public event Action<ITcpConnection> NewConnectionRequestReceived;

		public void Close() {
			this.Dispose();
		}

		/// <summary>
		///     Call to dispose of the connection listener.
		/// </summary>
		public void Dispose() {
			this.Dispose(true);
		}

		public void Start() {
			if(this.IsDisposed) {
				throw new ApplicationException("Cannot start adisposed tcp server");
			}

			try {

				this.listener.Bind(this.EndPoint);
				this.listener.Listen((int) SocketOptionName.MaxConnections);

				this.listener.BeginAccept(AcceptCallback, new ConnectionInstance {listener = this.listener, server = this});

			} catch(SocketException e) {
				throw new P2pException("Could not start listening as a SocketException occured", P2pException.Direction.Receive, P2pException.Severity.Casual, e);
			}
		}

		public void Stop() {

			this.Dispose();
		}

		private void InvokeNewConnection(SafeArrayHandle bytes, ITcpConnection connection) {
			if(this.NewConnection != null) {
				this.NewConnection(this, connection, bytes);
			}
		}

		private static void AcceptCallback(IAsyncResult result) {

			try {
				// Get the socket that handles the client request.  
				ConnectionInstance connectionInstance = (ConnectionInstance) result.AsyncState;

				Socket tcpSocket = null;

				try {
					if(connectionInstance.listener == null) {
						throw new ObjectDisposedException("listener");
					}

					tcpSocket = connectionInstance.listener.EndAccept(result);

				} catch(ObjectDisposedException) {
					//If the socket's been disposed then we can just end there.
					return;
				}

				// get the next connection
				connectionInstance.listener.BeginAccept(AcceptCallback, connectionInstance);

				IPEndPoint endPoint = (IPEndPoint) tcpSocket.RemoteEndPoint;

				// first thing, ask IPMarshall
				if(connectionInstance.server.CheckShouldDisconnect(endPoint)) {
					try {
						tcpSocket.Close();
					} catch(ObjectDisposedException) {
						//If the socket's been disposed then we can just end there.
					}

					return;
				}

				try {
					connectionInstance.server.AcceptNewConnection(tcpSocket);
				} catch(Exception ex) {
					try {
						tcpSocket?.Close();
						tcpSocket?.Dispose();
						
					} catch {
						//TODO: tell the IPMarshall?
					}
					IPMarshall.Instance.Quarantine(endPoint.Address, IPMarshall.QuarantineReason.ConnectionBroken, DateTimeEx.CurrentTime.AddSeconds(3));
					NLog.Default.Verbose("Failed to establish connection");
				}
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to listen for connections. this is bad. trying to reestablish connection.");

				try {
					// lets try again
					ConnectionInstance connectionInstance = (ConnectionInstance) result.AsyncState;
					connectionInstance.listener.BeginAccept(AcceptCallback, connectionInstance);
				} catch {
					NLog.Default.Fatal(ex, "Failed to listen for any connections. this is seriously critical! server is not listening anymore.");
				}
			}

		}
		
		protected virtual bool CheckShouldDisconnect(IPEndPoint endPoint) {
			return IPMarshall.Instance.RequestIncomingConnectionClearance(endPoint.Address) == false;
		}

		public void AcceptNewConnection(Socket tcpSocket) {
			//Sort the event out
			ITcpConnection tcpConnection = this.CreateTcpConnection(tcpSocket, this.exceptionCallback, this.protocolMessageFilters);

			lock(this.locker) {
				this.connections.Add(tcpConnection);
			}

			// make sure this connection is acceptable and not already created
			if(this.NewConnectionRequestReceived != null) {
				this.NewConnectionRequestReceived(tcpConnection);
			}

			//Wait for handshake
			tcpConnection.StartWaitingForHandshake(bytes => {
				//Invoke
				this.InvokeNewConnection(bytes, tcpConnection);
				
				return Task.CompletedTask;
			});

			lock(this.locker) {
				tcpConnection.Disconnected += (sender, args) => {
					lock(this.locker) {
						this.connections.Remove(tcpConnection);
					}
				};
			}
		}

		protected virtual ITcpConnection CreateTcpConnection(Socket socket, TcpConnection.ExceptionOccured exceptionCallback, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) {

			if(GlobalSettings.ApplicationSettings.SocketType == AppSettingsBase.SocketTypes.Duplex) {
				return new TcpDuplexConnection(socket, exceptionCallback, true, protocolMessageFilters);
			}

			if(GlobalSettings.ApplicationSettings.SocketType == AppSettingsBase.SocketTypes.Stream) {
				return new TcpStreamConnection(socket, exceptionCallback, true, protocolMessageFilters);
			}

			throw new ApplicationException("Invalid socket type");
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				foreach(ITcpConnection connection in this.connections.ToList()) {
					connection.Close();
				}

				if(this.listener.Connected) {
					this.listener.Shutdown(SocketShutdown.Both);
				}

				this.listener.Dispose();
			}

			this.IsDisposed = true;
		}

		public class ConnectionInstance {
			public Socket listener;

			public TcpServer server;
		}
	}
}