using System;
using System.Net;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;

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
			
			this.StartServer(GlobalSettings.ApplicationSettings.IPProtocolServer);
		}

		private void StartServer(IPMode ipMode) {

			if(ipMode == IPMode.Unknown) {
				throw new ApplicationException("Invalid IP protocol mode.");
			}
			try {
				Repeater.Repeat(() => {

					try {
						this.tcpServer = this.CreateTcpServer(ipMode);

						this.tcpServer.NewConnection += (listener, connection, buffer) => {
							NLog.Default.Verbose("New connection received");

							if(this.NewConnectionReceived != null) {
								this.NewConnectionReceived(listener, connection, buffer);
							}

							return Task.CompletedTask;
						};

						this.tcpServer.NewConnectionRequestReceived += connection => {
							NLog.Default.Verbose("New connection request received");

							if(this.NewConnectionRequestReceived != null) {
								this.NewConnectionRequestReceived(connection);
							}
						};

						string protocolTypes = "";
						
						if(this.tcpServer.IPMode == IPMode.IPv4) {
							protocolTypes = "IPv4 mode";
						}
						else if(this.tcpServer.IPMode == IPMode.IPv6) {
							protocolTypes = "IPv6 mode";
						}
						else if(this.tcpServer.IPMode == IPMode.Both) {
							protocolTypes = "IPv4 and IPv6 mode";
						}
						NLog.Default.Information($"Listening on port {this.port} in {protocolTypes}");

						this.tcpServer.Start();
					} catch {
						this.tcpServer.Dispose();

						throw;
					}
				});
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to start network listener");

				this.tcpServer.Dispose();
				this.tcpServer = null;

				throw;
			}
		}

		protected virtual ITcpServer CreateTcpServer(IPMode ipMode) {

			return new TcpServer(ipMode, this.port, this.TriggerExceptionOccured);
		}

		public event TcpConnection.ExceptionOccured ExceptionOccured;

		protected void TriggerExceptionOccured(Exception exception, ITcpConnection connection) {
			if(this.ExceptionOccured != null) {
				this.ExceptionOccured(exception, connection);
			}
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