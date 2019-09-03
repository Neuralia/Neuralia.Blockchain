using System;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssWalletKey : IWalletKey {
		int TreeHeight { get; set; }
		int KeyUseIndex { get; set; }
		int WarningHeight { get; set; }
		int ChangeHeight { get; set; }
		int MaximumHeight { get; set; }
		byte[] InitialPrivateKey { get; set; }
		Enums.KeyHashBits HashBits { get; set; }
	}

	public class XmssWalletKey : WalletKey, IXmssWalletKey {

		/// <summary>
		///     the amount of bits used for hashing XMSS tree
		/// </summary>
		public Enums.KeyHashBits HashBits { get; set; } = Enums.KeyHashBits.SHA3_256;

		/// <summary>
		///     The current private key index
		/// </summary>
		public int KeyUseIndex { get; set; }

		/// <summary>
		///     the amount of keys allowed before we should think about changing our key
		/// </summary>
		public int WarningHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public int ChangeHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public int MaximumHeight { get; set; }

		/// <summary>
		///     xmss tree height
		/// </summary>
		public int TreeHeight { get; set; }

		/// <summary>
		///     This is the first private key when generated. we store it, in case we need to rewalk the key sequence from scratch
		/// </summary>
		public byte[] InitialPrivateKey { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.InitialPrivateKey);
			nodeList.Add(this.TreeHeight);
			nodeList.Add(this.KeyUseIndex);
			nodeList.Add(this.WarningHeight);
			nodeList.Add(this.ChangeHeight);
			nodeList.Add(this.MaximumHeight);
			nodeList.Add((byte) this.HashBits);

			return nodeList;
		}

		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);

			if(disposing) {
				if(this.InitialPrivateKey != null) {
					Array.Clear(this.InitialPrivateKey, 0, this.InitialPrivateKey.Length);
					this.InitialPrivateKey = null;
				}

				// yes, its good to clear this. just in case
				this.KeyUseIndex = 0;
				this.MaximumHeight = 0;
			}

		}
	}
}