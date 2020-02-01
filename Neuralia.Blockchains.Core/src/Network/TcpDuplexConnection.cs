using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Network.ReadingContexts;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Pipelines.Sockets.Unofficial;
using Serilog;

namespace Neuralia.Blockchains.Core.Network {
	public class TcpDuplexConnection : TcpConnection<PipelineReadingContext> {

		protected SocketConnection clientPipe;

		public TcpDuplexConnection(TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(exceptionCallback, isServer, protocolMessageFilters) {
		}

		public TcpDuplexConnection(Socket socket, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(socket, exceptionCallback, isServer, protocolMessageFilters) {
		}

		public TcpDuplexConnection(NetworkEndPoint remoteEndPoint, TcpConnection.ExceptionOccured exceptionCallback, bool isServer = false, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters = null) : base(remoteEndPoint, exceptionCallback, isServer, protocolMessageFilters) {
		}

		protected override void SocketNewlyConnected() {
			if(this.clientPipe != null) {
				return;
			}

			//TODO: ensure that this buffer is the right size. for now, default multiplied by 9 seems to be a good value
			// lets set our options. We first increase the buffer size to 294912 BYTES
			int minimumSegmentSize = PipeOptions.Default.MinimumSegmentSize * 9;

			// reverse determine it from this calculation. usually, it will be equal to 16 as per source code.
			int segmentPoolSize = (int) PipeOptions.Default.PauseWriterThreshold / PipeOptions.Default.MinimumSegmentSize;

			int defaultResumeWriterThreshold = (minimumSegmentSize * segmentPoolSize) / 2;
			int defaultPauseWriterThreshold = minimumSegmentSize * segmentPoolSize;
			this.receiveBufferSize = defaultPauseWriterThreshold;

			PipeOptions receivePipeOptions = new PipeOptions(null, null, null, defaultPauseWriterThreshold, defaultResumeWriterThreshold, minimumSegmentSize);
			PipeOptions sendPipeOptions = new PipeOptions(null, null, null, PipeOptions.Default.PauseWriterThreshold, PipeOptions.Default.ResumeWriterThreshold, PipeOptions.Default.MinimumSegmentSize, PipeOptions.Default.UseSynchronizationContext);
			const SocketConnectionOptions socketConnectionOptions = SocketConnectionOptions.ZeroLengthReads; 
			this.clientPipe = SocketConnection.Create(this.socket, sendPipeOptions, receivePipeOptions, socketConnectionOptions);
		}

		/// <summary>
		///     Write to the socket, but dont sent it. amke sure you call CompleteWrite()
		/// </summary>
		/// <param name="message"></param>
		protected override void WritePart(in ReadOnlySpan<byte> message) {
			this.clientPipe.Output.Write(message);
		}

		/// <summary>
		///     Send anything in the buffer
		/// </summary>
		/// <returns></returns>
		protected override Task<bool> CompleteWrite() {

			return Flush(this.clientPipe.Output).AsTask();
		}

		protected static ValueTask<bool> Flush(PipeWriter writer) {
			static bool GetResult(FlushResult flush) {
				return !(flush.IsCanceled || flush.IsCompleted);
			}

			async ValueTask<bool> Awaited(ValueTask<FlushResult> incomplete) {
				return GetResult(await incomplete);
			}

			var flushTask = writer.FlushAsync();

			return flushTask.IsCompletedSuccessfully ? new ValueTask<bool>(GetResult(flushTask.Result)) : Awaited(flushTask);
		}

		protected override async Task<PipelineReadingContext> ReadDataFrame(PipelineReadingContext previousContext, CancellationToken ct) {
			return new PipelineReadingContext(await this.clientPipe.Input.ReadAsync(ct), this.clientPipe.Input);
		}

		protected override void ReadTaskCancelled() {
			this.clientPipe.Input.Complete();
		}

		protected override void DisposeSocket() {

			try {
				this.CompleteWrite().Wait(TimeSpan.FromSeconds(3));
			}catch {
				// do nothing, we tried
			}

			base.DisposeSocket();

			try {
				this.clientPipe?.Dispose();
			} catch {
				// do nothing, we tried
			}

			// lets give it some time to complete
			Thread.Sleep(100);
			this.clientPipe = null;
		}
	}

	
}