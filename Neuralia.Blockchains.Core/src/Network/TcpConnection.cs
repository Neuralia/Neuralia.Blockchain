﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Collections;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.Network.Protocols;
using Neuralia.Blockchains.Core.Network.ReadingContexts;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Extensions;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Mathematics;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using Nito.AsyncEx;

namespace Neuralia.Blockchains.Core.Network {

	public interface ITcpConnection : IDisposableExtended {

		EndPoint RemoteEndPoint { get; }
		IPMode IPMode { get; }
		NetworkEndPoint EndPoint { get; }
		ConnectionState State { get; }
		double Latency(TcpConnection.LatencyTypes latencyType);
		double LatencyStd(TcpConnection.LatencyTypes latencyType);
		double ConnectionLatency { get; }
		double ConnectionLatencyStd { get; }
		
		double BlockSyncLatency { get; }
		double BlockSyncLatencyStd { get; }

		void AddLatency(DateTime start);
		void AddBlockSyncLatency(DateTime start);
		Guid ReportedUuid { get; }
		Guid InternalUuid { get; }
		bool IsConnectedUuidProvidedSet { get; }

		event TcpConnection.MessageBytesReceived DataReceived;

		event TcpConnection.MessageBytesSent DataSent;
		event EventHandler<DisconnectedEventArgs> Disconnected;
		event Action Connected;
		event Action Disposing;

		event Action<Guid> ConnectedUuidProvided;

		void Close();
		Task Connect(SafeArrayHandle bytes);
		Task Connect(SafeArrayHandle bytes, TcpConnection.SocketParameterTypes socketParameterType, int socketBuffer = 0);
		bool SendMessage(long hash);
		void SendBytes(SafeArrayHandle bytes);
		void StartWaitingForHandshake(TcpConnection.MessageBytesReceived handshakeCallback);
		bool CheckConnected(bool force = false);

		Task<bool> PerformCounterConnection(int port);
	}

	public interface IProtocolTcpConnection : ITcpConnection {
		void SendSocketBytes(SafeArrayHandle bytes, bool sendSize = true);
		void SendSocketBytes(in Span<byte> bytes, bool sendSize = true);
	}

	public static class TcpConnection {

		public enum SocketParameterTypes {
			Sturdy, Fast
		}

		public class LatencySet {
			public readonly FixedConcurrentQueue<double> latencies = new (100);
			public double latency = 0;
			public double latencyStd = Double.MaxValue;
			public double Latency => this.latency;
			public double LatencyStd => this.latencyStd;
		}

		public enum LatencyTypes {
			Connection, BlockSync
		}

		
		private static bool? ipv6Supported;

		public static bool IPv6Supported {
			get {
				if(!ipv6Supported.HasValue) {
					ipv6Supported = false;
					if(GlobalSettings.ApplicationSettings.IPProtocolServer.HasFlag(IPMode.IPv6) || GlobalSettings.ApplicationSettings.IPProtocolClient.HasFlag(IPMode.IPv6)) {
						ipv6Supported = Socket.OSSupportsIPv6;
					}
				}

				return ipv6Supported.Value;
			}
		}

		public delegate void ExceptionOccured(Exception exception, ITcpConnection connection);

		public delegate Task MessageBytesReceived(SafeArrayHandle buffer);

		public delegate void MessageBytesSent(SafeArrayHandle buffer);

		/// <summary>
		///     All the protocol messages we support
		/// </summary>
		[Flags]
		public enum ProtocolMessageTypes : short {
			None = 0,
			Tiny = 1,
			Small = 1 << 1,
			Medium = 1 << 2,
			Large = 1 << 3,
			Split = 1 << 4,
			All = Tiny | Small | Medium | Large | Split
		}

