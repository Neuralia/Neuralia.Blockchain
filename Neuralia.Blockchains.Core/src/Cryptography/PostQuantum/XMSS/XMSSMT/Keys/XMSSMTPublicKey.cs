using System;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Allocation;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {
	public class XMSSMTPublicKey : XMSSMTKey {

		private readonly XMSSExecutionContext xmssExecutionContext;

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSMTPublicKey(IByteArray publicSeed, IByteArray root, XMSSExecutionContext xmssExecutionContext) {

			this.PublicSeed = publicSeed?.Clone();
			this.Root = root?.Clone();
			this.xmssExecutionContext = xmssExecutionContext;
		}

		public XMSSMTPublicKey(XMSSExecutionContext xmssExecutionContext) : this(null, null, xmssExecutionContext) {

		}

		public IByteArray PublicSeed { get; private set; }
		public IByteArray Root { get; private set; }

		public override void LoadKey(IByteArray publicKey) {

			int totalSize = this.xmssExecutionContext.DigestSize * 2;

			if(publicKey.Length != totalSize) {
				throw new ArgumentException($"Public is not of the expected size of {totalSize}");
			}

			this.Root = MemoryAllocators.Instance.cryptoAllocator.Take(this.xmssExecutionContext.DigestSize);

			this.Root.CopyFrom(publicKey, 0, this.Root.Length);
			this.PublicSeed = MemoryAllocators.Instance.cryptoAllocator.Take(this.xmssExecutionContext.DigestSize);

			this.PublicSeed.CopyFrom(publicKey, this.Root.Length, this.PublicSeed.Length);
		}

		public override IByteArray SaveKey() {
			IByteArray keyBuffer = MemoryAllocators.Instance.cryptoAllocator.Take(this.Root.Length + this.PublicSeed.Length);

			keyBuffer.CopyFrom(this.Root);
			keyBuffer.CopyFrom(this.PublicSeed, this.Root.Length);

			return keyBuffer;
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			this.PublicSeed?.Dispose();
			this.Root?.Dispose();
		}
	}
}