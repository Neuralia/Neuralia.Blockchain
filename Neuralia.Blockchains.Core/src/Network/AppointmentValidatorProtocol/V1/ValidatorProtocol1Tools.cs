using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1 {
	public static class ValidatorProtocol1Tools {

		public static TcpValidatorConnection BuildConnection(IPAddress address, int? port = null) {
			int actualPort = GlobalsService.DEFAULT_VALIDATOR_PORT;

			if(port.HasValue) {
				actualPort = port.Value;
			}
			
			return new TcpValidatorConnection(new NetworkEndPoint(address, actualPort, IPMode.Both), ex => {
			});
		}
		
		public static async Task<ITcpValidatorConnection> Connect(ValidatorOperation operation, ushort blockchainId, IPAddress address, int? port = null) {

			var connection = BuildConnection(address, port);

			using SafeArrayHandle operationBytes = GetOperationBytes(operation);

			var header = new ValidatorProtocolHeader();

			header.NetworkId = NetworkConstants.CURRENT_NETWORK_ID;
			header.ProtocolVersion = ValidatorProtocol1.PROTOCOL_VERSION;
			header.ChainId = blockchainId;

			await connection.Connect(() => header.Dehydrate(operationBytes)).ConfigureAwait(false);

			return connection;
		}
		
		public static async Task<OPERATION> ReceiveOperation<OPERATION>(ITcpValidatorConnection connection, CancellationToken ct)
			where OPERATION : ValidatorOperation {

			using ByteArray sizeBuffer = await connection.ReadData(sizeof(ushort), ct).ConfigureAwait(false);

			TypeSerializer.Deserialize(sizeBuffer.Span, out ushort size);

			using ByteArray resultBytes = await connection.ReadData(size, ct).ConfigureAwait(false);

			var envelope = new ValidatorProtocol1.Protocol1Envelope();
			envelope.Rehydrate(resultBytes, false);

			return (OPERATION) envelope.operation;
		}

		public static SafeArrayHandle GetOperationBytes(ValidatorOperation operation) {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			operation.Dehydrate(dehydrator);
			using SafeArrayHandle bytes = dehydrator.ToArray();

			var operationBytes = SafeArrayHandle.Create(sizeof(ushort) + bytes.Length);
			TypeSerializer.Serialize((ushort) bytes.Length, operationBytes.Span.Slice(0, sizeof(ushort)));
			bytes.Span.CopyTo(operationBytes.Span.Slice(sizeof(ushort), bytes.Length));

			return operationBytes;
		}

		public static bool SendOperation(ValidatorOperation operation, ValidatorConnectionSet connectionSet) {

			using SafeArrayHandle sendBytes = GetOperationBytes(operation);

			connectionSet.SocketAsyncEventArgs.SetBuffer(sendBytes.Bytes, sendBytes.Offset, sendBytes.Length);
			return connectionSet.Socket.SendAsync(connectionSet.SocketAsyncEventArgs);
		}

		public abstract class ValidatorOperation : IBinarySerializable {

			public abstract byte OperationId { get; }

			public virtual void Rehydrate(IDataRehydrator rehydrator) {

			}

			public virtual void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.Write(this.OperationId);
			}
		}
	}
}