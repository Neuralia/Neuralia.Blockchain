using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {

	public interface ITcpValidatorServer {

		bool IsRunning { get; }

		/// <summary>
		///     The local end point the listener is listening for new clients on.
		/// </summary>
		EndPoint EndPoint { get; }

		/// <summary>
		///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
		/// </summary>
		IPMode IPMode { get; }

		bool IsDisposed { get; }

		void Close();

		/// <summary>
		///     Call to dispose of the connection listener.
		/// </summary>
		void Dispose();

		void Start();

		void Stop();

		void RegisterBlockchainDelegate(BlockchainType blockchainType, IAppointmentValidatorDelegate appointmentValidatorDelegate, Func<bool> isInAppointmentWindow);
		void UnregisterBlockchainDelegate(BlockchainType blockchainType);
		bool BlockchainDelegateEmpty { get; }
	}

	/// <summary>
	///     Listens for new TCP connections and creates TCPConnections for them.
	/// </summary>
	/// <inheritdoc />
	public class TcpValidatorServer : ITcpValidatorServer {

		public const    byte PING_BYTE = 255;
		public const    byte PONG_BYTE = 255;
		public delegate Task MessageBytesReceived(TcpServer listener, ITcpConnection connection, SafeArrayHandle buffer);

		private readonly Action<Exception> exceptionCallback;

		/// <summary>
		///     The socket listening for connections.
		/// </summary>
		private Socket listener;

		private readonly object          locker   = new object();
		private readonly SafeArrayHandle pongByte = SafeArrayHandle.Wrap(new [] {PONG_BYTE });

		public TcpValidatorServer(NetworkEndPoint endPoint, Action<Exception> exceptionCallback) {
			this.exceptionCallback = exceptionCallback;
			this.EndPoint = endPoint.EndPoint;
			this.IPMode = endPoint.IPMode;
			this.networkEndPoint = endPoint;
		}

		public bool IsRunning { get; private set; } = false;

		/// <summary>
		///     The local end point the listener is listening for new clients on.
		/// </summary>
		public EndPoint EndPoint { get; }

		private NetworkEndPoint networkEndPoint;

		/// <summary>
		///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
		/// </summary>
		public IPMode IPMode { get; }

		public bool IsDisposed { get; private set; }

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

			this.Stop();
			try {
				if(NodeAddressInfo.IsAddressIpV4(this.networkEndPoint)) {
					this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				} else {
					if(!Socket.OSSupportsIPv6) {
						throw new P2pException("IPV6 not supported!", P2pException.Direction.Receive, P2pException.Severity.Casual);
					}

					this.listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
					this.listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
				}
				
				// seems to be needed in case the listener is not completely disposed yet (on linux and MacOs)
				//https://github.com/dotnet/corefx/issues/24562
				//TODO: this is a bug fix, and maybe in the future we may not need the below anymore.
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
					this.listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					this.listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
				}

				this.listener.InitializeSocketParameters();
				
				this.listener.Bind(this.EndPoint);
				this.listener.Listen((int) SocketOptionName.MaxConnections);

				this.listener.BeginAccept(AcceptCallback, new ValidatorConnectionInstance {listener = this.listener, server = this});
				this.IsRunning = true;

				NLog.Default.Information($"Validator TCP Server started");
			} catch(SocketException e) {
				throw new P2pException("Could not start listening as a SocketException occured", P2pException.Direction.Receive, P2pException.Severity.Casual, e);
			}
		}

		public void Stop() {
			if(this.IsRunning) {
				try {
					try {
						if(this.listener?.Connected ?? false) {
							this.listener.Shutdown(SocketShutdown.Both);
						}
					} catch {

					}

					this.listener?.Dispose();
					
					NLog.Default.Information($"Validator TCP Server stopped");
				} finally {
					this.IsRunning = false;
					this.listener = null;
				}
			}
		}

		private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates = new ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate>();
		private readonly ConcurrentDictionary<BlockchainType, Func<bool>>                    appointmentWindowChecks       = new ConcurrentDictionary<BlockchainType, Func<bool>>();

		public void RegisterBlockchainDelegate(BlockchainType blockchainType, IAppointmentValidatorDelegate appointmentValidatorDelegate, Func<bool> isInAppointmentWindow) {
			if(!this.appointmentValidatorDelegates.ContainsKey(blockchainType)) {
				appointmentValidatorDelegate.Initialize();
				this.appointmentValidatorDelegates.TryAdd(blockchainType, appointmentValidatorDelegate);
			}
			
			if(!this.appointmentWindowChecks.ContainsKey(blockchainType)) {
				this.appointmentWindowChecks.TryAdd(blockchainType, isInAppointmentWindow);
			}
		}

		public void UnregisterBlockchainDelegate(BlockchainType blockchainType) {
			if(this.appointmentValidatorDelegates.ContainsKey(blockchainType)) {
				this.appointmentValidatorDelegates.Remove(blockchainType, out var _);
			}
			
			if(this.appointmentWindowChecks.ContainsKey(blockchainType)) {
				this.appointmentWindowChecks.Remove(blockchainType, out var _);
			}
		}

		public bool BlockchainDelegateEmpty => !this.appointmentValidatorDelegates.Any();

		private static void AcceptCallback(IAsyncResult result) {

			try {
				// Get the socket that handles the client request.  
				var connectionInstance = (ValidatorConnectionInstance) result.AsyncState;

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

				var endPoint = (IPEndPoint) tcpSocket.RemoteEndPoint;

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
					}

					NLog.Default.Verbose("Failed to establish connection");
				}
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to listen for connections. this is bad. trying to reestablish connection.");

				try {
					// lets try again
					var connectionInstance = (ValidatorConnectionInstance) result.AsyncState;
					connectionInstance.listener.BeginAccept(AcceptCallback, connectionInstance);
				} catch {
					NLog.Default.Fatal(ex, "Failed to listen for any connections. this is seriously critical! server is not listening anymore.");
				}
			}
		}

		protected virtual bool CheckShouldDisconnect(IPEndPoint endPoint) {
			return IPMarshall.ValidationInstance.RequestIncomingConnectionClearance(endPoint.Address) == false;
		}

		public void AcceptNewConnection(Socket tcpSocket) {

			TcpValidatorConnection connection = null;

			try {
				connection = new TcpValidatorConnection(tcpSocket, ex => {
				}, true);

				using ByteArray frontBytes = connection.ReadData(ValidatorProtocolHeader.HEAD_BYTE_SIZE).WaitAndUnwrapException();

				if(frontBytes.Length == 0) {
					var endpoint = (IPEndPoint) tcpSocket.RemoteEndPoint;
					IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.PermanentBan, DateTimeEx.MaxValue);
				}
				
				byte frontByte = frontBytes[0];
				
				if(frontByte == PING_BYTE) {
					//TODO: ensure rate limiting here by IP.
					// send the pong
					connection.SendSocketBytes(this.pongByte);
				} else if(frontByte == ValidatorProtocolHeader.HEAD_BYTE && this.IsInAppointmentWindow()) {

					// first step, take the header
					using ByteArray headerBytes = connection.ReadData(ValidatorProtocolHeader.MAIN_HEADER_SIZE).WaitAndUnwrapException();

					var header = new ValidatorProtocolHeader();
					header.Rehydrate(frontByte, headerBytes);

					// first thing, valida header
					if(header.NetworkId != NetworkConstants.CURRENT_NETWORK_ID) {
						//blacklist
						var endpoint = (IPEndPoint) tcpSocket.RemoteEndPoint;
						IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.PermanentBan, DateTimeEx.MaxValue);
						return;
					}

					IValidatorProtocol protocol = ValidatorProtocolFactory.GetValidatorProtocolInstance(header.ProtocolVersion, header.ChainId, (type) => {
						if(this.appointmentValidatorDelegates.ContainsKey(type)) {
							return this.appointmentValidatorDelegates[type];
						}

						return null;
					});
					
					if(protocol == null) {
						return;
					}
					using var tokenSource = new CancellationTokenSource();
					protocol.HandleServerExchange(connection, tokenSource.Token).WaitAndUnwrapException(tokenSource.Token);
				} else {
					var endpoint = (IPEndPoint) tcpSocket.RemoteEndPoint;
					IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.PermanentBan, DateTimeEx.MaxValue);
				}
				
			} finally {
				connection?.Dispose();
			}
		}

		/// <summary>
		/// check if we have any appointment window happening
		/// </summary>
		/// <returns></returns>
		private bool IsInAppointmentWindow() {

			foreach(var key in this.appointmentWindowChecks.Keys) {
				if(this.appointmentWindowChecks[key]()) {
					return true;
				}
			}

			return false;
		}
		
		protected virtual ITcpValidatorConnection CreateTcpConnection(Socket socket, Action<Exception> exceptionCallback) {

			return new TcpValidatorConnection(socket, exceptionCallback, true);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				// foreach(ITcpConnection connection in this.connections.ToList()) {
				// 	connection.Close();
				// }

				this.Stop();
			}

			this.IsDisposed = true;
			this.IsRunning = false;
		}

		public class ValidatorConnectionInstance {
			public Socket listener;

			public TcpValidatorServer server;
		}
	}
}