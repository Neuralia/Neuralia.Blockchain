using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.Signatures;
using Org.BouncyCastle.Crypto;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public abstract class XMSSProviderBase : SignatureProviderBase {
		protected XMSSExecutionContext excutionContext;
		protected Enums.ThreadMode threadMode;

		protected int treeHeight;

		protected XMSSProviderBase(Enums.KeyHashBits hashBits, int treeHeight, Enums.ThreadMode threadMode) {
			this.HashBitsEnum = hashBits;
			this.threadMode = threadMode;

			if(hashBits == Enums.KeyHashBits.SHA3_256) {
				this.HashBits = 256;
				this.sha3 = true;
			}

			if(hashBits == Enums.KeyHashBits.SHA3_384) {
				this.HashBits = 384;
				this.sha3 = true;
			}

			if(hashBits == Enums.KeyHashBits.SHA3_512) {
				this.HashBits = 512;
				this.sha3 = true;
			}

			if(hashBits == Enums.KeyHashBits.SHA2_256) {
				this.HashBits = 256;
				this.sha3 = false;
			}

			if(hashBits == Enums.KeyHashBits.SHA2_512) {
				this.HashBits = 512;
				this.sha3 = false;
			}

			this.treeHeight = treeHeight;
		}

		public abstract int MaximumHeight { get; }

		public int HashBits { get; } = 256;
		private bool sha3 { get; } = true;

		public Enums.KeyHashBits HashBitsEnum { get; } = Enums.KeyHashBits.SHA3_256;

		public int TreeHeight {
			get => this.treeHeight;
			protected set => this.treeHeight = value;
		}

		protected XMSSExecutionContext GetNewExecutionContext() {
			return new XMSSExecutionContext(this.GenerateNewDigest, this.GetRandom());
		}

		protected IDigest GenerateNewDigest() {

			if(this.sha3) {
				return new Sha3ExternalDigest(this.HashBits);
			}

			if(this.HashBits == 256) {
				return new Sha256DotnetDigest();
			}

			return new Sha512DotnetDigest();
		}

		public int GetKeyUseThreshold(float percentage) {
			if((percentage <= 0) || (percentage > 1)) {
				throw new ApplicationException("Invadli percentage value. must be > 0 and <= 1");
			}

			return (int) (this.GetMaxMessagePerKey() * percentage);
		}

		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);

			if(disposing) {

			}
		}

		public abstract int GetMaxMessagePerKey();
	}
}