using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public class ValidatorProtocolHeader {

		public const int HEADER_SIZE = sizeof(ushort) + sizeof(int) + sizeof(ushort);

		public ushort ProtocolVersion { get; set; }
		public int NetworkId { get; set; }
		public BlockchainType ChainId { get; set; } = new BlockchainType();

		public void Rehydrate(ByteArray bytes) {

			TypeSerializer.Deserialize(bytes.Span.Slice(0, sizeof(ushort)), out ushort protocolVersion);
			this.ProtocolVersion = protocolVersion;

			TypeSerializer.Deserialize(bytes.Span.Slice(sizeof(ushort), sizeof(int)), out int networkid);
			this.NetworkId = networkid;

			TypeSerializer.Deserialize(bytes.Span.Slice(sizeof(ushort) + sizeof(int), sizeof(ushort)), out ushort chainId);
			this.ChainId.Value = chainId;
		}

		public SafeArrayHandle Dehydrate(SafeArrayHandle operationBytes) {

			var result = SafeArrayHandle.Create(HEADER_SIZE + operationBytes.Length);

			TypeSerializer.Serialize(this.ProtocolVersion, result.Span.Slice(0, sizeof(ushort)));
			TypeSerializer.Serialize(this.NetworkId, result.Span.Slice(sizeof(ushort), sizeof(int)));
			TypeSerializer.Serialize(this.ChainId.Value, result.Span.Slice(sizeof(ushort) + sizeof(int), sizeof(ushort)));

			operationBytes.CopyTo(result, 0, HEADER_SIZE, operationBytes.Length);

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