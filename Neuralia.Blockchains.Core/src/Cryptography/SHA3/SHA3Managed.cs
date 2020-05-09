using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.SHA3 {
	/// <summary>
	///     Custom implementation of SHA3
	/// </summary>
	public abstract unsafe class SHA3Managed : SHA3 {

		private const int KECCAK_NUMBER_OF_ROUNDS = 0x18;
		private const int KECCAK_LANE_SIZE_IN_BITS = 8 << 3;

		private static readonly ulong[] RoundConstants = {0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL, 0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL, 0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL, 0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL, 0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL, 0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL};
		private readonly int chunk;

		internal readonly int keccakCode;

		private ByteArray buffer;

		private int bufferLength;
		private int buffLength;

		private ByteArray chunkBuffer;
		private MemoryHandle chunkBufferMemHandle;
		private ulong* chunkBufferPtr;
		private byte[] obligatoryHashResult;
		private ByteArray state;
		private MemoryHandle stateMemHandle;

		private ulong* statePtr;

		internal SHA3Managed(int hashBitLength) {

			this.HashSize = hashBitLength;

			switch(hashBitLength) {
				case 256:
					this.keccakCode = 1088;

					break;

				case 512:
					this.keccakCode = 576;

					break;
				default:
					throw new ArgumentException("hashBitLength must be either 256 or 512", nameof(hashBitLength));
			}

			this.chunk = this.SizeInBytes >> 3;

			this.Initialize();
		}

		public override int HashSize { get; }

		protected int SizeInBytes => this.keccakCode >> 3;

		protected int HashByteLength => this.HashSize >> 3;

		/// <summary>
		///     the results of the last hashing.
		/// </summary>
		/// <remarks>We do NOT clear this memory. the caller owns it and is responsible for clearing it. not thread safe!</remarks>
		public ByteArray LastResult { get; private set; }

		public override void Initialize() {

			if(this.buffer?.Length != this.SizeInBytes) {
				this.buffer = null;
			}

			if(this.buffer?.IsNull ?? true) {
				this.buffer = ByteArray.Create(this.SizeInBytes);
			}

			this.bufferLength = this.SizeInBytes;
			this.buffLength = 0;

			if(this.state?.IsEmpty ?? true) {
				this.state = ByteArray.Create<ulong>(5 * 5);

				this.stateMemHandle = this.state.Memory.Pin();
				this.statePtr = (ulong*) this.stateMemHandle.Pointer;
			} else {
				this.state.Clear();
			}

			if(this.chunkBuffer?.IsEmpty ?? true) {
				this.chunkBuffer = ByteArray.Create<ulong>(this.chunk);

				this.chunkBufferMemHandle = this.chunkBuffer.Memory.Pin();
				this.chunkBufferPtr = (ulong*) this.chunkBufferMemHandle.Pointer;
			} else {
				this.chunkBuffer.Clear();
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if(disposing && !this.IsDisposed) {
				try {
					this.buffer.Dispose();
				} catch {
				}

				try {
					this.state.Dispose();
				} catch {
				}

				try {
					this.stateMemHandle.Dispose();
				} catch {
				}

				this.statePtr = null;

				try {
					this.chunkBufferMemHandle.Dispose();
				} catch {
				}

				this.chunkBufferPtr = null;
			}

			this.IsDisposed = true;
		}

		private void AddToBuffer(byte[] array, ref int offset, ref int count) {
			int amount = Math.Min(count, this.bufferLength - this.buffLength);

			this.buffer.CopyFrom((ReadOnlySpan<byte>) array, offset, this.buffLength, amount);

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

			ByteArray result = this.LastResult;

			this.LastResult = null;

			return result;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize) {
			base.HashCore(array, ibStart, cbSize);

			if(cbSize == 0) {
				return;
			}

			if(this.buffLength == this.SizeInBytes) {
				throw new InvalidOperationException("The buffer has no space.");
			}

			this.AddToBuffer(array, ref ibStart, ref cbSize);

			//buffer is full
			if(this.buffLength == this.SizeInBytes) {
				this.buffer.CopyTo(this.chunkBuffer, 0, 0, this.SizeInBytes);

				this.Keccak(this.chunkBufferPtr, this.chunk);
				this.buffLength = 0;
			}

			for(; cbSize >= this.SizeInBytes; cbSize -= this.SizeInBytes, ibStart += this.SizeInBytes) {

				this.chunkBuffer.CopyFrom(ref array, ibStart, 0, this.SizeInBytes);
				this.Keccak(this.chunkBufferPtr, this.chunk);
			}

			if(cbSize > 0) //some left over
			{
				this.buffer.CopyFrom((ReadOnlySpan<byte>) array, ibStart, this.buffLength, cbSize);

				this.buffLength += cbSize;
			}
		}

		private void CustomHashFinal() {
			//    padding
			this.buffer.Clear(this.buffLength, this.SizeInBytes - this.buffLength);

			if(this.UseKeccakPadding) {
				this.buffer[this.buffLength++] = 1;
			} else {
				this.buffer[this.buffLength++] = 6;
			}

			this.buffer[this.SizeInBytes - 1] |= 0x80;

			// result will be return and cleared by the callers. we are not responsible for it
			this.LastResult = ByteArray.Create(this.HashByteLength);

			this.buffer.CopyTo(this.chunkBuffer, 0, 0, this.SizeInBytes);
			this.Keccak(this.chunkBufferPtr, this.chunk);
			this.state.CopyTo(this.LastResult, 0, 0, this.HashByteLength);
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
		private ulong ROL(ulong* a, int offset) {
			return (*a << (offset % KECCAK_LANE_SIZE_IN_BITS)) ^ (*a >> (KECCAK_LANE_SIZE_IN_BITS - (offset % KECCAK_LANE_SIZE_IN_BITS)));
		}

		private void Keccak(ulong* inb, int laneCount) {

			while(--laneCount >= 0) {
				*(this.statePtr + laneCount) ^= *(inb + laneCount);
			}

			ulong* Aba, Abe, Abi, Abo, Abu;
			ulong* Aga, Age, Agi, Ago, Agu;
			ulong* Aka, Ake, Aki, Ako, Aku;
			ulong* Ama, Ame, Ami, Amo, Amu;
			ulong* Asa, Ase, Asi, Aso, Asu;
			ulong BCa, BCe, BCi, BCo, BCu;
			ulong Da, De, Di, Do, Du;
			ulong Eba, Ebe, Ebi, Ebo, Ebu;
			ulong Ega, Ege, Egi, Ego, Egu;
			ulong Eka, Eke, Eki, Eko, Eku;
			ulong Ema, Eme, Emi, Emo, Emu;
			ulong Esa, Ese, Esi, Eso, Esu;
			int round = laneCount;

			Aba = this.statePtr;
			Abe = this.statePtr + 1;
			Abi = this.statePtr + 2;
			Abo = this.statePtr + 3;
			Abu = this.statePtr + 4;
			Aga = this.statePtr + 5;
			Age = this.statePtr + 6;
			Agi = this.statePtr + 7;
			Ago = this.statePtr + 8;
			Agu = this.statePtr + 9;
			Aka = this.statePtr + 10;
			Ake = this.statePtr + 11;
			Aki = this.statePtr + 12;
			Ako = this.statePtr + 13;
			Aku = this.statePtr + 14;
			Ama = this.statePtr + 15;
			Ame = this.statePtr + 16;
			Ami = this.statePtr + 17;
			Amo = this.statePtr + 18;
			Amu = this.statePtr + 19;
			Asa = this.statePtr + 20;
			Ase = this.statePtr + 21;
			Asi = this.statePtr + 22;
			Aso = this.statePtr + 23;
			Asu = this.statePtr + 24;

			for(round = 0; round < KECCAK_NUMBER_OF_ROUNDS; round += 2) {
				BCa = *Aba ^ *Aga ^ *Aka ^ *Ama ^ *Asa;
				BCe = *Abe ^ *Age ^ *Ake ^ *Ame ^ *Ase;
				BCi = *Abi ^ *Agi ^ *Aki ^ *Ami ^ *Asi;
				BCo = *Abo ^ *Ago ^ *Ako ^ *Amo ^ *Aso;
				BCu = *Abu ^ *Agu ^ *Aku ^ *Amu ^ *Asu;

				Da = BCu ^ this.ROL(&BCe, 1);
				De = BCa ^ this.ROL(&BCi, 1);
				Di = BCe ^ this.ROL(&BCo, 1);
				Do = BCi ^ this.ROL(&BCu, 1);
				Du = BCo ^ this.ROL(&BCa, 1);

				*Aba ^= Da;
				BCa = *Aba;
				*Age ^= De;
				BCe = this.ROL(Age, 44);
				*Aki ^= Di;
				BCi = this.ROL(Aki, 43);
				*Amo ^= Do;
				BCo = this.ROL(Amo, 21);
				*Asu ^= Du;
				BCu = this.ROL(Asu, 14);
				Eba = BCa ^ (~BCe & BCi);
				Eba ^= RoundConstants[round];
				Ebe = BCe ^ (~BCi & BCo);
				Ebi = BCi ^ (~BCo & BCu);
				Ebo = BCo ^ (~BCu & BCa);
				Ebu = BCu ^ (~BCa & BCe);

				*Abo ^= Do;
				BCa = this.ROL(Abo, 28);
				*Agu ^= Du;
				BCe = this.ROL(Agu, 20);
				*Aka ^= Da;
				BCi = this.ROL(Aka, 3);
				*Ame ^= De;
				BCo = this.ROL(Ame, 45);
				*Asi ^= Di;
				BCu = this.ROL(Asi, 61);
				Ega = BCa ^ (~BCe & BCi);
				Ege = BCe ^ (~BCi & BCo);
				Egi = BCi ^ (~BCo & BCu);
				Ego = BCo ^ (~BCu & BCa);
				Egu = BCu ^ (~BCa & BCe);

				*Abe ^= De;
				BCa = this.ROL(Abe, 1);
				*Agi ^= Di;
				BCe = this.ROL(Agi, 6);
				*Ako ^= Do;
				BCi = this.ROL(Ako, 25);
				*Amu ^= Du;
				BCo = this.ROL(Amu, 8);
				*Asa ^= Da;
				BCu = this.ROL(Asa, 18);
				Eka = BCa ^ (~BCe & BCi);
				Eke = BCe ^ (~BCi & BCo);
				Eki = BCi ^ (~BCo & BCu);
				Eko = BCo ^ (~BCu & BCa);
				Eku = BCu ^ (~BCa & BCe);

				*Abu ^= Du;
				BCa = this.ROL(Abu, 27);
				*Aga ^= Da;
				BCe = this.ROL(Aga, 36);
				*Ake ^= De;
				BCi = this.ROL(Ake, 10);
				*Ami ^= Di;
				BCo = this.ROL(Ami, 15);
				*Aso ^= Do;
				BCu = this.ROL(Aso, 56);
				Ema = BCa ^ (~BCe & BCi);
				Eme = BCe ^ (~BCi & BCo);
				Emi = BCi ^ (~BCo & BCu);
				Emo = BCo ^ (~BCu & BCa);
				Emu = BCu ^ (~BCa & BCe);

				*Abi ^= Di;
				BCa = this.ROL(Abi, 62);
				*Ago ^= Do;
				BCe = this.ROL(Ago, 55);
				*Aku ^= Du;
				BCi = this.ROL(Aku, 39);
				*Ama ^= Da;
				BCo = this.ROL(Ama, 41);
				*Ase ^= De;
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

				Da = BCu ^ this.ROL(&BCe, 1);
				De = BCa ^ this.ROL(&BCi, 1);
				Di = BCe ^ this.ROL(&BCo, 1);
				Do = BCi ^ this.ROL(&BCu, 1);
				Du = BCo ^ this.ROL(&BCa, 1);

				Eba ^= Da;
				BCa = Eba;
				Ege ^= De;
				BCe = this.ROL(&Ege, 44);
				Eki ^= Di;
				BCi = this.ROL(&Eki, 43);
				Emo ^= Do;
				BCo = this.ROL(&Emo, 21);
				Esu ^= Du;
				BCu = this.ROL(&Esu, 14);
				*Aba = BCa ^ (~BCe & BCi);
				*Aba ^= RoundConstants[round + 1];
				*Abe = BCe ^ (~BCi & BCo);
				*Abi = BCi ^ (~BCo & BCu);
				*Abo = BCo ^ (~BCu & BCa);
				*Abu = BCu ^ (~BCa & BCe);

				Ebo ^= Do;
				BCa = this.ROL(&Ebo, 28);
				Egu ^= Du;
				BCe = this.ROL(&Egu, 20);
				Eka ^= Da;
				BCi = this.ROL(&Eka, 3);
				Eme ^= De;
				BCo = this.ROL(&Eme, 45);
				Esi ^= Di;
				BCu = this.ROL(&Esi, 61);
				*Aga = BCa ^ (~BCe & BCi);
				*Age = BCe ^ (~BCi & BCo);
				*Agi = BCi ^ (~BCo & BCu);
				*Ago = BCo ^ (~BCu & BCa);
				*Agu = BCu ^ (~BCa & BCe);

				Ebe ^= De;
				BCa = this.ROL(&Ebe, 1);
				Egi ^= Di;
				BCe = this.ROL(&Egi, 6);
				Eko ^= Do;
				BCi = this.ROL(&Eko, 25);
				Emu ^= Du;
				BCo = this.ROL(&Emu, 8);
				Esa ^= Da;
				BCu = this.ROL(&Esa, 18);
				*Aka = BCa ^ (~BCe & BCi);
				*Ake = BCe ^ (~BCi & BCo);
				*Aki = BCi ^ (~BCo & BCu);
				*Ako = BCo ^ (~BCu & BCa);
				*Aku = BCu ^ (~BCa & BCe);

				Ebu ^= Du;
				BCa = this.ROL(&Ebu, 27);
				Ega ^= Da;
				BCe = this.ROL(&Ega, 36);
				Eke ^= De;
				BCi = this.ROL(&Eke, 10);
				Emi ^= Di;
				BCo = this.ROL(&Emi, 15);
				Eso ^= Do;
				BCu = this.ROL(&Eso, 56);
				*Ama = BCa ^ (~BCe & BCi);
				*Ame = BCe ^ (~BCi & BCo);
				*Ami = BCi ^ (~BCo & BCu);
				*Amo = BCo ^ (~BCu & BCa);
				*Amu = BCu ^ (~BCa & BCe);

				Ebi ^= Di;
				BCa = this.ROL(&Ebi, 62);
				Ego ^= Do;
				BCe = this.ROL(&Ego, 55);
				Eku ^= Du;
				BCi = this.ROL(&Eku, 39);
				Ema ^= Da;
				BCo = this.ROL(&Ema, 41);
				Ese ^= De;
				BCu = this.ROL(&Ese, 2);
				*Asa = BCa ^ (~BCe & BCi);
				*Ase = BCe ^ (~BCi & BCo);
				*Asi = BCi ^ (~BCo & BCu);
				*Aso = BCo ^ (~BCu & BCa);
				*Asu = BCu ^ (~BCa & BCe);
			}

		}
	}
}