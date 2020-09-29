using System;
using System.Text;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Serialization {
	/// <summary>
	///     A special utility class to serialize with encryption and hashes
	/// </summary>
	public class EncryptionSerializer {
		private readonly IEncryptorParameters encryptorParameters;

		private readonly xxHasher64 hasher = new xxHasher64();

		private readonly SafeArrayHandle nonce1 = SafeArrayHandle.Create();
		private readonly SafeArrayHandle nonce2 = SafeArrayHandle.Create();

		private readonly SafeArrayHandle secret = SafeArrayHandle.Create();

		public EncryptionSerializer(SafeArrayHandle secret, IEncryptorParameters encryptorParameters, long nonce1, long nonce2) {
			this.secret = secret;
			this.encryptorParameters = encryptorParameters;

			Span<byte> bytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(nonce1, bytes);

			this.nonce1 = SafeArrayHandle.Create(bytes.Length);
			this.nonce1.Entry.CopyFrom(bytes);

			TypeSerializer.Serialize(nonce2, bytes);

			this.nonce2 = SafeArrayHandle.Create(bytes.Length);
			this.nonce2.Entry.CopyFrom(bytes);
		}

		private long HashEntry(in Span<byte> bytes) {
			Span<byte> finalbytes = stackalloc byte[bytes.Length + (sizeof(long) * 2)];
			bytes.CopyTo(finalbytes);

			this.nonce1.Entry.CopyTo(finalbytes.Slice(bytes.Length, sizeof(long)));
			this.nonce2.Entry.CopyTo(finalbytes.Slice(bytes.Length + sizeof(long), sizeof(long)));

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

			value.TryWriteBytes(bytes);

			return this.HashEntry(bytes);
		}

		public long Hash(DateTime value) {
			return this.Hash(value.Ticks);
		}

		public long Hash(string value) {
			return this.HashEntry(Encoding.UTF8.GetBytes(value));
		}

		public SafeArrayHandle Serialize(byte value) {
			Span<byte> bytes = stackalloc byte[sizeof(byte)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		private string ConvertToBase64(in Span<byte> bytes) {

			return Convert.ToBase64String(bytes);
		}

		public string SerializeBase64(byte value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(short value) {
			Span<byte> bytes = stackalloc byte[sizeof(short)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(short value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(ushort value) {
			Span<byte> bytes = stackalloc byte[sizeof(ushort)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(ushort value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(int value) {
			Span<byte> bytes = stackalloc byte[sizeof(int)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(int value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(uint value) {
			Span<byte> bytes = stackalloc byte[sizeof(uint)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(uint value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(long value) {
			Span<byte> bytes = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(long value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(ulong value) {
			Span<byte> bytes = stackalloc byte[sizeof(ulong)];
			TypeSerializer.Serialize(value, bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(ulong value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(Guid value) {
			Span<byte> bytes = stackalloc byte[16];

			value.TryWriteBytes(bytes);

			return this.Encrypt(bytes);
		}

		public string SerializeBase64(Guid value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(DateTime value) {
			return this.Serialize(value.Ticks);
		}

		public string SerializeBase64(DateTime value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		public SafeArrayHandle Serialize(string value) {
			return this.Encrypt(Encoding.UTF8.GetBytes(value));
		}

		public string SerializeBase64(string value) {
			return this.ConvertToBase64(this.Serialize(value).Span);
		}

		private SafeArrayHandle Encrypt(in Span<byte> bytes) {
			//TODO: the toarray causes performance considerations
			return FileEncryptor.Encrypt(SafeArrayHandle.Wrap(bytes.ToArray()), this.secret, this.encryptorParameters);
		}

		private SafeArrayHandle Decrypt(in Span<byte> bytes) {
			return FileEncryptor.Decrypt(SafeArrayHandle.Wrap(bytes.ToArray()), this.secret, this.encryptorParameters);
		}

		public void Deserialize(in Span<byte> bytes, out byte value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				value = result.Span[0];

			}
		}

		public void DeserializeBase64(string base64, out byte value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out short value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);

			}
		}

		public void DeserializeBase64(string base64, out short value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out ushort value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);

			}
		}

		public void DeserializeBase64(string base64, out ushort value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out int value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);
			}
		}

		public void DeserializeBase64(string base64, out int value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out uint value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);

			}
		}

		public void DeserializeBase64(string base64, out uint value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out long value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);

			}
		}

		public void DeserializeBase64(string base64, out long value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out ulong value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				TypeSerializer.Deserialize(result.Span, out value);

			}
		}

		public void DeserializeBase64(string base64, out ulong value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}

		public void Deserialize(in Span<byte> bytes, out Guid value) {

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				value = new Guid(result.Span);

			}
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

			using(SafeArrayHandle result = this.Decrypt(bytes)) {

				value = Encoding.UTF8.GetString(result.Span);

			}
		}

		public void DeserializeBase64(string base64, out string value) {
			this.Deserialize(Convert.FromBase64String(base64), out value);
		}
	}
}