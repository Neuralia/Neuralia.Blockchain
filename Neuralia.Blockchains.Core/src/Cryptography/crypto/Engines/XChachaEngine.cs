using System;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto.Engines;

namespace Neuralia.Blockchains.Core.Cryptography.crypto.Engines {
	public class XChachaEngine : XSalsa20Engine {

		/// <summary>
		///     Creates a 20 rounds XChaCha engine overriden so we can extend it
		/// </summary>
		public XChachaEngine() {
		}

		public XChachaEngine(int rounds) {
			if((rounds <= 0) || ((rounds & 1) != 0)) {
				throw new ArgumentException("xchacha rounds must be a positive and even number");
			}

			this.rounds = rounds;
		}

		public override string AlgorithmName => "XChaCha" + this.rounds;

		protected override void SetKey(byte[] keyBytes, byte[] ivBytes) {
			if(keyBytes == null) {
				throw new ArgumentException(this.AlgorithmName + " doesn't support re-initialize with a null key");
			}

			if(keyBytes.Length != 32) {
				throw new ArgumentException(this.AlgorithmName + " requires a 256 bit key");
			}

			base.SetKey(keyBytes, ivBytes);

			// ADD the next 64 bits of IV into enginestate instead of the counter variable
			this.PreprareBytes(ivBytes, 8, this.engineState, 8, 2);

			uint[] hsalsa20OutBytes = new uint[this.engineState.Length];
			SalsaCore(this.rounds, this.engineState, hsalsa20OutBytes);

			// Set new key, removing addition in last round of salsaCore
			this.engineState[1] = hsalsa20OutBytes[0] - this.engineState[0];
			this.engineState[2] = hsalsa20OutBytes[5] - this.engineState[5];
			this.engineState[3] = hsalsa20OutBytes[10] - this.engineState[10];
			this.engineState[4] = hsalsa20OutBytes[15] - this.engineState[15];

			this.engineState[11] = hsalsa20OutBytes[6] - this.engineState[6];
			this.engineState[12] = hsalsa20OutBytes[7] - this.engineState[7];
			this.engineState[13] = hsalsa20OutBytes[8] - this.engineState[8];
			this.engineState[14] = hsalsa20OutBytes[9] - this.engineState[9];

			// the last 64 bits of the  IV
			this.PreprareBytes(ivBytes, 16, this.engineState, 6, 2);
		}

		private void PreprareBytes(byte[] input, int srcOffset, in uint[] destBuffer, int destOffset, int loops) {
			for(int i = 0; i < loops; ++i) {
				TypeSerializer.Deserialize(input.AsSpan().Slice(srcOffset, sizeof(int)), out destBuffer[destOffset + i]);
				srcOffset += sizeof(int);
			}
		}
	}
}