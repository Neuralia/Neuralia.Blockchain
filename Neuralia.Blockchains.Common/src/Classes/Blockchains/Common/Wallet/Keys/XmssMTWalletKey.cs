using System.Collections.Generic;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssMTWalletKey : IXmssmtKey, IXmssWalletKey {
	}

	public class XmssMTWalletKey : XmssWalletKey, IXmssMTWalletKey {

		public XmssMTWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.XMSSMT, 1,0);
		}
		
		/// <summary>
		///     xmss layers if XMSSMT
		/// </summary>
		public byte TreeLayers { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeLayers);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.TreeLayers);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.TreeLayers = rehydrator.ReadByte();
		}
		
		protected override string KeyExportName => "XMSS^MT_WALLET";

		protected override string ExportPrivateKey() {
			using(XMSSMTProvider provider = new XMSSMTProvider(this.HashType, this.BackupHashType, this.TreeHeight, this.TreeLayers, Enums.ThreadMode.Single, this.NoncesExponent)) {
				provider.Initialize();
				var privateKey = provider.LoadPrivateKey(this.PrivateKey);

				return privateKey.ExportKey();
			}
		}
	}
}