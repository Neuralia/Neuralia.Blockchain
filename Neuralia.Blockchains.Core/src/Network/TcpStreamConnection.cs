using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Network.ReadingContexts;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;

namespace Neuralia.Blockchains.Core.Network {

	public class TcpStreamConnection : TcpConnection<StreamReadingContext> {

		private readonly Memory<byte> buffer = new byte[4096];

		protected NetworkStream networkStream;

		public TcpStreamConnection(TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(exceptionCallback, isServer, protocolMessageFilters) {
		}

		public TcpStreamConnection(Socket socket, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(socket, exceptionCallback, isServer, protocolMessageFilters) {
		}

		public TcpStreamConnection(NetworkEndPoint remoteEndPoint, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(remoteEndPoint, exceptionCallback, isServer, protocolMessageFilters) {
		}

		protected override void SocketNewlyConnected() {
			if(this.networkStream == null) {
				this.networkStream = new NetworkStream(this.socket, false);
				this.networkStream.WriteTimeout = 10000;
				this.networkStream.ReadTimeout = 10000;
			}
		}

		protected override void WritePart(in ReadOnlySpan<byte> message) {
			this.networkStream.Write(message);
		}

		protected override Task<bool> CompleteWrite() {
			this.networkStream.Flush();

			return Task.FromResult(true);
		}

		protected override async Task<StreamReadingContext> ReadDataFrame(StreamReadingContext previousContext, CancellationToken ct) {

			if(previousContext.AllDataRead) {
				return new StreamReadingContext(await this.ReadData(ct).ConfigureAwait(false));
			}

			// there is more data to read, so lets keep going
			return previousContext;
		}

		private async Task<Memory<byte>> ReadData(CancellationToken ct) {

			Task<int> asyncReadTask = this.networkStream.ReadAsync(this.buffer, ct).AsTask();

			while(true) {

				await Task.WhenAny(asyncReadTask, Task.Delay(1000, ct)).ConfigureAwait(false);

				if(ct.IsCancellationRequested) {
					this.ReadTaskCancelled();
					ct.ThrowIfCancellationRequested();
				}

				if(!asyncReadTask.IsCompleted) {
					continue;
				}

				// ReSharper disable once AsyncConverter.AsyncWait
				if(asyncReadTask.Result != 0) {
					// ReSharper disable once AsyncConverter.AsyncWait
					return this.buffer.Slice(0, asyncReadTask.Result);
				}

				return Memory<byte>.Empty;
			}
		}

		protected override void DisposeAll() {

			base.DisposeAll();

			try {
				this.networkStream?.Dispose();
			} catch {

			}
		}

		protected override void DisposeSocket() {

			try {
				this.networkStream?.Flush();
				this.networkStream?.Close();
			} catch {

			}

			base.DisposeSocket();
		}
	}
}