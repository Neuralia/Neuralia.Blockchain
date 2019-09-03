using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Pipelines.Sockets.Unofficial;

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
			if(this.clientPipe == null) {
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

				this.clientPipe = SocketConnection.Create(this.socket, sendPipeOptions, receivePipeOptions);
			}
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
			bool GetResult(FlushResult flush) {
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

				this.clientPipe?.Input.CancelPendingRead();
				this.clientPipe?.Output.CancelPendingFlush();

				this.clientPipe?.Input.Complete();
				this.clientPipe?.Output.Complete();
			} catch {
				// do nothing, we tried
			}

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

	public struct PipelineReadingContext : ITcpReadingContext {

		public readonly ReadResult readResult;
		public readonly PipeReader reader;

		public PipelineReadingContext(ReadResult readResult, PipeReader reader) {
			this.readResult = readResult;
			this.reader = reader;
		}

		public bool IsCanceled => this.readResult.IsCanceled;
		public bool IsCompleted => this.readResult.IsCompleted;
		public bool IsEmpty => this.readResult.Buffer.IsEmpty;
		public long Length => this.readResult.Buffer.Length;

		public void DataRead(int amount) {
			this.reader.AdvanceTo(this.readResult.Buffer.GetPosition(amount));
		}

		public void CopyTo(in Span<byte> dest, int srcOffset, int destOffset, int length) {
			this.readResult.Buffer.Slice(srcOffset, length).CopyTo(dest.Slice(destOffset, length));
		}

		public byte this[int i] => this.readResult.Buffer.Slice(i, 1).First.Span[0];
	}
}