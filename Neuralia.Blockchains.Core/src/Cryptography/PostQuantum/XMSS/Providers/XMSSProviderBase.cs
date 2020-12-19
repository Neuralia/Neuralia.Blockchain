using System;
using Neuralia.Blockchains.Core.Cryptography.crypto.digests;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.Signatures;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	public abstract class XMSSProviderBase : SignatureProviderBase {

		public enum hashAlgos {
			Sha2,
			Sha3
		}

		public const int BITS_256 = 256;
		public const int BITS_512 = 512;
		public XMSSExecutionContext ExcutionContext { get; protected set; }
		protected Enums.ThreadMode threadMode;
		protected byte treeHeight;
		protected byte noncesExponent;

		protected XMSSProviderBase(Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, byte treeHeight, Enums.ThreadMode threadMode, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) {
			this.HashTypeEnum = hashType;
			this.BackupHashTypeEnum = backupHashType;
			this.threadMode = threadMode;
			this.NoncesExponent = noncesExponent;

			if(hashType.HasFlag((Enums.KeyHashType) Enums.SHA2)) {
				this.hashAlgo = hashAlgos.Sha2;
			} else if(hashType.HasFlag((Enums.KeyHashType) Enums.SHA3)) {
				this.hashAlgo = hashAlgos.Sha3;
			}

			this.HashType = BITS_256;

			if(hashType.HasFlag((Enums.KeyHashType) Enums.HASH512)) {
				this.HashType = BITS_512;
			}
			
			
			if(backupHashType.HasFlag((Enums.KeyHashType) Enums.SHA2)) {
				this.backupHashAlgo = hashAlgos.Sha2;
			} else if(backupHashType.HasFlag((Enums.KeyHashType) Enums.SHA3)) {
				this.backupHashAlgo = hashAlgos.Sha3;
			}


			this.BackupHashType = BITS_256;

			if(backupHashType.HasFlag((Enums.KeyHashType) Enums.HASH512)) {
				this.BackupHashType = BITS_512;
			}

			this.treeHeight = treeHeight;

			if(this.hashAlgo == this.backupHashAlgo && this.HashType == this.BackupHashType) {
				throw new ApplicationException($"The hashing and backup hashing algorithms can not be the same with the same bit strength");
			}
		}

		public abstract long MaximumHeight { get; }
		public bool EnableCache { get; set; } = true;

		public int HashType { get; } = BITS_256;
		private hashAlgos hashAlgo { get; } = hashAlgos.Sha3;
		public Enums.KeyHashType HashTypeEnum { get; } = Enums.KeyHashType.SHA3_256;

		public int BackupHashType { get; } = BITS_256;
		private hashAlgos backupHashAlgo { get; } = hashAlgos.Sha3;
		public Enums.KeyHashType BackupHashTypeEnum { get; } = Enums.KeyHashType.SHA3_256;
		
		
		public byte TreeHeight {
			get => this.treeHeight;
			protected set => this.treeHeight = value;
		}
		
		public byte NoncesExponent {
			get => this.noncesExponent;
			protected set => this.noncesExponent = value;
		}

		protected XMSSExecutionContext GetNewExecutionContext() {
			return new XMSSExecutionContext(this.HashTypeEnum, this.BackupHashTypeEnum, this.GenerateNewDigest, this.GenerateNewBackupDigest, this.EnableCache, this.NoncesExponent);
		}

		protected IHashDigest GenerateNewDigest() {

			if(this.hashAlgo == hashAlgos.Sha2) {
				if(this.HashType == BITS_256) {
					return new Sha256DotnetDigest();
				}

				return new Sha512DotnetDigest();
			}

			return new Sha3ExternalDigest(this.HashType);
		}
		
		protected IHashDigest GenerateNewBackupDigest() {

			if(this.backupHashAlgo == hashAlgos.Sha2) {
				if(this.BackupHashType == BITS_256) {
					return new Sha256DotnetDigest();
				}

				return new Sha512DotnetDigest();
			}

			return new Sha3ExternalDigest(this.BackupHashType);
		}

		public long GetKeyUseThreshold(float percentage) {
			if((percentage <= 0) || (percentage > 1)) {
				throw new ApplicationException("Invadli percentage value. must be > 0 and <= 1");
			}

			return  (long)(this.GetMaxMessagePerKey() * percentage);
		}

		protected override void DisposeAll() {
			base.DisposeAll();

		}

		public abstract SafeArrayHandle SetPrivateKeyIndex(int index, SafeArrayHandle privateKey);

		public abstract long GetMaxMessagePerKey();
	}
}