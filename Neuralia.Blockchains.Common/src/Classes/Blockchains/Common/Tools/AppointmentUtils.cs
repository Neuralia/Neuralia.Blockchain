using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.IO;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.Chacha;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {

	public static class AppointmentUtils {

		public const int PUZZLE_KEY_SIZE = 300;
		public const int PUZZLE_SALT_SIZE = 100;

		public const int IdentityKeyHeight = 8;
		public const Enums.KeyHashType IdentityKeyHash = Enums.KeyHashType.SHA2_256;
		public const Enums.KeyHashType IdentityBackupKeyHash = Enums.KeyHashType.SHA3_256;
		public const byte IdentityNonceExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT;

		public const int HASH_SIZE = 64;
		public const int PASSWORD_LENGTH = HASH_SIZE - SALT_LENGTH;
		public const int SALT_LENGTH = 16;
		public const int CODE_LENGTH = 900;
		public const int CODE_PASSWORD_LENGTH = CODE_LENGTH - CODE_SALT_LENGTH;
		public const int CODE_SALT_LENGTH = 100;

		public static SafeArrayHandle BuildSecretConfirmationCorrelationCodeSeed(List<Guid> publicValidators, List<Guid> secretValidators, int appointmentKeyHash, int secretCode) {
			
			publicValidators = publicValidators.OrderBy(v => v).ToList();
			secretValidators = secretValidators.OrderBy(v => v).ToList();
			
			var adjustedPublicValidators = publicValidators.Skip(1).ToList();
			adjustedPublicValidators.AddRange(secretValidators);
			var first = publicValidators.First();

			using SafeArrayHandle password = SafeArrayHandle.Create((16 * adjustedPublicValidators.Count) + sizeof(int) + sizeof(int));
			using SafeArrayHandle salt = SafeArrayHandle.Create(16 + sizeof(int));

			using var buffer = SafeArrayHandle.Create(16);
			int offset = 0;

			foreach(var validator in adjustedPublicValidators) {

				TypeSerializer.Serialize(validator, buffer.Span);

				buffer.CopyTo(password, 0, offset, buffer.Length);
				offset += buffer.Length;
			}

			TypeSerializer.Serialize(appointmentKeyHash, buffer.Span.Slice(0, sizeof(int)));
			TypeSerializer.Serialize(secretCode, buffer.Span.Slice(sizeof(int), sizeof(int)));
			buffer.CopyTo(password, 0, offset, sizeof(int) * 2);

			TypeSerializer.Serialize(first, buffer.Span);
			buffer.CopyTo(salt, 0, 0, buffer.Length);

			TypeSerializer.Serialize(secretCode, buffer.Span.Slice(0, sizeof(int)));
			buffer.CopyTo(salt, 0, buffer.Length, sizeof(int));

			using var dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.WriteNonNullable(password);
			dehydrator.WriteNonNullable(salt);

			return dehydrator.ToArray();
		}

		public static (bool verifided, long? appointmentConfirmationCode) DecryptSecretConfirmationCorrelationCode(SafeArrayHandle bytes, SafeArrayHandle seed) {

			using var rehydrator = DataSerializationFactory.CreateRehydrator(seed);
			using var password = (SafeArrayHandle) rehydrator.ReadNonNullableArray();
			using var salt = (SafeArrayHandle) rehydrator.ReadNonNullableArray();

			using var decryptedBytes = Decrypt(bytes, password, salt);

			using var rehydrator2 = DataSerializationFactory.CreateRehydrator(decryptedBytes);
			bool verified = rehydrator2.ReadBool();
			long? appointmentConfirmationCode = rehydrator2.ReadNullableLong();

			return (verified, appointmentConfirmationCode);
		}

		// public static (bool verifided, long appointmentConfirmationCode) DecryptSecretConfirmationCorrelationCode(SafeArrayHandle bytes, SafeArrayHandle privateKey) {
		// 	using NTRUEncryptor ntru = new NTRUEncryptor();
		// 	using var decryptedBytes = ntru.Decrypt(bytes, privateKey);
		//
		// 	using var rehydrator = DataSerializationFactory.CreateRehydrator(decryptedBytes);
		// 	bool verified = rehydrator.ReadBool();
		// 	long appointmentConfirmationCode = rehydrator.ReadLong();
		// 	
		// 	return (verified, appointmentConfirmationCode);
		// }
		//
		public static SafeArrayHandle ValidatorRestoreKeyCode(DateTime appointment, SafeArrayHandle publicKey, SafeArrayHandle secretCode, SafeArrayHandle stride) {

			long hash = GetValidatorKeyHash(publicKey, appointment);

			return ValidatorRestoreKeyCode(appointment, hash, secretCode, stride);
		}

		public static SafeArrayHandle ValidatorRestoreKeyCode(DateTime appointment, long publicKeyHash, SafeArrayHandle secretCode, SafeArrayHandle stride) {
			
			(Guid validatorSecretKeyCode1, Guid validatorSecretKeyCode2) = GetValidatorSecretKeyCode(appointment, publicKeyHash, stride);

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(secretCode);
			var secretCode1 = rehydrator.ReadNonNullableArray();
			var secretCode2 = rehydrator.ReadNonNullableArray();

			var resultingCode = SafeArrayHandle.Create(32);
			var code = GuidDelta.Rebuild(secretCode1, validatorSecretKeyCode1);
			TypeSerializer.Serialize(code, resultingCode.Span.Slice(0, 16));

			code = GuidDelta.Rebuild(secretCode2, validatorSecretKeyCode2);
			TypeSerializer.Serialize(code, resultingCode.Span.Slice(16, 16));
			
			return resultingCode;
		}
		
		/// <summary>
		/// builds a random but valid entry for decoy purposes
		/// </summary>
		/// <returns></returns>
		public static SafeArrayHandle GetDummyValidatorSecretDelta() {
			
			using var validatorCodeMap1 = GuidDelta.CreateMap(Guid.NewGuid(), Guid.NewGuid());
			using var validatorCodeMap2 = GuidDelta.CreateMap(Guid.NewGuid(), Guid.NewGuid());

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.WriteNonNullable(validatorCodeMap1);
			dehydrator.WriteNonNullable(validatorCodeMap2);

			return dehydrator.ToArray();
		}

		private static long Roll(long value, long iterations) {

			int slide = (int) ((iterations & 0xC0) >> 6);
			int shift = (int) (iterations & (0x3F << slide)) >> slide;

			return (value << shift) | (value >> -shift);
		}

		public static (Guid code1, Guid code2) GetValidatorSecretKeyCode(DateTime appointment, long hash, SafeArrayHandle stride) {

			(long nonce1, long nonce2, long nonce3, long nonce4) = ExtractNonces(stride);

			long nonceA = 0;
			int part = (int) (hash & 0x3);
			long ticks = appointment.Ticks;

			nonce3 = Roll(nonce3, hash);

			if(part == 0) {
				nonceA = nonce1 ^ nonce2 ^ hash;
			} else if(part == 1) {
				nonceA = nonce2 ^ nonce3 ^ hash;
			} else if(part == 2) {
				nonceA = nonce1 ^ nonce3 ^ hash;
			} else if(part == 3) {
				nonceA = nonce1 ^ nonce2 ^ nonce3 ^ hash;
			}

			long nonceB = 0;
			part = (int) ((hash & 0xC) >> 2);

			if(part == 0) {
				nonceB = hash ^ nonce1;
			} else if(part == 1) {
				nonceB = hash ^ nonce2;
			} else if(part == 2) {
				nonceB = hash ^ nonce3;
			} else if(part == 3) {
				nonceB = hash ^ nonce2 ^ nonce3;
			}

			bool side = (hash & 0x40) == 0;

			if(side) {
				long temp = nonceA;
				nonceA = nonceB;
				nonceB = temp;
			}

			using var buffer = SafeArrayHandle.Create(16);
			TypeSerializer.Serialize(nonceA, buffer.Span.Slice(0, 8));
			TypeSerializer.Serialize(ticks, buffer.Span.Slice(8, 8));

			nonceA = HashingUtils.XxHash64(buffer);

			TypeSerializer.Serialize(nonceA, buffer.Span.Slice(0, 8));
			TypeSerializer.Serialize(nonceB, buffer.Span.Slice(8, 8));

			using var preSalt = SafeArrayHandle.Create(16);

			TypeSerializer.Serialize(nonce1 ^ nonce2 ^ nonce3, preSalt.Span);
			TypeSerializer.Serialize(nonce4 ^ ticks ^ nonce3, preSalt.Span.Slice(8, 8));

			using var salt = HashingUtils.HashSha3_512(preSalt);
			using var password = HashingUtils.HashSha3_512(buffer);

			ByteArray validatorSecretKeyBytes1 = null;
			ByteArray validatorSecretKeyBytes2 = null;

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), Math.Min(Math.Max((int) (nonce1 & 0xFFFF), 1000), 10000), HashAlgorithmName.SHA512)) {

				validatorSecretKeyBytes1 = ByteArray.Wrap(rfc2898DeriveBytes.GetBytes(16));
				validatorSecretKeyBytes2 = ByteArray.Wrap(rfc2898DeriveBytes.GetBytes(16));
			}

			// lets build the key code delta
			TypeSerializer.Deserialize(validatorSecretKeyBytes1.Span, out Guid validatorSecretKeyCode1);
			TypeSerializer.Deserialize(validatorSecretKeyBytes2.Span, out Guid validatorSecretKeyCode2);

			validatorSecretKeyBytes1.Dispose();
			validatorSecretKeyBytes2.Dispose();

			return (validatorSecretKeyCode1, validatorSecretKeyCode2);
		}

		/// <summary>
		/// generate a deterministic non identifying hash for a validator
		/// </summary>
		/// <param name="appointment"></param>
		/// <param name="appointmentKey"></param>
		/// <param name="accountId"></param>
		/// <param name="nonce1"></param>
		/// <param name="nonce2"></param>
		/// <param name="nonce3"></param>
		/// <param name="nonce4"></param>
		/// <returns></returns>
		public static Guid GetValidatorIdHash(DateTime appointment, AccountId accountId, SafeArrayHandle stride) {

			(long nonce1, long nonce2, long nonce3, long nonce4) = ExtractNonces(stride);

			long nonceA = 0;
			long accountIdLong = accountId.ToLongRepresentation();
			int part = (int) ((accountIdLong ^ nonce2) & 0x3);

			long ticks = appointment.Ticks;

			nonce1 = Roll(nonce1, accountIdLong);

			if(part == 0) {
				nonceA = nonce2 ^ nonce3 ^ ticks;
			} else if(part == 1) {
				nonceA = nonce1 ^ ticks;
			} else if(part == 2) {
				nonceA = nonce3 ^ (nonce1 / 3) ^ ticks;
			} else if(part == 3) {
				nonceA = (nonce2 * 3) ^ ticks;
			}

			long nonceB = 0;
			part = (int) ((accountIdLong & 0xC) >> 2);

			if(part == 0) {
				nonceB = ticks ^ nonce1;
			} else if(part == 1) {
				nonceB = ticks ^ nonce2;
			} else if(part == 2) {
				nonceB = ticks ^ nonce3;
			} else if(part == 3) {
				nonceB = ticks ^ nonce2 ^ nonce3;
			}

			bool side = ((accountIdLong ^ nonce1) & 0x40) == 0;

			using var dateBytes = SafeArrayHandle.Create(8);
			TypeSerializer.Serialize(ticks, dateBytes.Span);

			using var accountIdBytes = SafeArrayHandle.Create(8);
			TypeSerializer.Serialize(accountIdLong, accountIdBytes.Span);

			using var buffer = SafeArrayHandle.Create(16);
			TypeSerializer.Serialize(nonceA ^ ticks, buffer.Span.Slice(0, 8));
			TypeSerializer.Serialize(nonce4 ^ nonce1, buffer.Span.Slice(8, 8));
			using SafeArrayHandle lastHash = HashingUtils.GenerateMd5Hash(buffer);

			using var prePassword = SafeArrayHandle.Create(8 + 8 + +16);

			if(side) {
				long temp = nonceA;
				nonceA = nonceB;
				nonceB = temp;

				dateBytes.CopyTo(prePassword, 0, 0, 8);
				accountIdBytes.CopyTo(prePassword, 0, 8, 8);
				lastHash.CopyTo(prePassword, 0, 8 + 8, 16);

			} else {

				dateBytes.CopyTo(prePassword, 0, 0, 8);
				lastHash.CopyTo(prePassword, 0, 8, 16);
				accountIdBytes.CopyTo(prePassword, 0, 8 + 16, 8);
			}

			using var presalt = SafeArrayHandle.Create(16);
			TypeSerializer.Serialize(nonceA, presalt.Span.Slice(0, 8));
			TypeSerializer.Serialize(nonceB, presalt.Span.Slice(8, 8));

			using var salt = HashingUtils.HashSha3_512(presalt);
			using var password = HashingUtils.HashSha3_512(prePassword);

			ByteArray validatorSecretKeyBytes = null;

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), Math.Min(Math.Max((int) (nonce2 ^ accountIdLong & 0xFFFF), 1000), 10000), HashAlgorithmName.SHA512)) {

				validatorSecretKeyBytes = ByteArray.Wrap(rfc2898DeriveBytes.GetBytes(16));
			}

			// lets build the key code delta
			TypeSerializer.Deserialize(validatorSecretKeyBytes.Span, out Guid validatorSecretKeyCode);

			validatorSecretKeyBytes.Dispose();

			return validatorSecretKeyCode;
		}

		public static (long nonvr1, long nonce2, long nonce3, long nonce4) ExtractNonces(SafeArrayHandle stride) {

			TypeSerializer.Deserialize(stride.Span.Slice(0, sizeof(long)), out long nonce1);
			TypeSerializer.Deserialize(stride.Span.Slice(sizeof(long), sizeof(long)), out long nonce2);
			TypeSerializer.Deserialize(stride.Span.Slice(sizeof(long) * 2, sizeof(long)), out long nonce3);
			TypeSerializer.Deserialize(stride.Span.Slice(sizeof(long) * 3, sizeof(long)), out long nonce4);

			return (nonce1, nonce2, nonce3, nonce4);
		}

		/// <summary>
		/// transform a secret code to a derivative for the validator
		/// </summary>
		/// <param name="secretCode"></param>
		/// <param name="appointment"></param>
		/// <param name="appointmentKey"></param>
		/// <param name="accountId"></param>
		/// <param name="publicKeyHash2"></param>
		/// <param name="nonce1"></param>
		/// <param name="nonce2"></param>
		/// <param name="nonce3"></param>
		/// <param name="nonce4"></param>
		/// <returns></returns>
		public static int GenerateValidatorSecretCodeHash(int secretCode, DateTime appointment, SafeArrayHandle appointmentKey, AccountId accountId, long publicKeyHash2, SafeArrayHandle stride) {

			(long nonce1, long nonce2, long nonce3, long nonce4) = ExtractNonces(stride);

			long accountIdLong = accountId.ToLongRepresentation();
			long ticks = appointment.Ticks;

			nonce4 = Roll(nonce4, secretCode);

			using var hashBytes = SafeArrayHandle.Create(32);
			long adjustedSecretCode = (secretCode << (int) (nonce2 & 0x1F) ^ nonce4);
			TypeSerializer.Serialize(adjustedSecretCode, hashBytes.Span);

			using var prePassword = SafeArrayHandle.Create(32);

			using var buffer = SafeArrayHandle.Create(appointmentKey.Length + 16);
			appointmentKey.Entry.CopyTo(buffer.Entry);
			TypeSerializer.Serialize(publicKeyHash2 ^ ticks, buffer.Span.Slice(appointmentKey.Length, 8));
			TypeSerializer.Serialize(nonce4 ^ accountIdLong & ticks, buffer.Span.Slice(appointmentKey.Length + 8, 8));

			using SafeArrayHandle md5 = HashingUtils.GenerateMd5Hash(buffer);

			md5.Entry.CopyTo(prePassword.Entry);

			TypeSerializer.Serialize(nonce2 ^ ticks, prePassword.Span.Slice(16, 8));
			TypeSerializer.Serialize((accountIdLong ^ publicKeyHash2) >> (int) (publicKeyHash2 & 3), prePassword.Span.Slice(16 + 8, 8));

			using var preSalt = SafeArrayHandle.Create(16);
			TypeSerializer.Serialize(accountIdLong ^ nonce2 | nonce3, preSalt.Span.Slice(0, 8));
			TypeSerializer.Serialize(ticks ^ publicKeyHash2 | secretCode & nonce1, preSalt.Span.Slice(8, 8));

			using var salt = HashingUtils.HashSha3_512(preSalt);
			using var password = HashingUtils.HashSha3_512(prePassword);

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), Math.Min(Math.Max((int) (nonce4 ^ accountIdLong & 0xFFFF), 1000), 10000), HashAlgorithmName.SHA512)) {
				ByteArray.Wrap(rfc2898DeriveBytes.GetBytes(24)).CopyTo(hashBytes.Entry, 0, 8, 24);
			}

			return HashingUtils.XxHash32(hashBytes);
		}

		public static byte GetKeyOrdinal(AccountId accountId) {
			return GetKeyOrdinal(accountId.ToLongRepresentation());
		}

		public static int GetAppointmentKeyHash(SafeArrayHandle appointmentKey) {

			return HashingUtils.XxHash32(appointmentKey);
		}

		public static long GetAppointmentDateHash(DateTime appointment) {

			using SafeArrayHandle bytes = SafeArrayHandle.Create(8);
			TypeSerializer.Serialize(appointment.Ticks, bytes.Span);

			return HashingUtils.XxHash64(bytes);
		}

		public static long GetValidatorKeyHash(SafeArrayHandle key, DateTime appointment) {

			var appointmentDateHash = GetAppointmentDateHash(appointment);

			var keyHash = HashingUtils.XxHash64(key);

			using SafeArrayHandle hashBuffer = SafeArrayHandle.Create(sizeof(long));

			// the useful hash will help determine some randomness
			TypeSerializer.Serialize(appointmentDateHash ^ keyHash, hashBuffer.Span);

			return HashingUtils.XxHash64(hashBuffer);
		}

		public static byte GetKeyOrdinal(long accountId) {
			using var bytes = SafeArrayHandle.Create(8);

			TypeSerializer.Serialize(accountId, bytes.Span);
			int xxHash = HashingUtils.XxHash32(bytes);

			return (byte) ((xxHash & 1) + 1);
		}

		public static byte GetKeyOrdinal2(long accountId) {
			using var bytes = SafeArrayHandle.Create(8);

			TypeSerializer.Serialize(accountId, bytes.Span);
			int xxHash = HashingUtils.XxHash32(bytes);

			return (byte) ((xxHash & 0x800) + 1);
		}

		public static (SafeArrayHandle password, SafeArrayHandle salt) RebuildAppointmentApplicantSecretPackagePassword(Guid applicantAppointmentId, SafeArrayHandle validatorKeyCode) {

			var secretPackagePassword = SafeArrayHandle.Create(validatorKeyCode.Length + 16);

			using var buffer = SafeArrayHandle.Create(16);
			validatorKeyCode.Span.CopyTo(secretPackagePassword.Span);

			TypeSerializer.Serialize(applicantAppointmentId, buffer.Span);
			buffer.Span.CopyTo(secretPackagePassword.Span.Slice(validatorKeyCode.Length, 16));

			var salt = SafeArrayHandle.Create(8);

			long applicantSalt = HashingUtils.XxHash64(buffer);
			TypeSerializer.Serialize(applicantSalt, salt.Span);

			return (secretPackagePassword, salt);
		}

		public static (SafeArrayHandle password, SafeArrayHandle salt) RebuildAppointmentApplicantPackagePassword(Guid applicantAppointmentId, DateTime appointment, int appointmentKeyHash) {

			var password = SafeArrayHandle.Create(sizeof(long) + 16 + sizeof(int));

			using var buffer = SafeArrayHandle.Create(16);
			TypeSerializer.Serialize(applicantAppointmentId, buffer.Span);
			var applicantAppointmentIdxxHash = HashingUtils.XxHash64(buffer);

			int start1 = 0;
			int start2 = sizeof(long);

			// here we switch based on the hash
			if((applicantAppointmentIdxxHash & 1) == 0) {
				start1 = 16;
				start2 = 0;
			}

			buffer.Span.CopyTo(password.Span.Slice(start2, 16));

			var salt = SafeArrayHandle.Create(8);

			long applicantSalt = HashingUtils.XxHash64(buffer);
			TypeSerializer.Serialize(applicantSalt, salt.Span);

			var appointmentDateHash = GetAppointmentDateHash(appointment);
			TypeSerializer.Serialize(appointmentDateHash, buffer.Span.Slice(0, sizeof(long)));

			buffer.Span.Slice(0, sizeof(long)).CopyTo(password.Span.Slice(start1, sizeof(long)));

			TypeSerializer.Serialize(appointmentKeyHash, buffer.Span.Slice(0, sizeof(int)));

			buffer.Span.Slice(0, sizeof(int)).CopyTo(password.Span.Slice(sizeof(long) + 16, sizeof(int)));

			return (password, salt);
		}

		public static (SafeArrayHandle password, SafeArrayHandle salt) RebuildPuzzlePackagePassword(SafeArrayHandle appointmentKey) {

			var puzzlePassword = SafeArrayHandle.Create(PUZZLE_KEY_SIZE);
			var puzzleSalt = SafeArrayHandle.Create(PUZZLE_SALT_SIZE);

			appointmentKey.Entry.CopyTo(puzzlePassword.Entry, 0, 0, puzzlePassword.Length);
			appointmentKey.Entry.CopyTo(puzzleSalt.Entry, PUZZLE_KEY_SIZE, 0, puzzleSalt.Length);

			return (puzzlePassword, puzzleSalt);
		}

		public static SafeArrayHandle Decrypt(SafeArrayHandle encrypted, SafeArrayHandle password, SafeArrayHandle salt) {

			return CryptoUtil.Decrypt(encrypted, password, salt);
		}

		public static SafeArrayHandle PrepareValidatorMessageLogics(DateTime appointment, SafeArrayHandle message, SafeArrayHandle stride, SafeArrayHandle validatorSecretKey, Func<XchachaEncryptor, SafeArrayHandle, SafeArrayHandle, SafeArrayHandle, int, SafeArrayHandle> action) {

			int strideStart = 0;
			int strideLength = Constants.HALF_STRIDE_LENGTH;

			int secretKeyStart = strideLength;
			int secretKeyLength = validatorSecretKey.Length;

			int dateStart = strideStart + secretKeyStart;
			int dateLength = sizeof(long);

			using var buffer = SafeArrayHandle.Create(Constants.HALF_STRIDE_LENGTH + secretKeyLength + dateLength);
			var span = buffer.Span;

			if((validatorSecretKey[3] & 0x4) == 1) {
				secretKeyStart = 0;
				strideStart = secretKeyLength;
			}

			if((stride[3] & 0x4) == 1) {
				dateStart = 0;
				strideStart = dateLength;
				secretKeyStart = dateLength + strideLength;
			}

			if((stride[9] & 0x8) == 1) {
				dateStart = 0;
				secretKeyStart = dateLength;
				strideStart = dateLength + secretKeyLength;
			}

			stride.Span.Slice(strideLength, strideLength).CopyTo(span.Slice(strideStart, strideLength));
			validatorSecretKey.CopyTo(span.Slice(secretKeyStart, secretKeyLength));
			TypeSerializer.Serialize(appointment.Ticks, span.Slice(dateStart, dateLength));

			using var password = (SafeArrayHandle) buffer.Entry.Slice(0, buffer.Length - CODE_SALT_LENGTH);
			using var salt = (SafeArrayHandle) buffer.Entry.Slice(buffer.Length - CODE_SALT_LENGTH, CODE_SALT_LENGTH);

			TypeSerializer.Deserialize(password.Span.Slice(0, sizeof(int)), out int iterations);

			using SafeArrayHandle code = SafeArrayHandle.Create(CODE_LENGTH);

			using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArray(), salt.ToExactByteArrayCopy(), AppointmentUtils.GetIterations(iterations, 3000, 10_000), HashAlgorithmName.SHA512)) {
				SafeArrayHandle.Wrap(rfc2898DeriveBytes.GetBytes(code.Length)).CopyTo(code);
			}

			using var password2 = (SafeArrayHandle) code.Entry.Slice(0, CODE_PASSWORD_LENGTH);
			using var salt2 = (SafeArrayHandle) code.Entry.Slice(CODE_PASSWORD_LENGTH, CODE_SALT_LENGTH);

			TypeSerializer.Deserialize(password2.Span.Slice(0, sizeof(int)), out int iterations2);

			using XchachaEncryptor xchacha = new XchachaEncryptor();

			return action(xchacha, message, password2, salt2, GetIterations(iterations2, 3000, 10_000));
		}

		public static SafeArrayHandle DecryptValidatorMessage(DateTime appointment, SafeArrayHandle cypher, SafeArrayHandle stride, SafeArrayHandle validatorSecretKey) {

			return PrepareValidatorMessageLogics(appointment, cypher, stride, validatorSecretKey, (xchacha, m, p, s, i) => xchacha.Decrypt(m, p, s, i));
		}

		public static int GetIterations(int value, int min, int max) {
			return CryptoUtil.GetIterations(value, min, max);
		}

		public static SafeArrayHandle PrepareTHSHash(SafeArrayHandle appointmentKey, List<int> puzzleAnswers) {
			var buffer = SafeArrayHandle.Create(appointmentKey.Length + sizeof(long));

			appointmentKey.Entry.CopyTo(buffer.Entry);

			using var answersBuffer = SafeArrayHandle.Create(sizeof(int) * puzzleAnswers.Count);

			for(int i = 0; i < puzzleAnswers.Count; i++) {

				TypeSerializer.Serialize(puzzleAnswers[i], answersBuffer.Span.Slice(sizeof(int) * i, sizeof(int)));
			}

			var answersHash = HashingUtils.XxHash64(answersBuffer);
			TypeSerializer.Serialize(answersHash, buffer.Span.Slice(appointmentKey.Length, sizeof(long)));

			var hash64 = HashingUtils.XxHash64(buffer);

			var result = SafeArrayHandle.Create(sizeof(long));
			TypeSerializer.Serialize(hash64, result.Span);

			return result;
		}

		public static Guid CreateRequesterId(IWalletAccount account) {

			return CreateRequesterId(account.GetAccountId());
		}

		public static Guid CreateRequesterId(AccountId accountId) {
			return new TransactionId(accountId, 0).ToGuid();
		}

		public static async Task<bool> VerifyPuzzleTHS(SafeArrayHandle appointmentKey, List<int> puzzleAnswers, THSSolutionSet solution) {

			THSEngine thsEngine = null;

			try {
				if(TestingUtil.Testing) {
					thsEngine = new THSEngine(THSRulesSet.TestRuleset, THSRulesSet.TestRulesetDescriptor, Enums.THSMemoryTypes.RAM);
				} else {
					thsEngine = new THSEngine(THSRulesSet.PuzzleDefaultRuleset, THSRulesSet.PuzzleDefaultRulesetDescriptor, Enums.THSMemoryTypes.RAM);
				}

				await thsEngine.Initialize(THSEngine.THSModes.Verify).ConfigureAwait(false);

				using var thsHash = PrepareTHSHash(appointmentKey, puzzleAnswers);

				return await thsEngine.Verify(thsHash, solution).ConfigureAwait(false);
			} finally {
				thsEngine?.Dispose();
			}
		}

		public static Dictionary<int, (int secretCodeL2, long index)[]> RehydrateAssignedSecretCodes(SafeArrayHandle secretCodes) {

			if(secretCodes == null || secretCodes.IsZero) {
				return new Dictionary<int, (int secretCodeL2, long index)[]>();
			}

			using var rehydrator = DataSerializationFactory.CreateRehydrator(secretCodes);
			AdaptiveLong1_9 tools = new AdaptiveLong1_9();

			var results = new Dictionary<int, (int secretCodeL2, long index)[]>();

			tools.Rehydrate(rehydrator);
			int count = (int) tools.Value;
			
			// first the singles
			for(int i = 0; i < count; i++) {
				int key = rehydrator.ReadInt();
				
				int secretCodeL2 = rehydrator.ReadInt();

				tools.Rehydrate(rehydrator);
				long index = (long)tools.Value;

				(int secretCodeL2, long index)[] array = new (int secretCodeL2, long index)[]{(secretCodeL2, index)};
				if(!results.ContainsKey(key)) {
					results.Add(key, array);
				}
			}
			
			// multiples
			tools.Rehydrate(rehydrator);
			count = (int) tools.Value;

			for(int i = 0; i < count; i++) {
				int key = rehydrator.ReadInt();
				
				tools.Rehydrate(rehydrator);
				int subCount = (int) tools.Value;

				(int secretCodeL2, long index)[] array = new (int secretCodeL2, long index)[subCount];
				for(int j = 0; j < subCount; j++) {
					int secretCodeL2 = rehydrator.ReadInt();

					tools.Rehydrate(rehydrator);
					long index = tools.Value;

					array[j] = (secretCodeL2, index);
				}

				if(!results.ContainsKey(key)) {
					results.Add(key, array);
				}
			}
			
			return results;
		}

		public static SafeArrayHandle DehydrateAssignedSecretCodes(Dictionary<int, (int secretCodeL2, long index)[]> secretCodes) {

			using var dehydrator = DataSerializationFactory.CreateDehydrator();
			AdaptiveLong1_9 tools = new AdaptiveLong1_9();

			// first the singles
			var singles = secretCodes.Where(e => e.Value.Length == 1);
			tools.Value = singles.Count();
			tools.Dehydrate(dehydrator);

			foreach(var entry in singles) {
				dehydrator.Write(entry.Key);

				dehydrator.Write(entry.Value[0].secretCodeL2);

				tools.Value = (uint)entry.Value[0].index;
				tools.Dehydrate(dehydrator);
			}

			var multiples = secretCodes.Where(e => e.Value.Length > 1);
			tools.Value = multiples.Count();
			tools.Dehydrate(dehydrator);

			foreach(var entry in multiples) {
				dehydrator.Write(entry.Key);

				tools.Value = entry.Value.Length;
				tools.Dehydrate(dehydrator);

				foreach(var entry2 in entry.Value) {
					dehydrator.Write(entry2.secretCodeL2);

					tools.Value = entry2.index;
					tools.Dehydrate(dehydrator);
				}
			}

			return dehydrator.ToArray();
		}

		public static HashSet<int> RehydrateAssignedIndices(SafeArrayHandle indices) {
			if(indices == null || indices.IsZero) {
				return new HashSet<int>();
			}

			using var rehydrator = DataSerializationFactory.CreateRehydrator(indices);
			AdaptiveLong1_9 tools = new AdaptiveLong1_9();

			tools.Rehydrate(rehydrator);
			int count = (int) tools.Value;

			HashSet<int> results = new HashSet<int>();

			for(int i = 0; i < count; i++) {
				tools.Rehydrate(rehydrator);
				results.Add((int) tools.Value);
			}

			return results;
		}

		public static SafeArrayHandle DehydrateAssignedIndices(List<int> indices) {

			using var dehydrator = DataSerializationFactory.CreateDehydrator();
			AdaptiveLong1_9 tools = new AdaptiveLong1_9();

			tools.Value = indices.Count();
			tools.Dehydrate(dehydrator);

			foreach(var entry in indices) {
				tools.Value = entry;
				tools.Dehydrate(dehydrator);
			}

			return dehydrator.ToArray();
		}

		public static DateTime ComputeDispatchDelay(DateTime appointment, int validatorWindow) {
			return appointment.AddMinutes(GlobalRandom.GetNext(10, Math.Max((validatorWindow - 30)+1, 30+1)));
		}

		public static bool AppointmentWorkflowExpired(IWalletAccount account) {
			
			if(AppointmentExpired(account)) {
				return true;
			}
			
			// we sent a request, and never received a response
			if(AppointmentRequestExpired(account)) {
				return true;
			}
			// context received but puzzle never completed in time.
			if(AppointmentPuzzleWindowExpired(account)) {
				return true;
			}
			
			// puzzle completed, but we never received any verification results
			if(AppointmentPuzzleVerificationWindowExpired(account)) {
				return true;
			}
			
			if(AppointmentConfirmationCodeWindowExpired(account)) {
				return true;
			}
			
			return false;
		}

		/// <summary>
		/// check if an expiration is seriously reset
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentExpired(IWalletAccount account) {
			return account.AccountAppointment == null || account.AccountAppointment.AppointmentStatus == Enums.AppointmentStatus.None || (account.AccountAppointment.AppointmentStatus != Enums.AppointmentStatus.AppointmentRequested && !account.AccountAppointment.AppointmentTime.HasValue);
		}

		/// <summary>
		/// lets determine if a requested appointment has expired
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentRequestExpired(IWalletAccount account) {
			if(AppointmentExpired(account)) {
				return true;
			}
			
			if(account.AccountAppointment.AppointmentStatus <= Enums.AppointmentStatus.AppointmentRequested) {
				var requestTimeStamp = account.AccountAppointment.AppointmentRequestTimeStamp;
				
				if(!requestTimeStamp.HasValue || (requestTimeStamp.Value.AddDays(1) < DateTimeEx.CurrentTime)) {
					return true;
				}
			}

			return false;
		}
		
		/// <summary>
		/// lets determine if the puzzle window has expired and not completed
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentPuzzleWindowExpired(IWalletAccount account) {
			if(AppointmentExpired(account)) {
				return true;
			}

			var status = account.AccountAppointment.AppointmentStatus;
			if(status == Enums.AppointmentStatus.AppointmentSet || status == Enums.AppointmentStatus.AppointmentContextCached) {
				
				var requestTimeStamp = account.AccountAppointment.AppointmentTime;
				if(!requestTimeStamp.HasValue || requestTimeStamp.Value.AddHours(1) < DateTimeEx.CurrentTime) {
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// verify if the completed puzzle verification window during an appointment has expired rendering the appointment void
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentPuzzleVerificationWindowExpired(IWalletAccount account) {
			if(AppointmentExpired(account)) {
				return true;
			}
			
			if(account.AccountAppointment.AppointmentStatus >= Enums.AppointmentStatus.AppointmentPuzzleCompleted) {
				var verificationTime = account.AccountAppointment.AppointmentVerificationTime;
				if(!verificationTime.HasValue || verificationTime.Value.AddDays(2) < DateTimeEx.CurrentTime) {
					return true;
				}
			}

			return false;
		}
		
		public static bool AppointmentConfirmationCodeWindowExpired(IWalletAccount account) {
			if(AppointmentExpired(account)) {
				return true;
			}

			if(account.AccountAppointment.AppointmentStatus >= Enums.AppointmentStatus.AppointmentCompleted) {
				
				var confirmationCode = account.AccountAppointment.AppointmentConfirmationCode;
				var confirmationCodeExpiration = account.AccountAppointment.AppointmentConfirmationCodeExpiration;
				if(!confirmationCode.HasValue || !confirmationCodeExpiration.HasValue || confirmationCodeExpiration.Value < DateTimeEx.CurrentTime) {
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Determine if the appointment verification has completely expired 
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentVerificationExpired(IWalletAccount account) {
	
			if(account.VerificationLevel == Enums.AccountVerificationTypes.Appointment) {

				if(account.VerificationExpirationDate.HasValue) {
					return account.VerificationExpirationDate.Value < DateTimeEx.CurrentTime;
				}
			}
			
			return true;
		}

		public static DateTime? AppointmentVerificationExpiringDate(IWalletAccount account) {
			if(!account.VerificationExpirationDate.HasValue) {
				return null;
			}
			
			return account.VerificationExpirationDate.Value - TimeSpan.FromDays(7);
		}

		/// <summary>
		/// determine if the appointment verification is soon to expire
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public static bool AppointmentVerificationExpiring(IWalletAccount account) {

			if(AppointmentVerificationExpired(account)) {
				return true;
			}
			if(!account.VerificationExpirationDate.HasValue) {
				return true;
			}

			if(account.VerificationLevel == Enums.AccountVerificationTypes.Appointment) {
				return AppointmentVerificationExpiringDate(account) < DateTimeEx.CurrentTime;
			}

			return false;
		}

		public static bool ResetAppointment(IWalletAccount account) {

			if(account == null) {
				return false;
			}

			bool expired = AppointmentVerificationExpired(account);
			
			account.AccountAppointment = null;
			
			if(expired) {
				account.VerificationLevel = Enums.AccountVerificationTypes.None;
				account.VerificationExpirationDate = null;
				account.VerificationDate = null;
				account.VerificationData = null;
			}
			
			return true;
		}
	}
}