		/// <summary>
		///     Here we try a quick counterconnect to establish if their connection port is open and available
		/// </summary>
		/// <param name="port"></param>
		/// <returns></returns>
		/// <exception cref="P2pException"></exception>
		public static async Task<bool> PerformCounterConnection(IPAddress address, int port) {

			Socket counterSocket = null;
			bool success = false;
			try {
				var node = new NodeAddressInfo(address, NodeInfo.Unknown);
				
				//TODO: this can be made more efficient by releasing the thread but keeping the timeout.
				
				// make sure we are in the right ip format
				
				var endpoint = new NetworkEndPoint(new IPEndPoint(node.AdjustedAddress, port));
				
				(counterSocket, success) = await SocketExtensions.ConnectSocket(endpoint, s => {
					
					s.InitializeSocketParametersFast(ProtocolFactory.HANDSHAKE_COUNTERCONNECT_BYTES.Length);
				}, 3, (s, e) => {
					
					return Task.Factory.FromAsync(s.BeginConnect(e, null, null), result => {
						try {
							if(s.Connected && (s.Send(ProtocolFactory.HANDSHAKE_COUNTERCONNECT_BYTES) == ProtocolFactory.HANDSHAKE_COUNTERCONNECT_BYTES.Length)) {

								return true;
							}
						} catch {

						}

						return false;
					}, TaskCreationOptions.None);
				}).ConfigureAwait(false);
				
				if(!success) {
					throw new SocketException((int) SocketError.TimedOut);
				}

				return true;

			} catch(Exception ex) {
				// do nothing, we got our answer

			} finally {

				try {
					counterSocket?.Shutdown(SocketShutdown.Both);
				} catch {
					// do nothing, we got our answer
				}

				try {
					counterSocket?.Dispose();
				} catch {
					// do nothing, we got our answer
				}
			}

			return false;
		}
	}

	public abstract class TcpConnection<READING_CONTEXT> : IProtocolTcpConnection
		where READING_CONTEXT : ITcpReadingContext {

		/// <summary>
		///     a timer that will periodically check if connections are still active
		/// </summary>
		private static readonly Timer connectionCheckTimer = new Timer(state => {

			try {
				foreach(KeyValuePair<Guid, TcpConnection<READING_CONTEXT>> connState in connectionStates.ToArray()) {
					if(connState.Value.IsDisposed) {
						connectionStates.RemoveSafe(connState.Key);
					} else if(connState.Value.State == ConnectionState.Connected) {
						connState.Value.CheckConnected();
					}
				}
			} catch(Exception ex) {
				//TODO: do something?
				NLog.Connections.Error(ex, "Timer exception");
			}

		}, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10));

		private static readonly ConcurrentDictionary<Guid, TcpConnection<READING_CONTEXT>> connectionStates = new ConcurrentDictionary<Guid, TcpConnection<READING_CONTEXT>>();

		/// <summary>
		///     Reset event that is triggered when the connection is marked Connected.
		/// </summary>
		private readonly ManualResetEvent connectWaitLock = new ManualResetEvent(false);

		protected readonly AsyncLock disposeLocker = new AsyncLock();

		protected readonly TcpConnection.ExceptionOccured exceptionCallback;

		protected readonly SafeArrayHandle handshakeBytes = SafeArrayHandle.Create();

		protected readonly bool isServer;

		protected readonly ProtocolFactory protocolFactory = new ProtocolFactory();

		private readonly ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters;
		private readonly AdaptiveInteger1_4 receiveByteShrinker = new AdaptiveInteger1_4();

		private readonly AsyncManualResetEventSlim resetEvent = new AsyncManualResetEventSlim(false);

		private readonly AdaptiveInteger1_4 sendByteShrinker = new AdaptiveInteger1_4();
		protected readonly AsyncLock sendBytesLocker = new AsyncLock();

		/// <summary>
		///     The socket we're managing.
		/// </summary>
		protected Socket socket;

		/// <summary>
		///     if true, any exception will be alerted. whjen we know what we are doing and we want to shutup any further noise, we
		///     set this to false.
		/// </summary>
		private bool alertExceptions = true;

		protected Task dataReceptionTask;

		/// <summary>
		///     Did we send the handshake payload yet?
		/// </summary>
		protected HandshakeStatuses handshakeStatus = HandshakeStatuses.NotStarted;

		protected ProtocolCompression peerProtocolCompression;

		protected ProtocolVersion peerProtocolVersion;
		protected int receiveBufferSize;

		private volatile ConnectionState state;

		protected CancellationTokenSource tokenSource;

		public TcpConnection(TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) {
			this.isServer = isServer;

			this.State = ConnectionState.NotConnected;
			this.exceptionCallback = exceptionCallback;

			// the ability to filter which types of message this socket will allow
			if(protocolMessageFilters == null) {
				this.protocolMessageFilters = TcpConnection.ProtocolMessageTypes.All;
			} else {
				this.protocolMessageFilters = protocolMessageFilters;
			}

			this.tokenSource = new CancellationTokenSource();

			this.Initialize();
		}

