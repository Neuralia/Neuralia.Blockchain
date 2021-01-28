using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Nito.AsyncEx;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {

	public interface ITcpValidatorConnection : IDisposableExtended {

		EndPoint RemoteEndPoint { get; }
		IPMode IPMode { get; }
		NetworkEndPoint EndPoint { get; }
		ConnectionState State { get; }
		bool HasConnected { get; }

		Task<ByteArray> ReadData(int size, CancellationToken cancellationNeuralium = default);
		void SendSocketBytes(in Span<byte> bytes);
		void SendSocketBytes(SafeArrayHandle bytes);
	}

	public class TcpValidatorConnection : ITcpValidatorConnection {

		protected readonly AsyncLock disposeLocker = new AsyncLock();

		protected readonly Action<Exception> exceptionCallback;

		protected readonly bool isServer;

		private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
		protected readonly AsyncLock sendBytesLocker = new AsyncLock();

		public bool HasConnected { get; private set; }

		/// <summary>
		///     The socket we're managing.
		/// </summary>
		protected readonly Socket socket;

		protected NetworkStream networkStream;
		private volatile ConnectionState state;

		protected CancellationTokenSource tokenSource;

		public TcpValidatorConnection(Action<Exception> exceptionCallback, bool isServer = false) {
			this.isServer = isServer;

			this.State = ConnectionState.NotConnected;
			this.exceptionCallback = exceptionCallback;

			this.tokenSource = new CancellationTokenSource();
		}

		/// <summary>
		///     Creates a TcpValidatorConnection from a given TCP Socket. usually called by the TcpServer
		/// </summary>
		/// <param name="socket">The TCP socket to wrap.</param>
		public TcpValidatorConnection(Socket socket, Action<Exception> exceptionCallback, bool isServer = false) : this(exceptionCallback, isServer) {
			//Check it's a TCP socket
			if(socket.ProtocolType != ProtocolType.Tcp) {
				throw new ArgumentException("A TcpValidatorConnection requires a TCP socket.");
			}

			this.isServer = isServer;

			this.EndPoint = new NetworkEndPoint(socket.RemoteEndPoint);
			this.RemoteEndPoint = socket.RemoteEndPoint;

			this.socket = socket;
			this.socket.NoDelay = true;

			this.SocketNewlyConnected();

			this.State = ConnectionState.Connected;
		}

		public TcpValidatorConnection(NetworkEndPoint remoteEndPoint, Action<Exception> exceptionCallback, bool isServer = false) : this(exceptionCallback, isServer) {

			if(this.State != ConnectionState.NotConnected) {
				throw new InvalidOperationException("Cannot connect as the Connection is already connected.");
			}

			this.isServer = isServer;

			this.EndPoint = remoteEndPoint;
			this.RemoteEndPoint = remoteEndPoint.EndPoint;
			this.IPMode = remoteEndPoint.IPMode;

			//Create a socket
			if(NodeAddressInfo.IsAddressIpV4(remoteEndPoint)) {
				this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			} else {
				if(!Socket.OSSupportsIPv6) {
					throw new P2pException("IPV6 not supported!", P2pException.Direction.Send, P2pException.Severity.Casual);
				}

				this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
				this.socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
			}

			if(GlobalSettings.ApplicationSettings.SlowValidatorPort) {
				this.socket.InitializeSocketParameters();
			} else {
				this.socket.InitializeSocketParametersFast(TcpValidatorServer.BYTES_PER_REQUESTER);
			}
		}

		private bool IsDisposing { get; set; }

		public EndPoint RemoteEndPoint { get; }

		public IPMode IPMode { get; }

		public NetworkEndPoint EndPoint { get; }

		public ConnectionState State {

			get {
				using(this.disposeLocker.Lock()) {
					if(this.IsDisposed || this.IsDisposing) {
						return ConnectionState.NotConnected;
					}

					return this.state;
				}
			}

			private set {

				using(this.disposeLocker.Lock()) {
					if(this.IsDisposed || this.IsDisposing) {
						return;
					}

					this.state = value;
				}
			}
		}

		public async Task<ByteArray> ReadData(int size, CancellationToken cancellationNeuralium = default) {

			var read = false;
			DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(30);

			var resultBytes = ByteArray.Create(size);

			var bytesRead = 0;

			do {
				if(timeout < DateTime.Now) {
					throw new ApplicationException("Timeout out getting data");
				}

				Memory<byte> dataRead = await this.ReadDataFrame(size, this.networkStream, cancellationNeuralium, timeout).ConfigureAwait(false);

				dataRead.Span.CopyTo(resultBytes.Span.Slice(bytesRead, dataRead.Length));

				bytesRead += dataRead.Length;

			} while(bytesRead != size);

			return resultBytes;
		}

		public void SendSocketBytes(SafeArrayHandle bytes) {
			//Write the bytes to the socket

			this.SendSocketBytes(bytes.Span);
		}

		public void SendSocketBytes(in Span<byte> bytes) {
			//Write the bytes to the socket

			try {
				if(this.State != ConnectionState.Connected) {
					throw new SocketException((int) SocketError.Shutdown);
				}

				using(this.sendBytesLocker.Lock()) {

					// now the message
					this.networkStream.Write(bytes);
					this.networkStream.Flush();
				}
			} catch(Exception e) {
				var he = new P2pException("Could not send data as an error occured.", P2pException.Direction.Send, P2pException.Severity.Casual, e);
				this.Close();

				throw he;
			}
		}

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void SocketNewlyConnected() {
			if(this.networkStream == null) {
				this.networkStream = new NetworkStream(this.socket, false);
				this.networkStream.WriteTimeout = 10000;
				this.networkStream.ReadTimeout = 10000;
			}

			this.HasConnected = true;
		}

		public void Connect(Func<SafeArrayHandle> transformBytes, int timeout = 5000) {
			if(this.IsDisposed || this.IsDisposing) {
				throw new SocketException((int) SocketError.Shutdown);
			}

			using SafeArrayHandle bytes = transformBytes();

			if((bytes == null) || bytes.IsEmpty) {
				throw new TcpApplicationException("Handshake bytes can not be null");
			}

			//Connect
			this.State = ConnectionState.Connecting;

			try {
				// we want this synchronously
				EndPoint endpoint = this.RemoteEndPoint;

				if(NodeAddressInfo.IsAddressIpV4(this.EndPoint) && NodeAddressInfo.IsAddressIpv4MappedToIpV6(this.EndPoint)) {
					endpoint = new IPEndPoint(NodeAddressInfo.GetAddressIpV4(this.EndPoint), this.EndPoint.EndPoint.Port);
				}

				var connect = Task.Factory.FromAsync(this.socket.BeginConnect, this.socket.EndConnect, endpoint, null);
				var success = connect.Wait(TimeSpan.FromSeconds(10));
				
				if(!success) {
					throw new SocketException((int) SocketError.TimedOut);
				}

				if(this.socket.IsReallyConnected()) {
					this.SocketNewlyConnected();
				} else {
					this.Dispose();

					throw new SocketException((int) SocketError.Shutdown);
				}
			} catch(Exception e) {
				this.Dispose();

				throw new P2pException("Could not connect as an exception occured.", P2pException.Direction.Send, P2pException.Severity.Casual, e);
			}

			//Set connected
			this.State = ConnectionState.Connected;

			// Start receiving data
			this.SendSocketBytes(bytes);

			this.resetEvent.Reset();

		}

		private async Task<Memory<byte>> ReadDataFrame(int size, NetworkStream networkStream, CancellationToken ct, DateTime timeout) {

			Memory<byte> buffer = new byte[size];
			Task<int> asyncReadTask = networkStream.ReadAsync(buffer, ct).AsTask();

			while(true) {

				await Task.WhenAny(asyncReadTask, Task.Delay(10000, ct)).ConfigureAwait(false);

				if(ct.IsCancellationRequested) {
					ct.ThrowIfCancellationRequested();
				}

				if(!asyncReadTask.IsCompleted) {
					continue;
				}

				// ReSharper disable once AsyncConverter.AsyncWait
				if(asyncReadTask.Result != 0) {
					// ReSharper disable once AsyncConverter.AsyncWait
					return buffer.Slice(0, asyncReadTask.Result);
				}

				if(timeout < DateTime.Now) {
					throw new ApplicationException("Timeout out getting data");
				}

				Thread.Sleep(5);
			}
		}

		public void Close() {
			this.Dispose();
		}

		private void Dispose(bool disposing) {

			var disposingChanged = false;

			using(this.disposeLocker.Lock()) {

				if(this.IsDisposed || !disposing || this.IsDisposing) {
					return;

				}

				if(!this.IsDisposed && !this.IsDisposing) {
					this.IsDisposing = true;
					disposingChanged = true;
				}

			}

			// good to sleep a little, give it some time
			//note this wait seems to be required for the buffer to have time to clear. if we dont wait, sometimes it seems to close while data remains to be sent.
			// this happens despite lingering and Diconnect and wait.
			Thread.Sleep(100);

			try {

				if(!this.IsDisposed || disposingChanged) {

					this.DisposeAll();
				}
			} finally {
				this.IsDisposed = true;
				this.IsDisposing = false;
			}

		}

		~TcpValidatorConnection() {
			this.Dispose(false);
		}

		protected virtual void DisposeAll() {

			// give it a chance to stop cleanly by cancellation

			try {
				this.DisposeSocket();

			} catch {

			}

			try {
				this.tokenSource?.Cancel();
			} catch {

			}

			try {
				this.resetEvent?.Dispose();
			} catch {

			}

			try {
				this.tokenSource?.Dispose();
				this.tokenSource = null;
			} catch {

			}

			this.State = ConnectionState.NotConnected;
		}

		protected virtual void DisposeSocket() {
			try {
				if(this.socket?.Connected ?? false) {
					try {
						this.socket?.Shutdown(SocketShutdown.Both);
					} catch {
						// do nothing, we tried
					} finally {
						Thread.Sleep(500);
						this.socket?.Disconnect(false);
					}
				}
			} finally {
				this.socket?.Dispose();
			}
		}

		protected enum HandshakeStatuses {
			NotStarted,
			VersionSentNoBytes,
			VersionReceivedNoBytes,
			Completed,
			Unusable
		}
	}

}