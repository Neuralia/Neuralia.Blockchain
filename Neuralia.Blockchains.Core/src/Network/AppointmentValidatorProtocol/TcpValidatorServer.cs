﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Data.Pools;
using Neuralia.Blockchains.Tools.General;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {

	public interface ITcpValidatorServer {

		int RequesterCount { get; }

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
		bool BlockchainDelegateEmpty { get; }

		void Close();

		/// <summary>
		///     Call to dispose of the connection listener.
		/// </summary>
		void Dispose();

		void Start();

		void Stop();

		void Initialize();

		void RegisterBlockchainDelegate(BlockchainType blockchainType, IAppointmentValidatorDelegate appointmentValidatorDelegate, Func<bool> isInAppointmentWindow);
		void UnregisterBlockchainDelegate(BlockchainType blockchainType);
	}

	/// <summary>
	///     Listens for new TCP connections and creates TCPConnections for them.
	/// </summary>
	/// <inheritdoc />
	public class TcpValidatorServer : ITcpValidatorServer {

		public delegate Task MessageBytesReceived(TcpServer listener, ITcpConnection connection, SafeArrayHandle buffer);

		public const byte PING_BYTE = 255;
		public const byte PONG_BYTE = 255;
		public const byte BYTES_PER_REQUESTER = 100;

		private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates = new();
		private readonly ConcurrentDictionary<BlockchainType, Func<bool>> appointmentWindowChecks = new();
		private readonly Semaphore maximumAcceptedRequesters;

		private readonly NetworkEndPoint networkEndPoint;
		private ObjectPool<SocketAsyncEventArgs> asyncEventArgsPool;
		private ByteArray buffer;
		private int connectedRequesters;

		/// <summary>
		///     The socket listening for connections.
		/// </summary>
		private Socket listener;

		private bool enableMarshall;
		public TcpValidatorServer(int requesterCount, NetworkEndPoint endPoint, bool enableMarshall = true) {
			this.RequesterCount = requesterCount;
			this.EndPoint = endPoint.EndPoint;
			this.IPMode = endPoint.IPMode;
			this.networkEndPoint = endPoint;
			this.enableMarshall = enableMarshall;
			
			this.maximumAcceptedRequesters = new Semaphore(requesterCount, requesterCount);

			ValidatorProtocolFactory.InitializeValidatorProtocolPool(requesterCount);
		}

		public int RequesterCount { get; }

		public bool IsRunning { get; private set; }

		/// <summary>
		///     The local end point the listener is listening for new clients on.
		/// </summary>
		public EndPoint EndPoint { get; }

		/// <summary>
		///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
		/// </summary>
		public IPMode IPMode { get; }

		public bool IsDisposed { get; private set; }

		public void Initialize() {

			this.buffer?.Dispose();
			this.asyncEventArgsPool?.Dispose();

			this.buffer = ByteArray.Create(BYTES_PER_REQUESTER * this.RequesterCount);

			ClosureWrapper<int> index = 0;

			this.asyncEventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() => {

				SocketAsyncEventArgs entry = new();
				entry.Completed += this.ProcessCompleted;
				var token = new ValidatorConnectionInstance {listener = this.listener, server = this, BufferOffset = this.buffer.Offset + (BYTES_PER_REQUESTER * index.Value)};
				entry.UserToken = token;

				entry.SetBuffer(this.buffer.Bytes, token.BufferOffset, 1);

				index.Value++;

				return entry;
			}, this.RequesterCount, 0);
		}

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
				this.listener.InitializeSocketParametersFast(BYTES_PER_REQUESTER);

				this.listener.Bind(this.EndPoint);
				this.listener.Listen((int) SocketOptionName.MaxConnections);

				this.IsRunning = true;
				this.BeginAccepting(null);

				NLog.Default.Information("Validator TCP Server started");

				NLog.LoggingBatcher.Start();
				
			} catch(SocketException e) {
				throw new P2pException("Could not start listening as a SocketException occured", P2pException.Direction.Receive, P2pException.Severity.Casual, e);
			}
		}

		public void Stop() {
			if(this.IsRunning) {
				this.IsRunning = false;

				try {
					try {
						if(this.listener?.Connected ?? false) {
							this.listener.Shutdown(SocketShutdown.Both);
						}
					} catch {

					}

					try {
						this.listener?.Dispose();
					} catch {

					}
					
					try {
						this.buffer?.Dispose();
					} catch {

					}
					
					this.buffer = null;

					try {
						this.asyncEventArgsPool.Dispose();
					} catch {

					}
					
					this.asyncEventArgsPool = null;

					NLog.Default.Information("Validator TCP Server stopped");
				} finally {
					this.IsRunning = false;
					this.listener = null;
					
					NLog.LoggingBatcher.Stop();
				}
			}
		}

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
				this.appointmentValidatorDelegates.Remove(blockchainType, out IAppointmentValidatorDelegate _);
			}

			if(this.appointmentWindowChecks.ContainsKey(blockchainType)) {
				this.appointmentWindowChecks.Remove(blockchainType, out Func<bool> _);
			}
		}

		public bool BlockchainDelegateEmpty => !this.appointmentValidatorDelegates.Any();

		public void BeginAccepting(SocketAsyncEventArgs acceptEventArg) {

			if(!this.IsRunning) {
				return;
			}

			// this is the one passed around as a chain
			if(acceptEventArg == null) {
				acceptEventArg = new SocketAsyncEventArgs();
				acceptEventArg.Completed += this.AcceptCompleted;
			} else {
				acceptEventArg.AcceptSocket = null;
			}

			this.maximumAcceptedRequesters.WaitOne();
			bool willRaiseEvent = this.listener.AcceptAsync(acceptEventArg);

			if(!willRaiseEvent) {
				this.ProcessAccept(ref acceptEventArg);
			}
		}

		private void AcceptCompleted(object sender, SocketAsyncEventArgs e) {
			this.ProcessAccept(ref e);
		}

		private void ProcessAccept(ref SocketAsyncEventArgs e) {

			if(e.AcceptSocket != null) {
				var acceptSocket = e.AcceptSocket;
				var endPoint = (IPEndPoint)e.AcceptSocket.RemoteEndPoint;
				var task = Task.Run(() => {

					SocketAsyncEventArgs readEventArgs = null;
					NLog.LoggingBatcher.IncrementInstances();

					try {
						// first thing, ask IPMarshall
						if(this.CheckShouldDisconnect(endPoint)) {
							try {
								acceptSocket.Dispose();

							} catch(ObjectDisposedException) {
								//If the socket's been disposed then we can just end there.
							}
							NLog.LoggingBatcher.DecrementInstances();

							return;
						}

						Interlocked.Increment(ref this.connectedRequesters);

						readEventArgs = this.asyncEventArgsPool.GetObject();

						if(readEventArgs.UserToken is ValidatorConnectionInstance token) {
							token.listener = acceptSocket;
						}

						if(!acceptSocket.ReceiveAsync(readEventArgs)) {
							this.ProcessCompleted(this, ref readEventArgs);
						}
					} catch {
						if(readEventArgs != null) {

							ValidatorConnectionInstance token = (ValidatorConnectionInstance) readEventArgs.UserToken;
							this.Quarantine(token);
						}

						this.DisconnectClient(ref readEventArgs);
					}
				});
			}

			// Accept the next connection request
			this.BeginAccepting(e);
		}

		private void ProcessCompleted(object sender, SocketAsyncEventArgs e) {
			ProcessCompleted(sender, ref e);
		}

		private void ProcessCompleted(object sender, ref SocketAsyncEventArgs e) {
			ValidatorConnectionInstance token = (ValidatorConnectionInstance) e.UserToken;

			if((e.LastOperation == SocketAsyncOperation.Accept) && (token != null ? token.Step != ValidatorConnectionInstance.Steps.Closing : true)) {
				this.ProcessAccept(ref e);
			} else if(token.Step == ValidatorConnectionInstance.Steps.ReceiveFrontByte) {
				this.ProcessReceiveFrontByte(token, ref e);
			} else if(token.Step == ValidatorConnectionInstance.Steps.ReceiveHeader) {
				this.ProcessReceiveHeader(token, ref e);
			} else if(token.Step == ValidatorConnectionInstance.Steps.ReceiveOperation) {
				this.ProcessReceiveOperation(token, ref e);
			} else if(token.Step == ValidatorConnectionInstance.Steps.Closing) {
				// do nothing
			} else {
				this.DisconnectClient(ref e);
			}
		}

		private void ProcessReceiveOperation(ValidatorConnectionInstance token, ref SocketAsyncEventArgs e) {
			if((e.BytesTransferred != 0) && (e.SocketError == SocketError.Success)) {

				// first step, take the header
				using ByteArray operationBytes = ByteArray.Wrap(e.Buffer, e.Offset, e.BytesTransferred, false);
				
				IValidatorProtocol protocol = ValidatorProtocolFactory.GetValidatorProtocolInstance(token.Header.ProtocolVersion, token.Header.ChainId, type => {

					IAppointmentValidatorDelegate validatorDelegate = null;
					this.appointmentValidatorDelegates.TryGetValue(type, out validatorDelegate);
					
					return validatorDelegate;
				});

				if(protocol == null) {
					return;
				}

				using CancellationTokenSource tokenSource = new();

				if(!protocol.HandleServerExchange(new ValidatorConnectionSet {Token = token, Socket = token.listener, SocketAsyncEventArgs = e}, operationBytes, tokenSource.Token).WaitAndUnwrapException(tokenSource.Token)) {
					this.DisconnectClient(ref e);
				} else {
					token.Step = ValidatorConnectionInstance.Steps.Finished;
				}
			} else {
				this.DisconnectClient(ref e);

				this.Quarantine(token);
			}
		}

		private void ProcessReceiveHeader(ValidatorConnectionInstance token, ref SocketAsyncEventArgs e) {
			if((e.BytesTransferred > 0) && (e.SocketError == SocketError.Success)) {

				// first step, take the header
				using ByteArray headerBytes = ByteArray.Wrap(e.Buffer, e.Offset, ValidatorProtocolHeader.MAIN_HEADER_SIZE, false);

				ValidatorProtocolHeader header = new();
				header.Rehydrate(ValidatorProtocolHeader.HEAD_BYTE, headerBytes);

				// first thing, validate header
				if(header.NetworkId != NetworkConstants.CURRENT_NETWORK_ID) {
					//blacklist
					this.Quarantine(token);

					return;
				}

				e.SetBuffer(this.buffer.Bytes, token.BufferOffset, BYTES_PER_REQUESTER);
				token.Step = ValidatorConnectionInstance.Steps.ReceiveOperation;
				token.Header = header;

				if(!token.listener.ReceiveAsync(e)) {
					this.ProcessReceiveOperation(token, ref e);
				}
			} else {
				this.DisconnectClient(ref e);

				this.Quarantine(token);
			}
		}

		private void Quarantine(ValidatorConnectionInstance token, ILoggingBatcher loggingBatcher = null) {
			if(enableMarshall) {
				IPEndPoint endpoint = (IPEndPoint) token.listener.RemoteEndPoint;
				
				IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.PermanentBan, DateTimeEx.CurrentTime.AddDays(1));
			}
		}
		
		private void ProcessReceiveFrontByte(ValidatorConnectionInstance token, ref SocketAsyncEventArgs e) {

			if((e.BytesTransferred > 0) && (e.SocketError == SocketError.Success)) {
				//increment the count of the total bytes receive by the server

				if(e.BytesTransferred != 1) {
					this.DisconnectClient(ref e);

					this.Quarantine(token);

					return;
				}
 
				byte frontByte = e.Buffer[e.Offset];

				if(frontByte == PING_BYTE) {
					//TODO: ensure rate limiting here by IP.
					// send the pong
					e.Buffer[e.Offset] = PONG_BYTE;

					token.Step = ValidatorConnectionInstance.Steps.SendPong;

					if(!token.listener.SendAsync(e)) {
						this.DisconnectClient(ref e);
					}
				} else if(frontByte == ValidatorProtocolHeader.HEAD_BYTE) {

					if(!this.IsInAppointmentWindow()) {
						this.DisconnectClient(ref e);

						this.Quarantine(token);

						return;
					}

					e.SetBuffer(this.buffer.Bytes, token.BufferOffset, ValidatorProtocolHeader.MAIN_HEADER_SIZE);
					token.Step = ValidatorConnectionInstance.Steps.ReceiveHeader;

					if(!token.listener.ReceiveAsync(e)) {
						this.ProcessReceiveHeader(token, ref e);
					}
				} else {
					this.Quarantine(token);
				}
			} else {
				this.DisconnectClient(ref e);

				this.Quarantine(token);
			}
		}

		private void DisconnectClient(ref SocketAsyncEventArgs e) {
			if(e == null) {
				return;
			}
			ValidatorConnectionInstance token = e.UserToken as ValidatorConnectionInstance;

			token.Step = ValidatorConnectionInstance.Steps.Closing;

			try {
				token.listener.Shutdown(SocketShutdown.Both);
			} catch(Exception) {
				
			}
			try {
				token.listener.Dispose();
			} catch(Exception) {
				
			}

			token.listener = null;
			Interlocked.Decrement(ref this.connectedRequesters);
			
			//restore basics
			token.Step = ValidatorConnectionInstance.Steps.ReceiveFrontByte;
			e.SetBuffer(this.buffer.Bytes, token.BufferOffset, 1);

			this.asyncEventArgsPool.PutObject(e);

			this.maximumAcceptedRequesters.Release();
			NLog.LoggingBatcher.DecrementInstances();
		}

		protected virtual bool CheckShouldDisconnect(IPEndPoint endPoint) {
			if(!enableMarshall) {
				return false;
			}

			return IPMarshall.ValidationInstance.RequestIncomingConnectionClearance(endPoint.Address) == false;
		}

		/// <summary>
		///     check if we have any appointment window happening
		/// </summary>
		/// <returns></returns>
		private bool IsInAppointmentWindow() {

			foreach(BlockchainType key in this.appointmentWindowChecks.Keys) {
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

			public enum Steps {
				ReceiveFrontByte,
				ReceiveHeader,
				ReceiveOperation,
				SendPong,
				Closing,
				Finished
			}

			public ValidatorProtocolHeader Header;
			public Socket listener;
			public TcpValidatorServer server;
			public int BufferOffset;

			public Steps Step { get; set; } = Steps.ReceiveFrontByte;
		}
	}
}