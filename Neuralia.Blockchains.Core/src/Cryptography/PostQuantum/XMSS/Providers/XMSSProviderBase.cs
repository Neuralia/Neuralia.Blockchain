using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.Signatures;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public abstract class XMSSProviderBase : SignatureProviderBase {
		protected XMSSExecutionContext excutionContext;
		protected Enums.ThreadMode threadMode;

		public enum hashAlgos{Sha2,Sha3,Blake2}
		protected int treeHeight;

		public const int BITS_256 = 256;
		public const int BITS_512 = 512;
		
		protected XMSSProviderBase(Enums.KeyHashBits hashBits, int treeHeight, Enums.ThreadMode threadMode) {
			this.HashBitsEnum = hashBits;
			this.threadMode = threadMode;

			if(hashBits.HasFlag((Enums.KeyHashBits)Enums.SHA2)) {
				this.hashAlgo = hashAlgos.Sha2;
			}
			else if(hashBits.HasFlag((Enums.KeyHashBits)Enums.SHA3)) {
				this.hashAlgo = hashAlgos.Sha3;
			}
			else if(hashBits.HasFlag((Enums.KeyHashBits)Enums.BLAKE2)) {
				this.hashAlgo = hashAlgos.Blake2;
			}
			
			this.HashBits = BITS_256;
			if(hashBits.HasFlag((Enums.KeyHashBits)Enums.HASH512)) {
				this.HashBits = BITS_512;
			}

			this.treeHeight = treeHeight;
		}

		public abstract int MaximumHeight { get; }

		public int HashBits { get; } = BITS_256;
		private hashAlgos hashAlgo { get; }= hashAlgos.Sha3;
		
		public Enums.KeyHashBits HashBitsEnum { get; } = Enums.KeyHashBits.SHA3_256;

		public int TreeHeight {
			get => this.treeHeight;
			protected set => this.treeHeight = value;
		}

		protected XMSSExecutionContext GetNewExecutionContext() {
			return new XMSSExecutionContext(this.GenerateNewDigest, this.GetRandom());
		}

		protected IDigest GenerateNewDigest() {
			
			if(this.hashAlgo == hashAlgos.Sha2) {
				if(this.HashBits == BITS_256) {
					return new Sha256DotnetDigest();
				}

				return new Sha512DotnetDigest();
			}

			if(this.hashAlgo == hashAlgos.Blake2) {
				return new Blake2bDigest(this.HashBits);
			}

			return new Sha3ExternalDigest(this.HashBits);
		}

		public int GetKeyUseThreshold(float percentage) {
			if((percentage <= 0) || (percentage > 1)) {
				throw new ApplicationException("Invadli percentage value. must be > 0 and <= 1");
			}

			return (int) (this.GetMaxMessagePerKey() * percentage);
		}

		protected override void DisposeAll() {
			base.DisposeAll();
			
		}

		public abstract int GetMaxMessagePerKey();
	}
}