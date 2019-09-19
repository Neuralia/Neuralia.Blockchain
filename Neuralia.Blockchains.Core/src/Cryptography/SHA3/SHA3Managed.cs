using System;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.SHA3 {
	/// <summary>
	///     Custom implementation of SHA3
	/// </summary>
	public abstract class SHA3Managed : SHA3 {

		private const int KECCAK_NUMBER_OF_ROUNDS = 0x18;
		private const int KECCAK_LANE_SIZE_IN_BITS = 8 << 3;

		private static readonly ulong[] RoundConstants = {0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL, 0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL, 0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL, 0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL, 0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL, 0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL};

		private readonly SafeArrayHandle buffer = SafeArrayHandle.Create();

		private int bufferLength;
		private int buffLength;
		private byte[] obligatoryHashResult;
		private readonly SafeArrayHandle state = SafeArrayHandle.Create();

		internal SHA3Managed(int hashBitLength) {

			this.HashSize = hashBitLength;

			switch(hashBitLength) {
				case 256:
					this.KeccakCode = 1088;

					break;

				case 512:
					this.KeccakCode = 576;

					break;
				default:
					throw new ArgumentException("hashBitLength must be either 256 or 512", nameof(hashBitLength));
			}

			this.Initialize();
		}

		internal int KeccakCode { get; set; }

		public override int HashSize { get; }

		protected int SizeInBytes => this.KeccakCode >> 3;

		protected int HashByteLength => this.HashSize >> 3;

		/// <summary>
		///     the results of the last hashing.
		/// </summary>
		/// <remarks>We do NOT clear this memory. the caller owns it and is responsible for clearing it. not thread safe!</remarks>
		public ByteArray LastResult { get; private set; }

		public override void Initialize() {
			
			if(this.buffer.Length != this.SizeInBytes) {
				this.buffer.Entry = null;
			}

			if(this.buffer?.IsNull??true) {
				this.buffer.Entry = ByteArray.Create(this.SizeInBytes);
			}

			
			this.bufferLength = this.SizeInBytes;
			this.buffLength = 0;

			if(this.state.IsEmpty) {
				this.state.Entry = ByteArray.Create<ulong>(5 * 5);
			} else {
				this.state.Entry.Clear();
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if(disposing && !this.IsDisposed) {
				this.buffer.Dispose();
				this.state.Dispose();
			}

			this.IsDisposed = true;
		}

		private void AddToBuffer(byte[] array, ref int offset, ref int count) {
			int amount = Math.Min(count, this.bufferLength - this.buffLength);

			this.buffer.Entry.CopyFrom((ReadOnlySpan<byte>) array, offset, this.buffLength, amount);

			//Buffer.BlockCopy(array, offset, this.buffer.Bytes, this.buffLength, amount);
			offset += amount;
			this.buffLength += amount;
			count -= amount;
		}

		/// <summary>
		///     This version allows us to avoid a useless byte[] allocation
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public ByteArray CustomComputeHash(ByteArray buffer) {
			return this.CustomComputeHash(buffer, 0, buffer.Length);
		}

		public ByteArray CustomComputeHash(ByteArray buffer, int offset, int length) {
			this.HashCore(buffer.Bytes, buffer.Offset + offset, length);

			return this.CustomCaptureHashCodeAndReinitialize();
		}

		private ByteArray CustomCaptureHashCodeAndReinitialize() {

			this.CustomHashFinal();
			this.Initialize();

			var result = this.LastResult;

			this.LastResult = null;
			
			return result;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize) {
			base.HashCore(array, ibStart, cbSize);

			if(cbSize == 0) {
				return;
			}

			int chunk = this.SizeInBytes >> 3;

			using(SafeArrayHandle utempBlock = ByteArray.Create<ulong>(chunk)) {
				var utemps = utempBlock.Entry.CastedArray<ulong>();
				
				if(this.buffLength == this.SizeInBytes) {
					throw new InvalidOperationException("The buffer has no space.");
				}

				this.AddToBuffer(array, ref ibStart, ref cbSize);

				//buffer is full
				if(this.buffLength == this.SizeInBytes) {
					this.buffer.Entry.CopyTo(utempBlock.Entry, 0, 0, this.SizeInBytes);

					this.Keccak(utemps, chunk);
					this.buffLength = 0;
				}

				for(; cbSize >= this.SizeInBytes; cbSize -= this.SizeInBytes, ibStart += this.SizeInBytes) {

					utempBlock.Entry.CopyFrom(ref array, ibStart, 0, this.SizeInBytes);
					this.Keccak(utemps, chunk);
				}

				if(cbSize > 0) //some left over
				{
					this.buffer.Entry.CopyFrom((ReadOnlySpan<byte>) array, ibStart, this.buffLength, cbSize);

					this.buffLength += cbSize;
				}
			}
		}

		private void CustomHashFinal() {
			//    padding
			this.buffer.Entry.Clear(this.buffLength, this.SizeInBytes - this.buffLength);

			if(this.UseKeccakPadding) {
				this.buffer[this.buffLength++] = 1;
			} else {
				this.buffer[this.buffLength++] = 6;
			}

			this.buffer[this.SizeInBytes - 1] |= 0x80;
			int chunk = this.SizeInBytes >> 3;

			using(SafeArrayHandle utempBlock = ByteArray.Create<ulong>(chunk)) {

				var utemps = utempBlock.Entry.CastedArray<ulong>();

				// result will be return and cleared by the callers. we are not responsible for it
				this.LastResult = ByteArray.Create(this.HashByteLength);
				
				this.buffer.Entry.CopyTo(utempBlock.Entry, 0, 0, this.SizeInBytes);
				this.Keccak(utemps, chunk);
				this.state.Entry.CopyTo(this.LastResult, 0, 0, this.HashByteLength);
			}
		}
		
		protected override byte[] HashFinal() {

			this.CustomHashFinal();

			// no choice, the hash structure requires this
			if(this.obligatoryHashResult == null) {
				this.obligatoryHashResult = new byte[this.HashByteLength];
			}

			// we have no choice to return an array here. the hashing structure demands it and we dont control it.
			this.LastResult.CopyTo(this.obligatoryHashResult, 0, 0, this.HashByteLength);

			return this.obligatoryHashResult;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ulong ROL(ulong a, int offset) {
			return (a << (offset % KECCAK_LANE_SIZE_IN_BITS)) ^ (a >> (KECCAK_LANE_SIZE_IN_BITS - (offset % KECCAK_LANE_SIZE_IN_BITS)));
		}

		private void Keccak(in Span<ulong> inb, int laneCount) {
			var stateBuffer = this.state.Entry.CastedArray<ulong>();

			while(--laneCount >= 0) {
				stateBuffer[laneCount] ^= inb[laneCount];
			}

			ulong Aba, Abe, Abi, Abo, Abu;
			ulong Aga, Age, Agi, Ago, Agu;
			ulong Aka, Ake, Aki, Ako, Aku;
			ulong Ama, Ame, Ami, Amo, Amu;
			ulong Asa, Ase, Asi, Aso, Asu;
			ulong BCa, BCe, BCi, BCo, BCu;
			ulong Da, De, Di, Do, Du;
			ulong Eba, Ebe, Ebi, Ebo, Ebu;
			ulong Ega, Ege, Egi, Ego, Egu;
			ulong Eka, Eke, Eki, Eko, Eku;
			ulong Ema, Eme, Emi, Emo, Emu;
			ulong Esa, Ese, Esi, Eso, Esu;
			int round = laneCount;

			Aba = stateBuffer[0];
			Abe = stateBuffer[1];
			Abi = stateBuffer[2];
			Abo = stateBuffer[3];
			Abu = stateBuffer[4];
			Aga = stateBuffer[5];
			Age = stateBuffer[6];
			Agi = stateBuffer[7];
			Ago = stateBuffer[8];
			Agu = stateBuffer[9];
			Aka = stateBuffer[10];
			Ake = stateBuffer[11];
			Aki = stateBuffer[12];
			Ako = stateBuffer[13];
			Aku = stateBuffer[14];
			Ama = stateBuffer[15];
			Ame = stateBuffer[16];
			Ami = stateBuffer[17];
			Amo = stateBuffer[18];
			Amu = stateBuffer[19];
			Asa = stateBuffer[20];
			Ase = stateBuffer[21];
			Asi = stateBuffer[22];
			Aso = stateBuffer[23];
			Asu = stateBuffer[24];

			for(round = 0; round < KECCAK_NUMBER_OF_ROUNDS; round += 2) {
				BCa = Aba ^ Aga ^ Aka ^ Ama ^ Asa;
				BCe = Abe ^ Age ^ Ake ^ Ame ^ Ase;
				BCi = Abi ^ Agi ^ Aki ^ Ami ^ Asi;
				BCo = Abo ^ Ago ^ Ako ^ Amo ^ Aso;
				BCu = Abu ^ Agu ^ Aku ^ Amu ^ Asu;

				Da = BCu ^ this.ROL(BCe, 1);
				De = BCa ^ this.ROL(BCi, 1);
				Di = BCe ^ this.ROL(BCo, 1);
				Do = BCi ^ this.ROL(BCu, 1);
				Du = BCo ^ this.ROL(BCa, 1);

				Aba ^= Da;
				BCa = Aba;
				Age ^= De;
				BCe = this.ROL(Age, 44);
				Aki ^= Di;
				BCi = this.ROL(Aki, 43);
				Amo ^= Do;
				BCo = this.ROL(Amo, 21);
				Asu ^= Du;
				BCu = this.ROL(Asu, 14);
				Eba = BCa ^ (~BCe & BCi);
				Eba ^= RoundConstants[round];
				Ebe = BCe ^ (~BCi & BCo);
				Ebi = BCi ^ (~BCo & BCu);
				Ebo = BCo ^ (~BCu & BCa);
				Ebu = BCu ^ (~BCa & BCe);

				Abo ^= Do;
				BCa = this.ROL(Abo, 28);
				Agu ^= Du;
				BCe = this.ROL(Agu, 20);
				Aka ^= Da;
				BCi = this.ROL(Aka, 3);
				Ame ^= De;
				BCo = this.ROL(Ame, 45);
				Asi ^= Di;
				BCu = this.ROL(Asi, 61);
				Ega = BCa ^ (~BCe & BCi);
				Ege = BCe ^ (~BCi & BCo);
				Egi = BCi ^ (~BCo & BCu);
				Ego = BCo ^ (~BCu & BCa);
				Egu = BCu ^ (~BCa & BCe);

				Abe ^= De;
				BCa = this.ROL(Abe, 1);
				Agi ^= Di;
				BCe = this.ROL(Agi, 6);
				Ako ^= Do;
				BCi = this.ROL(Ako, 25);
				Amu ^= Du;
				BCo = this.ROL(Amu, 8);
				Asa ^= Da;
				BCu = this.ROL(Asa, 18);
				Eka = BCa ^ (~BCe & BCi);
				Eke = BCe ^ (~BCi & BCo);
				Eki = BCi ^ (~BCo & BCu);
				Eko = BCo ^ (~BCu & BCa);
				Eku = BCu ^ (~BCa & BCe);

				Abu ^= Du;
				BCa = this.ROL(Abu, 27);
				Aga ^= Da;
				BCe = this.ROL(Aga, 36);
				Ake ^= De;
				BCi = this.ROL(Ake, 10);
				Ami ^= Di;
				BCo = this.ROL(Ami, 15);
				Aso ^= Do;
				BCu = this.ROL(Aso, 56);
				Ema = BCa ^ (~BCe & BCi);
				Eme = BCe ^ (~BCi & BCo);
				Emi = BCi ^ (~BCo & BCu);
				Emo = BCo ^ (~BCu & BCa);
				Emu = BCu ^ (~BCa & BCe);

				Abi ^= Di;
				BCa = this.ROL(Abi, 62);
				Ago ^= Do;
				BCe = this.ROL(Ago, 55);
				Aku ^= Du;
				BCi = this.ROL(Aku, 39);
				Ama ^= Da;
				BCo = this.ROL(Ama, 41);
				Ase ^= De;
				BCu = this.ROL(Ase, 2);
				Esa = BCa ^ (~BCe & BCi);
				Ese = BCe ^ (~BCi & BCo);
				Esi = BCi ^ (~BCo & BCu);
				Eso = BCo ^ (~BCu & BCa);
				Esu = BCu ^ (~BCa & BCe);

				BCa = Eba ^ Ega ^ Eka ^ Ema ^ Esa;
				BCe = Ebe ^ Ege ^ Eke ^ Eme ^ Ese;
				BCi = Ebi ^ Egi ^ Eki ^ Emi ^ Esi;
				BCo = Ebo ^ Ego ^ Eko ^ Emo ^ Eso;
				BCu = Ebu ^ Egu ^ Eku ^ Emu ^ Esu;

				Da = BCu ^ this.ROL(BCe, 1);
				De = BCa ^ this.ROL(BCi, 1);
				Di = BCe ^ this.ROL(BCo, 1);
				Do = BCi ^ this.ROL(BCu, 1);
				Du = BCo ^ this.ROL(BCa, 1);

				Eba ^= Da;
				BCa = Eba;
				Ege ^= De;
				BCe = this.ROL(Ege, 44);
				Eki ^= Di;
				BCi = this.ROL(Eki, 43);
				Emo ^= Do;
				BCo = this.ROL(Emo, 21);
				Esu ^= Du;
				BCu = this.ROL(Esu, 14);
				Aba = BCa ^ (~BCe & BCi);
				Aba ^= RoundConstants[round + 1];
				Abe = BCe ^ (~BCi & BCo);
				Abi = BCi ^ (~BCo & BCu);
				Abo = BCo ^ (~BCu & BCa);
				Abu = BCu ^ (~BCa & BCe);

				Ebo ^= Do;
				BCa = this.ROL(Ebo, 28);
				Egu ^= Du;
				BCe = this.ROL(Egu, 20);
				Eka ^= Da;
				BCi = this.ROL(Eka, 3);
				Eme ^= De;
				BCo = this.ROL(Eme, 45);
				Esi ^= Di;
				BCu = this.ROL(Esi, 61);
				Aga = BCa ^ (~BCe & BCi);
				Age = BCe ^ (~BCi & BCo);
				Agi = BCi ^ (~BCo & BCu);
				Ago = BCo ^ (~BCu & BCa);
				Agu = BCu ^ (~BCa & BCe);

				Ebe ^= De;
				BCa = this.ROL(Ebe, 1);
				Egi ^= Di;
				BCe = this.ROL(Egi, 6);
				Eko ^= Do;
				BCi = this.ROL(Eko, 25);
				Emu ^= Du;
				BCo = this.ROL(Emu, 8);
				Esa ^= Da;
				BCu = this.ROL(Esa, 18);
				Aka = BCa ^ (~BCe & BCi);
				Ake = BCe ^ (~BCi & BCo);
				Aki = BCi ^ (~BCo & BCu);
				Ako = BCo ^ (~BCu & BCa);
				Aku = BCu ^ (~BCa & BCe);

				Ebu ^= Du;
				BCa = this.ROL(Ebu, 27);
				Ega ^= Da;
				BCe = this.ROL(Ega, 36);
				Eke ^= De;
				BCi = this.ROL(Eke, 10);
				Emi ^= Di;
				BCo = this.ROL(Emi, 15);
				Eso ^= Do;
				BCu = this.ROL(Eso, 56);
				Ama = BCa ^ (~BCe & BCi);
				Ame = BCe ^ (~BCi & BCo);
				Ami = BCi ^ (~BCo & BCu);
				Amo = BCo ^ (~BCu & BCa);
				Amu = BCu ^ (~BCa & BCe);

				Ebi ^= Di;
				BCa = this.ROL(Ebi, 62);
				Ego ^= Do;
				BCe = this.ROL(Ego, 55);
				Eku ^= Du;
				BCi = this.ROL(Eku, 39);
				Ema ^= Da;
				BCo = this.ROL(Ema, 41);
				Ese ^= De;
				BCu = this.ROL(Ese, 2);
				Asa = BCa ^ (~BCe & BCi);
				Ase = BCe ^ (~BCi & BCo);
				Asi = BCi ^ (~BCo & BCu);
				Aso = BCo ^ (~BCu & BCa);
				Asu = BCu ^ (~BCa & BCe);
			}

			stateBuffer[0] = Aba;
			stateBuffer[1] = Abe;
			stateBuffer[2] = Abi;
			stateBuffer[3] = Abo;
			stateBuffer[4] = Abu;
			stateBuffer[5] = Aga;
			stateBuffer[6] = Age;
			stateBuffer[7] = Agi;
			stateBuffer[8] = Ago;
			stateBuffer[9] = Agu;
			stateBuffer[10] = Aka;
			stateBuffer[11] = Ake;
			stateBuffer[12] = Aki;
			stateBuffer[13] = Ako;
			stateBuffer[14] = Aku;
			stateBuffer[15] = Ama;
			stateBuffer[16] = Ame;
			stateBuffer[17] = Ami;
			stateBuffer[18] = Amo;
			stateBuffer[19] = Amu;
			stateBuffer[20] = Asa;
			stateBuffer[21] = Ase;
			stateBuffer[22] = Asi;
			stateBuffer[23] = Aso;
			stateBuffer[24] = Asu;

		}
	}
}