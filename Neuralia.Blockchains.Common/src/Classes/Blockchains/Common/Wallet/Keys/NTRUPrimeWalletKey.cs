using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {

	public interface INTRUPrimeWalletKey : INTRUPrimeKey, IWalletKey {
	}

	public class NTRUPrimeWalletKey : WalletKey, INTRUPrimeWalletKey {
		
		public NTRUPrimeWalletKey() {
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.NTRUPrime, 1,0);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			
			dehydrator.Write((byte)this.Strength);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.Strength = (NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes)rehydrator.ReadByte();
		}
		
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add((byte)this.Strength);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("Strength", this.Strength);
		}

		public NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes Strength { get; set; } = NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857;
	}
}