using System;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash {
	public class THSxxHash : THSHashBase {

		private readonly xxHasher64 xxHash = new xxHasher64();

		public override int HashType => 512;

		public override SafeArrayHandle Hash(SafeArrayHandle message) {

			long hashLong = this.xxHash.Hash(message);

			SafeArrayHandle hash = SafeArrayHandle.Create(64);
			hash.CopyFrom(message);

			Span<byte> buffer = hash.Span;
			Span<byte> buffer2 = stackalloc byte[sizeof(long)];

			void Apply(long entry, Span<byte> hashBuffer) {
				Span<byte> slice = hashBuffer.Slice(0, sizeof(long));
				TypeSerializer.Deserialize(slice, out long temp);
				TypeSerializer.Serialize(temp ^ hashLong, slice);

				slice = hashBuffer.Slice(sizeof(long), sizeof(long));
				TypeSerializer.Deserialize(slice, out temp);
				TypeSerializer.Serialize(temp & hashLong, slice);

				slice = hashBuffer.Slice(sizeof(long) * 2, sizeof(long));
				TypeSerializer.Deserialize(slice, out temp);
				TypeSerializer.Serialize(temp | hashLong, slice);

				slice = hashBuffer.Slice(sizeof(long) * 3, sizeof(long));
				TypeSerializer.Deserialize(slice, out temp);
				TypeSerializer.Serialize(hashLong, slice);
			}

			Apply(hashLong, buffer.Slice(0, 32));
			Apply(~hashLong, buffer.Slice(32, 32));

			return hash;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.xxHash?.Dispose();
		}
	}
}