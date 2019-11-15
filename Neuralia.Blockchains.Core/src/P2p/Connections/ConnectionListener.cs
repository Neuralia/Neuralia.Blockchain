using System;
using System.Net;
using System.Net.Sockets;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public interface IConnectionListener : IDisposableExtended {
		event TcpServer.MessageBytesReceived NewConnectionReceived;
		event Action<ITcpConnection> NewConnectionRequestReceived;
		void Start();
	}

	public class ConnectionListener : IConnectionListener {

		private readonly int port;
		private ITcpServer tcpServer;

		public ConnectionListener(int port, ServiceSet serviceSet) {
			this.port = port;

		}

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public event TcpServer.MessageBytesReceived NewConnectionReceived;
		public event Action<ITcpConnection> NewConnectionRequestReceived;

		public void Start() {

			if(Socket.OSSupportsIPv6) {
				this.StartServer(IPMode.IPv6);
			} else {
				this.StartServer(IPMode.IPv4);
			}
		}

		private void StartServer(IPMode ipMode) {
			try {
				Repeater.Repeat(() => {

					try {
						this.tcpServer = this.CreateTcpServer(ipMode);

						this.tcpServer.NewConnection += (listener, connection, buffer) => {
							Log.Verbose("New connection received");

							this.NewConnectionReceived?.Invoke(listener, connection, buffer);
						};

						this.tcpServer.NewConnectionRequestReceived += connection => {
							Log.Verbose("New connection request received");

							this.NewConnectionRequestReceived?.Invoke(connection);
						};

						Log.Information($"Listening on port {this.port} in {ipMode}{(ipMode == IPMode.IPv6 ? " and " + IPMode.IPv4 : "")} mode");

						this.tcpServer.Start();
					} catch {
						this.tcpServer.Dispose();

						throw;
					}
				});
			} catch(Exception ex) {
				Log.Error(ex, "Failed to start network listener");

				this.tcpServer.Dispose();
				this.tcpServer = null;

				throw;
			}
		}

		protected virtual ITcpServer CreateTcpServer(IPMode ipMode) {
			return new TcpServer(new NetworkEndPoint(IPAddress.Any, this.port, ipMode), this.TriggerExceptionOccured);
		}

		public event TcpConnection.ExceptionOccured ExceptionOccured;

		protected void TriggerExceptionOccured(Exception exception, ITcpConnection connection) {
			this.ExceptionOccured?.Invoke(exception, connection);
		}

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {
				try {
					this.tcpServer?.Stop();

					this.tcpServer?.Dispose();

				} finally {
					this.tcpServer = null;
				}
			}
			this.IsDisposed = true;
		}

		~ConnectionListener() {
			this.Dispose(false);
		}
	}
}