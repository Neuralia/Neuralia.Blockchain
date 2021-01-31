using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.Tools {
	/// <summary>
	///     class used to simplify sending messages and bytes over a socket
	/// </summary>
	public class DataDispatcher {

		private readonly Action<PeerConnection> invalidConnectionCallback;

		private readonly RecursiveAsyncLock locker = new RecursiveAsyncLock();
		private readonly ITimeService timeService;

		public DataDispatcher(ITimeService timeService, Action<PeerConnection> invalidConnectionCallback) {
			this.timeService = timeService;
			this.invalidConnectionCallback = invalidConnectionCallback;
		}

		private SafeArrayHandle DehydrateMessage(INetworkMessageSet message) {
			SafeArrayHandle bytes = message.Dehydrate();

			return bytes;
		}

		public Task<bool> SendMessage(PeerConnection peerConnection, INetworkMessageSet message, LockContext lockContext) {
			message.BaseHeader.SentTime = this.timeService.CurrentRealTime;

			return this.SendBytes(peerConnection, this.DehydrateMessage(message), lockContext);
		}

		public Task<bool> SendFinalMessage(PeerConnection peerConnection, INetworkMessageSet message, LockContext lockContext) {
			message.BaseHeader.SentTime = this.timeService.CurrentRealTime;

			return this.SendFinalBytes(peerConnection, this.DehydrateMessage(message), lockContext);
		}

		public Task<bool> SendBytes(PeerConnection peerConnection, SafeArrayHandle data, LockContext lockContext) {
			return this.Send(() => this.SendBytesToPeer(peerConnection, data, lockContext));
		}

		public Task<bool> SendFinalBytes(PeerConnection peerConnection, SafeArrayHandle data, LockContext lockContext) {
			async Task<bool> SendAction() {
				try {
					return await SendBytesToPeer(peerConnection, data, lockContext).ConfigureAwait(false);
				} catch(Exception e) {
					NLog.Default.Verbose("error occured", e);

					return false;
				} finally {
					peerConnection.Dispose();
				}
			}

			return this.Send(SendAction);
		}

		private async Task<bool> SendBytesToPeer(PeerConnection peerConnection, SafeArrayHandle data, LockContext lockContext) {

			using(var session = await locker.LockAsync(lockContext).ConfigureAwait(false)) {

				ConnectionState state = peerConnection.connection.State;

				try {
					if(peerConnection.IsDisposed) {
						peerConnection.connection.Dispose();

						if(this.invalidConnectionCallback != null) {
							this.invalidConnectionCallback(peerConnection);
						}

						return false;
					}

					if(state == ConnectionState.NotConnected) {
						return await ConnectAndSendBytesToPeer(peerConnection, data, session).ConfigureAwait(false);
					}

					if(state != ConnectionState.Connected) {
						if(state == ConnectionState.Connecting) {
							//wait a little bit then retry
							int attempt = 1;
							int sleepTime = 100;

							do {
								Thread.Sleep(sleepTime);

								state = peerConnection.connection.State;

								if(state != ConnectionState.Connected) {
									sleepTime = 500;
									attempt++;
								}
							} while(attempt <= 3);
						}

						if(state == ConnectionState.Disconnecting) {
							throw new TcpApplicationException("Connection is in the process of disconnecting.");
						}

						if(state != ConnectionState.Connected) {
							throw new TcpApplicationException("Failed to connect to the peer.");
						}
					}

					peerConnection.connection.SendBytes(data);
				} catch(Exception ex) {
					NLog.Default.Verbose(ex, "Failed to send bytes to peer.");

					return false;
				}
			}

			return true;
		}

		private async Task<bool> ConnectAndSendBytesToPeer(PeerConnection peerConnection, SafeArrayHandle data, LockContext lockContext) {

			if(peerConnection.IsDisposed) {
				peerConnection.connection.Dispose();

				if(this.invalidConnectionCallback != null) {
					this.invalidConnectionCallback(peerConnection);
				}

				return false;
			}

			ConnectionState state = peerConnection.connection.State;

			using(var session = await locker.LockAsync(lockContext).ConfigureAwait(false)) {
				try {
					if(state != ConnectionState.NotConnected) {

						if(state == ConnectionState.Connecting) {
							//wait a little bit then retry
							int attempt = 1;
							int sleepTime = 100;

							do {
								Thread.Sleep(sleepTime);

								state = peerConnection.connection.State;

								if(state != ConnectionState.Connected) {
									sleepTime = 500;
									attempt++;
								}
							} while(attempt <= 3);
						}

						if(state == ConnectionState.Connected) {
							return await SendBytesToPeer(peerConnection, data, session).ConfigureAwait(false);
						}

						if(state == ConnectionState.Disconnecting) {
							//wait a little bit then retry
							int attempt = 1;
							int sleepTime = 100;

							do {
								Thread.Sleep(sleepTime);

								state = peerConnection.connection.State;

								if(state != ConnectionState.NotConnected) {
									sleepTime = 500;
									attempt++;
								}
							} while(attempt <= 3);
						}

						if(state != ConnectionState.NotConnected) {
							throw new TcpApplicationException("Connection is already connected. could not connect.");
						}
					}

					try {
						await peerConnection.connection.Connect(data).ConfigureAwait(false);
					} catch(Exception ex) {
						throw new TcpApplicationException("Failed to connect to client. They are not available", ex);
					}
				} catch(TcpApplicationException tcpEx) {

					if(tcpEx.InnerException is P2pException p2pEx) {
						if(p2pEx.InnerException is SocketException socketException) {

						} else {
							NLog.Default.Verbose(tcpEx, "Failed to send bytes to peer.");
						}
					} else {
						NLog.Default.Verbose(tcpEx, "Failed to send bytes to peer.");
					}

					return false;
				} catch(P2pException p2pEx) {
					if(p2pEx.InnerException is SocketException socketException) {
						if((socketException.SocketErrorCode == SocketError.TimedOut) || (socketException.SocketErrorCode == SocketError.Shutdown)) {
							// thats it, we time out, let's report it simply
							NLog.Default.Verbose("Failed to send bytes to peer. socket connection was unavailable");
						} else {
							NLog.Default.Verbose(p2pEx, "Failed to send bytes to peer.");
						}
					} else {
						NLog.Default.Verbose(p2pEx, "Failed to send bytes to peer.");
					}

					return false;
				} catch(Exception ex) {
					NLog.Default.Verbose(ex, "Failed to send bytes to peer.");

					return false;
				}
			}

			return true;
		}

		/// <summary>
		///     Attempt some connection event x amount of times, checking for cancel every time
		/// </summary>
		/// <param name="action"></param>
		/// <param name="trycount"></param>
		/// <returns></returns>
		private async Task<bool> Send(Func<Task<bool>> action, int tryCount = 3) {
			int counter = 0;
			Exception lastException = null;

			try {
				do {
					try {
						return await action().ConfigureAwait(false);
					} catch(Exception ex) {
						// lets make sure we check before every loop so we dont waste time. every connection can idle for 5 seconds. thats long at shutdown

						counter++;
						lastException = ex;
						Thread.Sleep(100);
					}

					if(counter == tryCount) {
						throw new TcpApplicationException("Failed to connect to client. They are not available", lastException);
					}
				} while(true);
			} catch(Exception ex) {
				NLog.Default.Verbose(ex, "Failed to send bytes to peer.");
			}

			return false;
		}
	}
}