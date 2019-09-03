using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Neuralia.Blockchains.Core.Cryptography.crypto.Engines;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

//TODO: refactor this class so that it looks different, different from java bouncycastle source
namespace Neuralia.Blockchains.Core.Cryptography.Encryption.Symetrical {
	public class XChaChaFileEncryptor {
		public enum ChachaRounds : byte {
			XCHACHA_20 = 1,
			XCHACHA_40 = 2
		}

		protected readonly EncryptorParameters parameters;

		protected readonly XChachaEngine xchachaCipher;

		public XChaChaFileEncryptor(EncryptorParameters parameters) {

			this.parameters = parameters;

			int rounds = 40;

			if(parameters.cipher == EncryptorParameters.SymetricCiphers.XCHACHA_20) {
				rounds = 20;
			}

			this.xchachaCipher = new XChachaEngine(rounds);
		}

		private KeyParameter InitRecordMAC(XChachaEngine cipher, bool forEncryption, IByteArray password) {
			//its not ideal i know, but we have no choice for now. no way to pass a secure string to the encryptor
			//TODO: can this be made safer by clearing the password?

			cipher.Reset();

			try {
				using(Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password.ToExactByteArrayCopy(), this.parameters.salt, this.parameters.iterations)) {

					ByteArray Key = rfc2898DeriveBytes.GetBytes(256 / 8);
					ByteArray IV = rfc2898DeriveBytes.GetBytes(192 / 8);

					cipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(Key.ToExactByteArrayCopy()), IV.ToExactByteArrayCopy()));
				}
			} finally {
				// hopefully this will clear the password from memory (we hope but most probably wont)
				GC.Collect();
			}

			IByteArray startingBlock = new ByteArray(64);
			cipher.ProcessBytes(startingBlock.Bytes, startingBlock.Offset, startingBlock.Length, startingBlock.Bytes, startingBlock.Offset);

			// NOTE: The BC implementation puts 'r' after 'k'
			Buffer.BlockCopy(startingBlock.Bytes, startingBlock.Offset, startingBlock.Bytes, 32, 16);

			KeyParameter macKey = new KeyParameter(startingBlock.ToExactByteArrayCopy(), 16, 32);
			Poly1305KeyGenerator.Clamp(macKey.GetKey());

			return macKey;
		}

		public static EncryptorParameters GenerateEncryptionParameters(int saltLength = 500, ChachaRounds rounds = ChachaRounds.XCHACHA_40) {

			SecureRandom rnd = new SecureRandom();

			ByteArray salt = new ByteArray(saltLength);

			// get a random salt
			salt.FillSafeRandom();

			EncryptorParameters.SymetricCiphers cipherType = EncryptorParameters.SymetricCiphers.XCHACHA_40;

			if(rounds == ChachaRounds.XCHACHA_20) {
				cipherType = EncryptorParameters.SymetricCiphers.XCHACHA_20;
			}

			return new EncryptorParameters {cipher = cipherType, salt = salt.ToExactByteArrayCopy(), iterations = rnd.Next(1000, short.MaxValue), keyBitLength = 256};
		}

		public IByteArray Encrypt(IByteArray plain, SecureString password) {

			IByteArray passwordBtyes = (ByteArray) Encoding.UTF8.GetBytes(password.ConvertToUnsecureString());

			return this.Encrypt(plain, passwordBtyes);
		}

		public IByteArray Encrypt(IByteArray plain, IByteArray password) {

			int ciphertextLength = plain.Length + 16; // 16 is the size of the HMAC

			KeyParameter macKey = this.InitRecordMAC(this.xchachaCipher, true, password);

			ByteArray output = new ByteArray(ciphertextLength);
			this.xchachaCipher.ProcessBytes(plain.Bytes, plain.Offset, plain.Length, output.Bytes, output.Offset);

			IByteArray additionalData = this.getAdditionalData();
			IByteArray mac = this.calculateRecordMAC(macKey, additionalData, output, 0, plain.Length);

			mac.CopyTo(output, plain.Length);

			return output;

		}

		public IByteArray Decrypt(IByteArray cipher, SecureString password) {
			try {
				IByteArray passwordBtyes = (ByteArray) Encoding.UTF8.GetBytes(password.ConvertToUnsecureString());

				return this.Decrypt(cipher, 0, cipher.Length, passwordBtyes);

			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public IByteArray Decrypt(IByteArray cipher, IByteArray password) {
			try {
				return this.Decrypt(cipher, 0, cipher.Length, password);
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		public IByteArray Decrypt(IByteArray ciphertext, int offset, int length, IByteArray password) {

			try {
				int lengthDelta = length - offset;
				int plaintextLength = lengthDelta - 16; // the size of the HMAC is 16

				if(this.getPlaintextLimit(lengthDelta) < 0) {
					throw new DataEncryptionException("Decoding error");
				}

				IByteArray receivedMAC = MemoryAllocators.Instance.allocator.Take((offset + lengthDelta) - (offset + plaintextLength));

				ciphertext.CopyTo(receivedMAC, offset + plaintextLength, 0, receivedMAC.Length);

				KeyParameter macKey = this.InitRecordMAC(this.xchachaCipher, false, password);

				IByteArray additionalData = this.getAdditionalData();
				IByteArray calculatedMAC = this.calculateRecordMAC(macKey, additionalData, ciphertext, offset, plaintextLength);

				if(!calculatedMAC.Equals(receivedMAC)) {
					throw new DataEncryptionException("Bad record MAC");
				}

				receivedMAC.Return();

				IByteArray output = new ByteArray(plaintextLength);
				this.xchachaCipher.ProcessBytes(ciphertext.Bytes, ciphertext.Offset + offset, plaintextLength, output.Bytes, output.Offset);

				return output;
			} catch(DataEncryptionException ex) {
				throw;
			} catch(Exception ex) {
				throw new DataEncryptionException("", ex);
			}
		}

		protected int getPlaintextLimit(int ciphertextLimit) {
			return ciphertextLimit;
		}

		protected IByteArray calculateRecordMAC(KeyParameter macKey, IByteArray additionalData, IByteArray buf, int off, int len) {
			IMac mac = new Poly1305();
			mac.Init(macKey);

			this.updateRecordMAC(mac, additionalData, 0, additionalData.Length);
			this.updateRecordMAC(mac, buf, off, len);

			IByteArray output = new ByteArray(mac.GetMacSize());
			mac.DoFinal(output.Bytes, output.Offset);

			return output;
		}

		protected void updateRecordMAC(IMac mac, IByteArray buf, int off, int len) {
			mac.BlockUpdate(buf.Bytes, buf.Offset + off, len);

			Span<byte> buffer = stackalloc byte[sizeof(long)];
			TypeSerializer.Serialize((ulong) len & 0xFFFFFFFFUL, buffer);

			mac.BlockUpdate(buffer.ToArray(), 0, buffer.Length);
		}

		protected IByteArray getAdditionalData() {
			/*
			 * additional_data = seq_num + TLSCompressed.type + TLSCompressed.version +
			 * TLSCompressed.length
			 */
			IByteArray additional_data = new ByteArray(0);

			//		TlsUtils.writeUint64(seqNo, additional_data, 0);
			//		TlsUtils.writeUint8(type, additional_data, 8);
			//		TlsUtils.writeVersion(context.getServerVersion(), additional_data, 9);
			//		TlsUtils.writeUint16(len, additional_data, 11);

			return additional_data;
		}
	}
}