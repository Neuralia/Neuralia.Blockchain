using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface ISecretComboWalletKey : ISecretWalletKey {

		long PromisedNonce1 { get; set; }

		long PromisedNonce2 { get; set; }
	}

	//TODO: ensure we use sakura trees for the hashing of a secret key.
	//TODO: is it safe to hash a key?  what if another type of key/nonce combination can also give the same hash?
	/// <summary>
	///     A secret key is QTesla key we keep secret and use only once.
	/// </summary>
	public class SecretComboWalletKey : SecretWalletKey, ISecretComboWalletKey {

		public SecretComboWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.SecretCombo, 1,0);
		}
		
		public long PromisedNonce1 { get; set; }

		public long PromisedNonce2 { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.PromisedNonce1);
			nodeList.Add(this.PromisedNonce2);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.PromisedNonce1);
			dehydrator.Write(this.PromisedNonce2);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.PromisedNonce1 = rehydrator.ReadLong();
			this.PromisedNonce2 = rehydrator.ReadLong();
		}
	}
}