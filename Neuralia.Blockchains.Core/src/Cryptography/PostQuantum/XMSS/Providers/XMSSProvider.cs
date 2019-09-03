using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers {
	/// <summary>
	///     A random XMSS provider
	/// </summary>
	public class XMSSProvider : XMSSProviderBase {

		//TODO: reset the default values for production :  height 13

		public const int DEFAULT_XMSS_TREE_HEIGHT = 12; //13;
		public const Enums.KeyHashBits DEFAULT_HASH_BITS = Enums.KeyHashBits.SHA2_512;

		protected XMSSEngine xmss;

		public XMSSProvider() : this(DEFAULT_HASH_BITS) {
		}

		public XMSSProvider(Enums.KeyHashBits hashBits, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : this(hashBits, DEFAULT_XMSS_TREE_HEIGHT, threadMode) {
		}

		public XMSSProvider(Enums.KeyHashBits hashBits, int treeHeight, Enums.ThreadMode threadMode = Enums.ThreadMode.Half) : base(hashBits, treeHeight, threadMode) {
		}

		public override int MaximumHeight => this.xmss?.MaximumIndex ?? 0;

		public override void Reset() {
			this.xmss?.Dispose();
			this.excutionContext?.Dispose();

			this.excutionContext = this.GetNewExecutionContext();

			//TODO: make modes configurable
			this.xmss = new XMSSEngine(XMSSOperationModes.Both, Enums.ThreadMode.Half, null, this.excutionContext, this.TreeHeight);
		}

		public (IByteArray privateKey, IByteArray publicKey) GenerateKeys() {
			(XMSSPrivateKey privateKey, XMSSPublicKey publicKey) xmssKeys = this.xmss.GenerateKeys();

			IByteArray publicKey = xmssKeys.publicKey.SaveKey();
			IByteArray privateKey = xmssKeys.privateKey.SaveKey();

			return (privateKey, publicKey);
		}

		public (IByteArray signature, IByteArray nextPrivateKey) Sign(IByteArray content, IByteArray privateKey) {
			XMSSPrivateKey loadedPrivateKey = new XMSSPrivateKey(this.excutionContext);
			loadedPrivateKey.LoadKey(privateKey);

			IByteArray result = this.Sign(content, loadedPrivateKey);

			// export the new private key
			IByteArray nextPrivateKey = loadedPrivateKey.SaveKey();

			loadedPrivateKey.Dispose();

			return (result, nextPrivateKey);
		}

		public IByteArray Sign(IByteArray content, XMSSPrivateKey privateKey) {

			Log.Verbose($"Singing message using XMSS (Key index: {privateKey.Index} of {this.MaximumHeight}, Tree height: {this.TreeHeight}, Hash bits: {this.HashBits})");

			IByteArray signature = this.xmss.Sign(content, privateKey);

			// this is important, increment our key index
			privateKey.IncrementIndex(this.xmss);

			return signature;
		}

		public override bool Verify(IByteArray message, IByteArray signature, IByteArray publicKey) {

			return this.xmss.Verify(signature, message, publicKey);
		}

		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);

			if(disposing) {
				this.xmss?.Dispose();
				this.excutionContext?.Dispose();
			}

		}

		public override int GetMaxMessagePerKey() {
			return this.MaximumHeight;
		}
	}
}