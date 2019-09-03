using System;
using System.Text;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Serialization {
	/// <summary>
	///     A special utility class to serialize with encryption and hashes
	/// </summary>
	public class EncryptionSerializer {
		private readonly EncryptorParameters encryptorParameters;

		private readonly xxHasher64 hasher = new xxHasher64();

		private readonly IByteArray nonce1;
		private readonly IByteArray nonce2;

		private readonly IByteArray secret;

		public EncryptionSerializer(IByteArray secret, EncryptorParameters encryptorParameters, long nonce1, long nonce2) {
			this.secret = secret;
			this.encryptorParameters = encryptorParameters;

			Span<byte> bytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(nonce1, bytes);

			this.nonce1 = new ByteArray(bytes.Length);
			this.nonce1.CopyFrom(bytes);

			TypeSerializer.Serialize(nonce2, bytes);

			this.nonce2 = new ByteArray(bytes.Length);
			this.nonce2.CopyFrom(bytes);
		}

		private long HashEntry(in Span<byte> bytes) {
			Span<byte> finalbytes = stackalloc byte[bytes.Length + (sizeof(long) * 2)];
			bytes.CopyTo(finalbytes);

			this.nonce1.CopyTo(finalbytes.Slice(bytes.Length, sizeof(long)));
			this.nonce2.CopyTo(finalbytes.Slice(bytes.Length + sizeof(long), sizeof(long)));

			return this.hasher.Hash(finalbytes);
		}

		public long Hash(short value) {
			Span<byte> bytes = stackalloc byte[sizeof(short)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(ushort value) {
			Span<byte> bytes = stackalloc byte[sizeof(ushort)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(int value) {
			Span<byte> bytes = stackalloc byte[sizeof(int)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(uint value) {
			Span<byte> bytes = stackalloc byte[sizeof(uint)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(long value) {
			Span<byte> bytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(ulong value) {
			Span<byte> bytes = stackalloc byte[sizeof(ulong)];
			TypeSerializer.Serialize(value, bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(Guid value) {
			Span<byte> bytes = stackalloc byte[16];

#if (NETSTANDARD2_0)
			Span<byte> guidBytes = value.ToByteArray();
			guidBytes.CopyTo(bytes);
#elif (NETCOREAPP2_2)
			value.TryWriteBytes(bytes);
#else
	throw new NotImplementedException();
#endif

			return this.HashEntry(bytes);
		}

		public long Hash(DateTime value) {
			return this.Hash(value.Ticks);
		}

		public long Hash(string value) {
			return this.HashEntry(Encoding.UTF8.GetBytes(value));
		}

		public IByteArray Serialize(byte value) {
			Span<byte> bytes = stackalloc byte[sizeof(byte)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		private string ConvertToBase64(in Span<byte> bytes) {
#if (NETSTANDARD2_0)
			return Convert.ToBase64String(bytes.ToArray());
#elif (NETCOREAPP2_2)
			return Convert.ToBase64String(bytes);
#else
	throw new NotImplementedException();
#endif
		}

		public string SerializeBase64(byte value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(short value) {
			Span<byte> bytes = stackalloc byte[sizeof(short)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(short value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(ushort value) {
			Span<byte> bytes = stackalloc byte[sizeof(ushort)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(ushort value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(int value) {
			Span<byte> bytes = stackalloc byte[sizeof(int)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(int value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(uint value) {
			Span<byte> bytes = stackalloc byte[sizeof(uint)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(uint value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(long value) {
			Span<byte> bytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(long value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(ulong value) {
			Span<byte> bytes = stackalloc byte[sizeof(ulong)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(ulong value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(Guid value) {
			Span<byte> bytes = stackalloc byte[16];

#if (NETSTANDARD2_0)
			Span<byte> guidBytes = value.ToByteArray();
			guidBytes.CopyTo(bytes);
#elif (NETCOREAPP2_2)
			value.TryWriteBytes(bytes);
#else
	throw new NotImplementedException();
#endif

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(Guid value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(DateTime value) {
			return this.Serialize(value.Ticks);
		}

		public string SerializeBase64(DateTime value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public IByteArray Serialize(string value) {
			return this.Encrypt(Encoding.UTF8.GetBytes(value));
		}

		public string SerializeBase64(string value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		private IByteArray Encrypt(in Span<byte> bytes) {
			return AESFileEncryptor.Encrypt(bytes, this.secret, this.encryptorParameters);
		}

		private IByteArray Decrypt(in Span<byte> bytes) {
			return AESFileEncryptor.Decrypt(bytes, this.secret, this.encryptorParameters);
		}

		public void Deserialize(in Span<byte> bytes, out byte value) {

			IByteArray result = this.Decrypt(bytes);

			value = result.Span[0];

			result.Return();
		}

		public void DeserializeBase64(string base64, out byte value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out short value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out short value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out ushort value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out ushort value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out int value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out int value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out uint value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out uint value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out long value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out long value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out ulong value) {

			IByteArray result = this.Decrypt(bytes);

			TypeSerializer.Deserialize(result.Span, out value);

			result.Return();
		}

		public void DeserializeBase64(string base64, out ulong value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out Guid value) {

			IByteArray result = this.Decrypt(bytes);

#if (NETSTANDARD2_0)
			value = new Guid(result.ToExactByteArray());
#elif (NETCOREAPP2_2)
			value = new Guid(result.Span);
#else
	throw new NotImplementedException();
#endif

			result.Return();
		}

		public void DeserializeBase64(string base64, out Guid value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out DateTime value) {

			this.Deserialize(bytes, out long result);

			value = new DateTime(result);
		}

		public void DeserializeBase64(string base64, out DateTime value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out string value) {

			IByteArray result = this.Decrypt(bytes);

#if (NETSTANDARD2_0)
			value = Encoding.UTF8.GetString(result.ToExactByteArray());
#elif (NETCOREAPP2_2)
			value = Encoding.UTF8.GetString(result.Span);
#else
	throw new NotImplementedException();
#endif

			result.Return();
		}

		public void DeserializeBase64(string base64, out string value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}
	}
}