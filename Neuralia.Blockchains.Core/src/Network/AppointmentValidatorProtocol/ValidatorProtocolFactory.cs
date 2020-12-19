using System;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public class ValidatorProtocolHeader {

		public const byte           HEAD_BYTE   = 1;
		
		public const byte HEAD_BYTE_SIZE   = 1;
		public const int  MAIN_HEADER_SIZE = sizeof(ushort) + sizeof(int) + sizeof(ushort);
		public const int  FULL_HEADER_SIZE = HEAD_BYTE_SIZE + MAIN_HEADER_SIZE;
		
		public       ushort         ProtocolVersion { get; set; }
		public       int            NetworkId       { get; set; }
		public       BlockchainType ChainId         { get; set; } = new BlockchainType();

		public void Rehydrate(byte headByte, ByteArray bytes) {
			
			this.RehydrateParts(headByte, 0, bytes);
		}
		
		public void Rehydrate(ByteArray bytes) {

			this.RehydrateParts(bytes[0], HEAD_BYTE_SIZE, bytes);
		}

		private void RehydrateParts(int headByte, int offset, ByteArray bytes) {
			
			if(headByte != HEAD_BYTE) {
				throw new ApplicationException("Invalid head byte");
			}

			TypeSerializer.Deserialize(bytes.Span.Slice(offset, sizeof(ushort)), out ushort protocolVersion);
			this.ProtocolVersion = protocolVersion;

			TypeSerializer.Deserialize(bytes.Span.Slice(offset + sizeof(ushort), sizeof(int)), out int networkid);
			this.NetworkId = networkid;

			TypeSerializer.Deserialize(bytes.Span.Slice(offset + sizeof(ushort) + sizeof(int), sizeof(ushort)), out ushort chainId);
			this.ChainId.Value = chainId;
		}
		
		public SafeArrayHandle Dehydrate(SafeArrayHandle operationBytes) {

			var result = SafeArrayHandle.Create(FULL_HEADER_SIZE + operationBytes.Length);

			result[0] = HEAD_BYTE;
			TypeSerializer.Serialize(this.ProtocolVersion, result.Span.Slice(HEAD_BYTE_SIZE, sizeof(ushort)));
			TypeSerializer.Serialize(this.NetworkId, result.Span.Slice(HEAD_BYTE_SIZE     + sizeof(ushort), sizeof(int)));
			TypeSerializer.Serialize(this.ChainId.Value, result.Span.Slice(HEAD_BYTE_SIZE + sizeof(ushort) + sizeof(int), sizeof(ushort)));

			operationBytes.CopyTo(result, 0, FULL_HEADER_SIZE, operationBytes.Length);

			return result;
		}
	}

	public static class ValidatorProtocolFactory {

		public static IValidatorProtocol GetValidatorProtocolInstance(ushort version, BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate) {

			if(version == ValidatorProtocol1.PROTOCOL_VERSION) {
				return new ValidatorProtocol1(blockchainType, getValidatorDelegate);
			}

			throw new InvalidOperationException();
		}
	}
}