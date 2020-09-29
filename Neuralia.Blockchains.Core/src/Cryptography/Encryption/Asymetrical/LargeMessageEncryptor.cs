using System;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical {
	public static class LargeMessageEncryptor {

		private const int HASH_SIZE = 64;
		private const int PASSWORD_LENGTH = HASH_SIZE - SALT_LENGTH;
		private const int SALT_LENGTH = 16;
		
		public static ComponentVersion Version = new ComponentVersion(1,0);
		
		public enum EncryptionStrength:byte {
			Regular=0, Strong=1
		}
		
		public static SafeArrayHandle Encrypt(SafeArrayHandle message, SafeArrayHandle publicKey, EncryptionStrength encryptionStrength = EncryptionStrength.Regular) {

			var param = GetParameters(encryptionStrength);
			using var buffer = SafeArrayHandle.Create(param.InitialBufferSize);
			buffer.FillSafeRandom();
			using var password = HashingUtils.HashSha512(buffer);
			
			buffer.FillSafeRandom();
			using var salt = HashingUtils.HashSha3_512(buffer);
			
			TypeSerializer.Deserialize(salt.Span.Slice(0, sizeof(int)), out int iterations1);

			using var hash = SafeArrayHandle.Create(64);
			
			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), GetIterations(iterations1, param.IterationsMin, param.IterationsMax), HashAlgorithmName.SHA512)) {
				ByteArray.Wrap(rfc2898DeriveBytes.GetBytes(HASH_SIZE)).CopyTo(hash.Entry);
			}
			
			using NTRUPrimeEncryptor ntru = new NTRUPrimeEncryptor(param.NTRUStrength);
				
			(var encryptedKey, var symmetricKey) = ntru.Encrypt(hash, publicKey);
			
			using XchachaEncryptor xchachaEncryptor = new XchachaEncryptor(param.XChachaRounds);

			using var password2 = (SafeArrayHandle)symmetricKey.Entry.Slice(0, PASSWORD_LENGTH);
			using var salt2 = (SafeArrayHandle)symmetricKey.Entry.Slice(PASSWORD_LENGTH, SALT_LENGTH);
			
			TypeSerializer.Deserialize(password2.Span.Slice(0, sizeof(int)), out int iterations2);
			using var encryptedMessage = xchachaEncryptor.Encrypt(message, password2, salt2, GetIterations(iterations2, param.IterationsMin, param.IterationsMax));
			
			using IDataDehydrator finalDehydrator = DataSerializationFactory.CreateDehydrator();
			
			Version.Dehydrate(finalDehydrator);
			
			finalDehydrator.Write((byte)encryptionStrength);
			
			finalDehydrator.WriteNonNullable(encryptedKey);
			finalDehydrator.WriteNonNullable(encryptedMessage);
				
			return finalDehydrator.ToArray();
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle encrypted, SafeArrayHandle privateKey, EncryptionStrength encryptionStrength = EncryptionStrength.Regular) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(encrypted);

			ComponentVersion messageVersion = new ComponentVersion();
			messageVersion.Rehydrate(rehydrator);
			EncryptionStrength messageEncryptionStrength = (EncryptionStrength)rehydrator.ReadByte();
			
			if(messageVersion != Version) {
				throw new ApplicationException($"Encrypted message version {messageVersion} is incompatible with this encryptor version {Version}");
			}
			
			if(messageEncryptionStrength != encryptionStrength) {
				throw new ApplicationException($"Encrypted message strength {messageEncryptionStrength} is incompatible with expected message strength {encryptionStrength}");
			}
			
			var param = GetParameters(encryptionStrength);

			using var encryptedKey = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			using var encryptedMessage = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			
			using NTRUPrimeEncryptor ntru = new NTRUPrimeEncryptor(param.NTRUStrength);
			
			using var symmetricKey = ntru.Decrypt(encryptedKey, privateKey);
			
			using var password = (SafeArrayHandle)symmetricKey.Entry.Slice(0, PASSWORD_LENGTH);
			using var salt = (SafeArrayHandle)symmetricKey.Entry.Slice(PASSWORD_LENGTH, SALT_LENGTH);

			using XchachaEncryptor xchachaEncryptor = new XchachaEncryptor(param.XChachaRounds);

			TypeSerializer.Deserialize(password.Span.Slice(0, sizeof(int)), out int iterations);
			return xchachaEncryptor.Decrypt(encryptedMessage, password, salt, GetIterations(iterations, param.IterationsMin, param.IterationsMax));
		}
		
		private static Parameters GetParameters( EncryptionStrength encryptionStrength) {
			
			Parameters param = new Parameters();

			if(encryptionStrength == EncryptionStrength.Strong) {
				param.InitialBufferSize = 3000;
				param.IterationsMin = 3000;
				param.IterationsMax = 10_000;
				param.XChachaRounds = XChaCha.CHACHA_40_ROUNDS;
				param.NTRUStrength = NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857;
			} else {
				param.InitialBufferSize = 300;
				param.IterationsMin = 1000;
				param.IterationsMax = 5000;
				param.XChachaRounds = XChaCha.CHACHA_20_ROUNDS;
				param.NTRUStrength = NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761;
			}

			return param;
		}
		private struct Parameters {
			public int InitialBufferSize;
			public int IterationsMin;
			public int IterationsMax;
			public int XChachaRounds;
			public NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes NTRUStrength;
		}
		
		private static int GetIterations(int value, int min, int max) {
			return Math.Min(Math.Max(Math.Abs(value) & 0xFFFF, min), max);
		}
	}
}