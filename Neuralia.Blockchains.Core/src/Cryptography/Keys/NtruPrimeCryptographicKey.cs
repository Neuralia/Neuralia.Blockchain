﻿using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {

	public interface INTRUPrimeKey : IKey {
		NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes Strength { get; set; }
	}

	public interface INTRUPrimeCryptographicKey : INTRUPrimeKey, ICryptographicKey {
	}

	public class NTRUPrimeCryptographicKey : CryptographicKey, INTRUPrimeCryptographicKey {

		public NTRUPrimeCryptographicKey() {

		}

		public NTRUPrimeCryptographicKey(INTRUPrimeCryptographicKey other) : base(other) {
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

		public override void SetFromKey(IKey walletKey) {
			base.SetFromKey(walletKey);

			if(walletKey is INTRUPrimeKey ntruPrimeKey) {
				this.Strength = ntruPrimeKey.Strength;
			}
		}
		
		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.NTRUPrime, 1, 0);
		}
		
		public NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes Strength { get; set; } = NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857;
	}
}