		/// <summary>
		///     Creates a TcpConnection from a given TCP Socket. usually called by the TcpServer
		/// </summary>
		/// <param name="socket">The TCP socket to wrap.</param>
		public TcpConnection(Socket socket, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : this(exceptionCallback, isServer, protocolMessageFilters) {
			//Check it's a TCP socket
			if(socket.ProtocolType != ProtocolType.Tcp) {
				throw new ArgumentException("A TcpConnection requires a TCP socket.");
			}

			this.isServer = isServer;

			this.EndPoint = new NetworkEndPoint(socket.RemoteEndPoint);
			this.RemoteEndPoint = socket.RemoteEndPoint;

			this.socket = socket;
			this.socket.NoDelay = true;

			this.SocketNewlyConnected();

			this.State = ConnectionState.Connected;

			this.Initialize();
		}

		public TcpConnection(NetworkEndPoint remoteEndPoint, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : this(exceptionCallback, isServer, protocolMessageFilters) {

			if(this.State != ConnectionState.NotConnected) {
				throw new InvalidOperationException("Cannot connect as the Connection is already connected.");
			}

			this.isServer = isServer;

			this.EndPoint = remoteEndPoint;
			this.RemoteEndPoint = remoteEndPoint.EndPoint;
			this.IPMode = remoteEndPoint.IPMode;

			this.Initialize();
		}

		private void Initialize() {
			
			AddConnectionState(this);
			
			this.LatencySets.TryAdd(TcpConnection.LatencyTypes.Connection, new TcpConnection.LatencySet());
			this.LatencySets.TryAdd(TcpConnection.LatencyTypes.BlockSync, new TcpConnection.LatencySet());
		}
		
		/// <summary>
		///     a timestamp to know when we should check the connection for keepalive
		/// </summary>
		private DateTime NextConnectedAliveCheck { get; set; } = DateTimeEx.MinValue;

		protected Guid ConnectionId { get; } = Guid.NewGuid();
		

		private bool IsDisposing { get; set; }

		public double ConnectionLatency => this.Latency(TcpConnection.LatencyTypes.Connection);
		public double ConnectionLatencyStd => this.LatencyStd(TcpConnection.LatencyTypes.Connection);
		
		public double BlockSyncLatency => this.Latency(TcpConnection.LatencyTypes.BlockSync);
		public double BlockSyncLatencyStd => this.LatencyStd(TcpConnection.LatencyTypes.BlockSync);
		
		public double Latency(TcpConnection.LatencyTypes latencyType) {
			return this.LatencySets[latencyType].Latency;
		}
		public double LatencyStd(TcpConnection.LatencyTypes latencyType) {
			return this.LatencySets[latencyType].LatencyStd;
		}
		
		public ConcurrentDictionary<TcpConnection.LatencyTypes, TcpConnection.LatencySet> LatencySets { get; } = new ConcurrentDictionary<TcpConnection.LatencyTypes, TcpConnection.LatencySet>();
		
		public void AddBlockSyncLatency(DateTime start) {
            AddLatencyEntry(start, LatencySets[TcpConnection.LatencyTypes.BlockSync]);
		}
		
		public void AddLatency(DateTime start) {

            AddLatencyEntry(start, LatencySets[TcpConnection.LatencyTypes.Connection]);
		}

		private static void AddLatencyEntry(DateTime start, TcpConnection.LatencySet latencySet) {
			if(start == DateTimeEx.MinValue || start == DateTime.MinValue) {
				return;
			}
			double value = (DateTimeEx.CurrentTime - start).TotalSeconds;

			// only calculate latency if we have enough data points to make it meaningful
			if (latencySet.latencies.Count >= 3)
			{
				var zScore = MathUtils.ZScore(latencySet.latencies.ToArray(), value, out var mean, out var std);
				
				if(zScore < 7)
					latencySet.latencies.Enqueue(value);

				latencySet.latency = mean;
				latencySet.latencyStd = std;
			}
			else
			{
				latencySet.latencies.Enqueue(value);
				latencySet.latency = latencySet.latencies.Min();
			}
		}

		// A UUiD we set and use internally
		public Guid InternalUuid { get; } = Guid.NewGuid();

		public Guid ReportedUuid { get; private set; }

		public void SendSocketBytes(SafeArrayHandle bytes, bool sendSize = true) {
			//Write the bytes to the socket

			this.SendSocketBytes(bytes.Span, sendSize);
			
			bytes.Dispose();
		}

		public void SendSocketBytes(in Span<byte> bytes, bool sendSize = true) {
			//Write the bytes to the socket

			try {
				if(this.State != ConnectionState.Connected) {
					throw new SocketException((int) SocketError.Shutdown);
				}

				using(this.sendBytesLocker.Lock()) {
					if(sendSize) {
						// write the size of the message first
						this.sendByteShrinker.Value = (uint) bytes.Length;
						this.WritePart((Span<byte>) this.sendByteShrinker.GetShrunkBytes());
					}

					// now the message
					this.Write(bytes);
				}
			} catch(Exception e) {
				var he = new P2pException("Could not send data as an error occured.", P2pException.Direction.Send, P2pException.Severity.Casual, e);
				this.Close();

				throw he;
			}
		}

		public EndPoint RemoteEndPoint { get; }

		public IPMode IPMode { get; }

		public NetworkEndPoint EndPoint { get; }

		public bool CheckConnected(bool force = false) {

			if((this.NextConnectedAliveCheck < DateTime.Now) || force) {
				this.NextConnectedAliveCheck = DateTime.Now + TimeSpan.FromSeconds(20);

				if(this.State == ConnectionState.NotConnected) {
					return false;
				}

				bool CheckConnected() {
					using(this.disposeLocker.Lock()) {
						if(this.IsDisposed || this.IsDisposing) {
							return false;
						}

						// TimeSpan latency = new TimeSpan();
						bool result = false;

						if(this.socket != null) {
							result = this.socket.IsReallyConnected(out var latency);
						}
						// if (result)
							// this.Latency = latency.TotalSeconds;
						return result;

					}
				}

				bool connected = CheckConnected();

				if(!connected) {
					// yes, we try twice, just in case...
					Thread.Sleep(300);
					connected = CheckConnected();

					if(!connected) {
						// ok, we give up, connection is disconnected
						this.State = ConnectionState.NotConnected;

						// seems we are not connected after all
						NLog.Connections.Verbose("Socket was disconnected ungracefully from the other side. Disconnecting.");
						this.Close();
						return false;
					}
				}

			}

			return true;
		}

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

					if(this.state == ConnectionState.Connected) {
						this.connectWaitLock.Set();
					} else {
						this.connectWaitLock.Reset();
					}
				}
			}
		}

		public bool IsDisposed { get; private set; }

		public event TcpConnection.MessageBytesReceived DataReceived;

		public event TcpConnection.MessageBytesSent DataSent;
		public event EventHandler<DisconnectedEventArgs> Disconnected;
		public event Action Connected;
		public event Action Disposing;
		public event Action<Guid> ConnectedUuidProvided;

		public bool IsConnectedUuidProvidedSet => this.ConnectedUuidProvided != null;

		public void Close() {
			this.Dispose();
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public Task Connect(SafeArrayHandle bytes) {
			return this.Connect(bytes, TcpConnection.SocketParameterTypes.Sturdy);
		}

		public async Task Connect(SafeArrayHandle bytes, TcpConnection.SocketParameterTypes socketParameterType, int socketBuffer = 0) {
			if(this.IsDisposed || this.IsDisposing) {
				throw new SocketException((int) SocketError.Shutdown);
			}

			if((bytes == null) || bytes.IsEmpty) {
				throw new TcpApplicationException("Handshake bytes can not be null");
			}

			//Connect
			this.State = ConnectionState.Connecting;

			try {
				bool success = false;
				(this.socket, success) = await SocketExtensions.ConnectSocket(this.EndPoint, s => {

					if(socketParameterType == TcpConnection.SocketParameterTypes.Sturdy) {
						s.InitializeSocketParameters();
					}
					else if(socketParameterType == TcpConnection.SocketParameterTypes.Fast) {
						s.InitializeSocketParametersFast(socketBuffer);
					}
				}, 5).ConfigureAwait(false);
				
				if(!success) {
					throw new SocketException((int) SocketError.TimedOut);
				}

				if(this.socket.IsReallyConnected()) {

					if(this.Connected != null) {
						this.Connected();
					}

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

			//Start receiving data
			this.StartReceivingData();

			this.handshakeBytes.Entry = bytes.Entry;
			this.handshakeStatus = HandshakeStatuses.VersionSentNoBytes;

			var startHandshake = DateTimeEx.CurrentTime;
			//Send handshake
			this.SendHandshakeVersion();

			// now wait for the handshake to complete
			if(await this.resetEvent.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false)) {
				this.resetEvent.Reset();
			}

			this.AddLatency(startHandshake);
			
			if(this.handshakeStatus != HandshakeStatuses.Completed) {

				this.Dispose();

				throw new TcpApplicationException("Timedout waiting for a proper connection handshake.");
			}

			

		}

		/// <summary>
		///     Here we try a quick counterconnect to establish if their connection port is open and available
		/// </summary>
		/// <param name="port"></param>
		/// <returns></returns>
		/// <exception cref="P2pException"></exception>
		public Task<bool> PerformCounterConnection(int port) {
			return TcpConnection.PerformCounterConnection(((IPEndPoint) this.RemoteEndPoint).Address, port);
		}

		public bool SendMessage(long hash) {
			MessageInstance message = null;

			message = this.protocolFactory.WrapMessage(hash);

			if(message == null) {
				return false;
			}

			this.SendMessage(message);

			return true;
		}

		public void SendBytes(SafeArrayHandle bytes) {

			if((bytes == null) || bytes.IsEmpty) {
				throw new TcpApplicationException("The message bytes can not be null");
			}

			MessageInstance messageInstance = null;

			messageInstance = this.protocolFactory.WrapMessage(bytes, this.protocolMessageFilters);

			this.SendMessage(messageInstance);

			this.InvokeDataSent(bytes);
		}

		public void StartWaitingForHandshake(TcpConnection.MessageBytesReceived handshakeCallback) {
			
			this.StartReceivingData(bytes => {
				//Invoke
				handshakeCallback(bytes);
				
				return Task.CompletedTask;
			});
		}

		private static void AddConnectionState(TcpConnection<READING_CONTEXT> connection) {
			if(!connectionStates.ContainsKey(connection.ConnectionId)) {
				connectionStates.AddSafe(connection.ConnectionId, connection);
			}
		}

		protected virtual void SocketNewlyConnected() {

		}

		protected void SocketClosed() {

		}

		protected virtual void StartReceivingData(TcpConnection.MessageBytesReceived handshakeCallback = null) {

			if(this.IsDisposed) {
				throw new TcpApplicationException("Can not reuse a disposed tcp connection");
			}

			if(this.State != ConnectionState.Connected) {
				throw new TcpApplicationException("Socket is not connected");
			}

			this.dataReceptionTask = this.StartReceptionStream(this.tokenSource.Token, handshakeCallback).WithAllExceptions().ContinueWith(task => {
				if(task.IsFaulted) {
					// an exception occured. but alert only if we should, otherwise let the connection die silently.
					NLog.Connections.Verbose($"[{nameof(TcpConnection)}] Error in {nameof(this.StartReceptionStream)}, task exception: '{task.Exception}'");
					if(this.alertExceptions) {
						var exception = new P2pException("A serious exception occured while receiving data from the socket.", P2pException.Direction.Receive, P2pException.Severity.VerySerious, task.Exception);

						try {
							this.Close();
						} finally
						{
							// inform the users we had a serious exception. We only invoke this when we receive data, since we want to capture evil peers. we trust ourselves, so we dont act on our own sending errors.
							if(this.exceptionCallback != null) {
								this.exceptionCallback(exception, this);
							}
						}
					}
				}

				this.resetEvent.Set();
			},default, TaskContinuationOptions.None, TaskScheduler.Current);
		}

		private async Task<int> StartReceptionStream(CancellationToken ct, TcpConnection.MessageBytesReceived handshakeCallback = null) {

			Exception? e = null;
			try {

				await this.ReadHandshake(ct).ConfigureAwait(false);

				if(this.handshakeStatus == HandshakeStatuses.NotStarted) {
					throw new TcpApplicationException("Handshake protocol has failed");
				}

				if(this.handshakeStatus == HandshakeStatuses.Completed) {
					this.resetEvent.Set();
				}

				
				await this.ReadMessage(bytes => {

					if(handshakeCallback != null) {

						this.TriggerHandshakeCallback(ref handshakeCallback, bytes);

						this.handshakeStatus = HandshakeStatuses.Completed;

						this.resetEvent.Set();
					} else {
						// data received
						return this.InvokeDataReceived(bytes);
					}
					
					return Task.CompletedTask;
				}, ct).ContinueWith(task => {
					if(task.IsFaulted) {
						//an exception occured
						NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.StartReceptionStream)}]: task is faulted : {task.Exception}");
						throw new P2pException("An exception occured while processing a message response.", P2pException.Direction.Receive, P2pException.Severity.VerySerious, task.Exception);
					}
					//NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.StartReceptionStream)}]: task is not faulted");
				}, ct, TaskContinuationOptions.None, TaskScheduler.Current).ConfigureAwait(false);

			} catch(InvalidPeerException ipex)
			{
				e = ipex;
				// ok, we got an invalid peer. we dont need to do anything, lost let it go and disconnect
				this.alertExceptions = false;
			} catch(CounterConnectionException cex)
			{
				e = cex;
				this.alertExceptions = false;
			} catch(OperationCanceledException opex)
			{
				e = opex;
				this.alertExceptions = false;
			} catch(ObjectDisposedException ode)
			{
				e = ode;
				// do nothing
				this.alertExceptions = false;
			} catch(Exception ex)
			{
				e = ex;
				NLog.Connections.Verbose(ex, "Error occured on the connection");
			} finally {
				// disconnected
				// NLog.Connections.Verbose($"[{nameof(TcpConnection)}] {this.InternalUuid} {this.EndPoint} Disconnecting, exception: '{e?.Message??"None"}', stack trace {Environment.StackTrace}");
				this.Close();
			}

			return 1;
		}

		protected virtual void TriggerHandshakeCallback(ref TcpConnection.MessageBytesReceived handshakeCallback, SafeArrayHandle bytes) {

			if(this.handshakeStatus != HandshakeStatuses.VersionReceivedNoBytes) {
				throw new TcpApplicationException("Handshake bytes received in the wrong order");
			}

			handshakeCallback(bytes);
			handshakeCallback = null; // we never call it again
		}

		/// <summary>
		///     Write and flush a message
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		protected bool Write(in ReadOnlySpan<byte> message) {

			this.WritePart(message);

			return this.CompleteWrite().GetAwaiter().GetResult();
		}

		/// <summary>
		///     Write to the socket, but dont sent it. make sure you call CompleteWrite()
		/// </summary>
		/// <param name="message"></param>
		protected abstract void WritePart(in ReadOnlySpan<byte> message);

		/// <summary>
		///     Send anything in the buffer
		/// </summary>
		/// <returns></returns>
		protected abstract Task<bool> CompleteWrite();

		private async Task ReadHandshake(CancellationToken cancellationNeuralium = default) {

			var dataRead = false;
			READING_CONTEXT read = default;

			DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(30);

			do {
				if(timeout < DateTime.Now) {
					throw new ApplicationException("Timeout out getting handshake");
				}

				read = await this.ReadDataFrame(default, cancellationNeuralium).ConfigureAwait(false);

				if(read.IsCanceled || read.IsCompleted) {
					this.ReadTaskCancelled();

					throw new TaskCanceledException();
				}

				dataRead = !read.IsEmpty;

				if(!dataRead) {
					read.DataRead(0);
				}

			} while(dataRead == false);

			// can we find a complete frame?
			if(!read.IsEmpty && (read.Length == ProtocolFactory.HANDSHAKE_COUNTERCONNECT_BYTES.Length)) {

				var counterBytes = new byte[read.Length];
				read.CopyTo(counterBytes, 0, 0, counterBytes.Length);

				if(counterBytes.SequenceEqual(ProtocolFactory.HANDSHAKE_COUNTERCONNECT_BYTES)) {
					// ok, we just received a port confirmation counterconnect. we do nothing further with this connection
					this.Dispose();

					throw new CounterConnectionException();
				}
			}

			if(!read.IsEmpty && (read.Length == ProtocolFactory.HANDSHAKE_PROTOCOL_SIZE)) {
				// ok, we should have our handshake request
				(ProtocolVersion version, ProtocolCompression compression, Guid uuid) = this.ParseVersion(read);
				this.peerProtocolVersion = version;
				this.peerProtocolCompression = compression;

				if(uuid == Guid.Empty) {
					throw new TcpApplicationException("Peer uuid cannot be empty");
				}

				this.ReportedUuid = uuid;

				// lets alert that we have a peer uuid, see if we accept it
				try {
					if(this.ConnectedUuidProvided != null) {
						this.ConnectedUuidProvided(uuid);
					}
				} catch {
					// we can't go further, stop.
					this.handshakeStatus = HandshakeStatuses.Unusable;

					throw;
				}

				this.protocolFactory.SetPeerProtocolVersion(this.peerProtocolVersion);
				this.protocolFactory.SetPeerProtocolCompression(this.peerProtocolCompression);

				read.DataRead((int) read.Length);

				if(this.handshakeStatus == HandshakeStatuses.NotStarted) {
					// we are a server and are awaiting for the handshake bytes now
					// now reply and send our own version

					//TODO: as the server, we have the right to insist on another compression mode instead of taking the client's. lets keep this in mind.
					this.SendHandshakeVersion();

					this.handshakeStatus = HandshakeStatuses.VersionReceivedNoBytes;
				} else if(this.handshakeStatus == HandshakeStatuses.VersionSentNoBytes) {
					// ok, we are the client, now is the time to send the handshake bytes
					this.SendHandshakeBytes();

					this.handshakeStatus = HandshakeStatuses.Completed;
				}
			} else {
				throw new TcpApplicationException("invalid protocol handshake format");
			}
		}

		protected virtual (ProtocolVersion version, ProtocolCompression compression, Guid uuid) ParseVersion(READING_CONTEXT read) {

			Span<byte> header = stackalloc byte[ProtocolFactory.HANDSHAKE_PROTOCOL_SIZE];
			read.CopyTo(header, 0, 0, header.Length);

			return ProtocolFactory.ParseVersion(header);
		}

		protected abstract Task<READING_CONTEXT> ReadDataFrame(READING_CONTEXT previous, CancellationToken ct);

		protected virtual void ReadTaskCancelled() {

		}

		protected async Task ReadMessage(TcpConnection.MessageBytesReceived callback, CancellationToken cancellationNeuralium = default) {
			SafeArrayHandle mainBuffer = null;

			var bytesCopied = 0;
			var sizeByteSize = 0;

			var tryAttempt = 0;

			READING_CONTEXT read = default;
			try
			{
				while(true) {
					if(cancellationNeuralium.IsCancellationRequested) {
						this.ReadTaskCancelled();
						//NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) Cancellation Requested (1)");
						throw new TaskCanceledException();
					}

					read = await this.ReadDataFrame(read, cancellationNeuralium).ConfigureAwait(false);

					if(read.IsCanceled) {
						this.ReadTaskCancelled();
						//NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) Cancellation Requested (2)");
						throw new TaskCanceledException();
					}

					// can we find a complete frame?
					if(!read.IsEmpty) {

						// first thing, extract the message size

						var segmentOffset = 0;

						// first we always read the message size
						var messageSize = 0;

						if(sizeByteSize == 0) {
							try {
								bytesCopied = 0;
								tryAttempt = 0;
								sizeByteSize = this.receiveByteShrinker.ReadBytes(read);
								messageSize = (int) this.receiveByteShrinker.Value;

								// yup, we will need this so lets not make it clearable
								mainBuffer = SafeArrayHandle.Create(messageSize);

								read.DataRead(sizeByteSize);

								continue;

							} catch(Exception) {
								sizeByteSize = 0;
								mainBuffer?.Dispose();
								mainBuffer = null;
							}
						} else if(mainBuffer != null) {

							int usefulBufferLength = (int) read.Length - segmentOffset;

							// accumulate data		
							if(usefulBufferLength != 0) {

								tryAttempt = 0;

								if(mainBuffer.Length < usefulBufferLength) {
									usefulBufferLength = mainBuffer.Length;
								}

								int remainingLength = usefulBufferLength + bytesCopied;

								if(remainingLength > mainBuffer.Length) {
									usefulBufferLength = mainBuffer.Length - bytesCopied;
								}

								//lets check if we received more data than we expected. if we did, this is critical, means everything is offsetted. this is serious and we break.
								if(bytesCopied > mainBuffer.Length) {
									NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) (throwing) The amount of data received is greater than expected. fatal error.");
									throw new TcpApplicationException("The amount of data received is greater than expected. fatal error.");
								}

								read.CopyTo(mainBuffer.Span, segmentOffset, bytesCopied, usefulBufferLength);

								bytesCopied += usefulBufferLength;

								// ok we are done with this
								read.DataRead(usefulBufferLength);

								if(bytesCopied == mainBuffer.Length) {

									IMessageEntry messageEntry = null;

									//we expect to read the header to start. if the header is corrupted, this will break and thats it.
									messageEntry = this.protocolFactory.CreateMessageParser(mainBuffer.Branch()).RehydrateHeader(this.protocolMessageFilters);

									// use the entry
									IMessageEntry entry = messageEntry;
									sizeByteSize = 0;
									messageSize = 0;

									// free the message entry for another message
									SafeArrayHandle releasedMainBuffer = mainBuffer;

									mainBuffer = null;

									if(cancellationNeuralium.IsCancellationRequested) {
										NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) Cancellation Requested (3)");
										this.ReadTaskCancelled();

										throw new TaskCanceledException();
									}

									//lets handle the completed message. we can launch it in its own thread since message pumping can continue meanwhile independently

									await Task.Run(async () => {

										SafeArrayHandle localMainBuffer = releasedMainBuffer;

										using(localMainBuffer) {
											using(IDataRehydrator bufferRehydrator = DataSerializationFactory.CreateRehydrator(localMainBuffer)) {

												// skip the header offset
												bufferRehydrator.Forward(messageEntry.Header.MessageOffset);

												entry.SetMessageContent(bufferRehydrator);

												await this.protocolFactory.HandleCompetedMessage(entry, callback, this).ConfigureAwait(false);
											}
										}

										return true;
									}, cancellationNeuralium).WithAllExceptions().ContinueWith(task => {
										if(task.IsFaulted) {
											//an exception occured
											NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) task is faulted : {task.Exception}");
											throw new P2pException("An exception occured while processing a message response.", P2pException.Direction.Receive, P2pException.Severity.VerySerious, task.Exception);
										}
									}, cancellationNeuralium, TaskContinuationOptions.None, TaskScheduler.Current).ConfigureAwait(false);

								}
							} else {
								tryAttempt++;

								if(tryAttempt == 5) {
									NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) Spent all 5 attempts");
									throw new TcpApplicationException("Our sender just hanged. we received no new data that we expected.");
								}
							}
						}
					}

					if(read.IsCompleted) {
						NLog.Connections.Verbose($"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) read is completed");
						break;
					}
				}
			}
			catch (OperationCanceledException e)
			{
				throw;
			}
			catch (Exception e)
			{
				NLog.Connections.Verbose(e, $"[{nameof(TcpConnection)}{nameof(this.ReadMessage)}: ({this.EndPoint}) Caught Exception");
				throw;
			}
		}

		private void SendHandshakeVersion() {
			if(this.IsDisposed) {
				throw new TcpApplicationException("Can not use a disposed tcp connection");
			}

			SafeArrayHandle wrappedBytes = this.CreateHandshakeBytes();

			this.SendSocketBytes(wrappedBytes, false);
		}

		protected virtual SafeArrayHandle CreateHandshakeBytes() {
			return this.protocolFactory.CreateHandshake();
		}

		protected void SendMessage(MessageInstance messageInstance) {

			if(this.IsDisposed) {
				throw new TcpApplicationException("Can not use a disposed tcp connection");
			}

			SafeArrayHandle sendBytes = null;

			if(messageInstance.IsSplitMessage) {
				if(!MessageCaches.SendCaches.Exists(messageInstance.Hash)) {
					// ensure it is cached
					MessageCaches.SendCaches.AddEntry(messageInstance.SplitMessage);
				}

				if(this.protocolMessageFilters.MissesOption(TcpConnection.ProtocolMessageTypes.Split)) {
					throw new TcpApplicationException("Split messages are not allowed on this socket");
				}

				// here we send the server the header of our big message so they can start the sliced download
				sendBytes = messageInstance.SplitMessage.Dehydrate();
			} else {
				sendBytes = messageInstance.MessageBytes.Branch();
			}

			this.SendSocketBytes(sendBytes);

			if(!messageInstance.IsCached) {
				messageInstance.Dispose();
			}
		}

		protected virtual void SendHandshakeBytes() {

			if((this.handshakeBytes == null) || this.handshakeBytes.IsEmpty) {
				throw new TcpApplicationException("The handshake bytes can not be null");
			}

			this.SendBytes(this.handshakeBytes);
			this.handshakeBytes?.Dispose();
		}

		protected Task InvokeDataReceived(SafeArrayHandle bytes) {
			if(this.DataReceived != null) {
				return this.DataReceived(bytes);
			}
			return Task.CompletedTask;
		}

		protected void InvokeDataSent(SafeArrayHandle bytes) {
			if(this.DataSent != null) {
				this.DataSent(bytes);
			}
		}

		protected void InvokeDisconnected() {
			DisconnectedEventArgs args = DisconnectedEventArgs.GetObject();

			//Make a copy to avoid race condition between null check and invocation

			if(this.Disconnected != null) {
				this.Disconnected(this, args);
			}
		}

		protected bool WaitOnConnect(int timeout) {
			return this.connectWaitLock.WaitOne(timeout);
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

			if(connectionStates.ContainsKey(this.ConnectionId)) {
				connectionStates.RemoveSafe(this.ConnectionId);
			}

			try {

				if(!this.IsDisposed || disposingChanged) {

					this.DisposeAll();

					if(this.Disposing != null) {
						this.Disposing();
					}
				}
			} finally {
				this.IsDisposed = true;
				this.IsDisposing = false;
			}

		}

		~TcpConnection() {
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

			try {
				this.SocketClosed();
			} finally {

				this.InvokeDisconnected();
			}
		}

		protected virtual void DisposeSocket() {
			try {
				if(this.socket?.Connected ?? false) {
					try {
						this.socket?.Shutdown(SocketShutdown.Both);
					} catch {
						// do nothing, we tried
					}
					
					try {
						Thread.Sleep(200);
						this.socket?.Disconnect(false);
					} catch {
						// do nothing, we tried
					}
				}
			} finally {
				try {
					this.socket?.Dispose();
				} catch {
					// do nothing, we tried
				}
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