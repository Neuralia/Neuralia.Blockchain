using System;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public class XMSSMTPublicKey : XMSSMTKey {

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSMTPublicKey(ByteArray publicSeed, ByteArray root, ByteArray backupRoot, XMSSExecutionContext xmssExecutionContext) {

			this.PublicSeed = publicSeed?.Clone();
			this.Root = root?.Clone();
			this.BackupRoot = backupRoot?.Clone();
			this.xmssExecutionContext = xmssExecutionContext;
		}

		public XMSSMTPublicKey(XMSSExecutionContext xmssExecutionContext) : this(null, null, null, xmssExecutionContext) {

		}

		public ByteArray PublicSeed { get; private set; }
		public ByteArray Root { get; private set; }
		public ByteArray BackupRoot { get; private set; }

		public override void LoadKey(SafeArrayHandle publicKey) {

			int totalSize = (this.xmssExecutionContext.DigestSize * 2) + this.xmssExecutionContext.BackupDigestSize;

			if(publicKey.Length != totalSize) {
				throw new ArgumentException($"Public is not of the expected size of {totalSize}");
			}

			this.Root = ByteArray.Create(this.xmssExecutionContext.DigestSize);
			this.Root.CopyFrom(publicKey.Entry, 0, this.Root.Length);
			
			this.BackupRoot = ByteArray.Create(this.xmssExecutionContext.BackupDigestSize);
			this.BackupRoot.CopyFrom(publicKey.Entry, this.Root.Length, this.BackupRoot.Length);
			
			this.PublicSeed = ByteArray.Create(this.xmssExecutionContext.DigestSize);
			this.PublicSeed.CopyFrom(publicKey.Entry, this.Root.Length + this.BackupRoot.Length, this.PublicSeed.Length);
		}

		public override SafeArrayHandle SaveKey() {
			SafeArrayHandle keyBuffer = SafeArrayHandle.Create(this.Root.Length + this.BackupRoot.Length + this.PublicSeed.Length);

			keyBuffer.Entry.CopyFrom(this.Root);
			keyBuffer.Entry.CopyFrom(this.BackupRoot, this.Root.Length);
			keyBuffer.Entry.CopyFrom(this.PublicSeed, this.Root.Length + this.BackupRoot.Length);

			return keyBuffer;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.PublicSeed?.Dispose();
			this.Root?.Dispose();
			this.BackupRoot?.Dispose();
		}
	}
}