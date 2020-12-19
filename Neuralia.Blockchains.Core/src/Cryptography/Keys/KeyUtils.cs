using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Cryptography.Keys {
	public static class KeyUtils {
		public static void SetFromKey(IKey source, IKey destination) {
			
			destination.Ordinal = source.Ordinal;
			destination.Index = source.Index.Clone();
			destination.PublicKey.Entry = source.PublicKey?.Entry?.Clone();
			
			if(source is IXmssKey sourceXmssWalletKey && destination is IXmssKey destinationXmssWalletKey) {
				destinationXmssWalletKey.HashType = sourceXmssWalletKey.HashType;
				destinationXmssWalletKey.BackupHashType = sourceXmssWalletKey.BackupHashType;
				destinationXmssWalletKey.TreeHeight = sourceXmssWalletKey.TreeHeight;
				destinationXmssWalletKey.NoncesExponent = sourceXmssWalletKey.NoncesExponent;
			}
			
			if(source is IXmssmtKey sourceXmssmtKey && destination is IXmssmtKey destinationXmssmtKey) {
				destinationXmssmtKey.TreeLayers = sourceXmssmtKey.TreeLayers;
			}
			
			if(source is IMcElieceKey sourceMccElieceWalletKey && destination is IMcElieceKey destinationMcElieceWalletKey) {
				destinationMcElieceWalletKey.McElieceCipherMode = sourceMccElieceWalletKey.McElieceCipherMode;
				destinationMcElieceWalletKey.McElieceHashMode = sourceMccElieceWalletKey.McElieceHashMode;
				destinationMcElieceWalletKey.M = sourceMccElieceWalletKey.M;
				destinationMcElieceWalletKey.T = sourceMccElieceWalletKey.T;
			}
			
			if(source is INTRUPrimeKey sourceNtruPrimeWalletKey && destination is INTRUPrimeKey destinationNtruPrimeWalletKey) {
				destinationNtruPrimeWalletKey.Strength = sourceNtruPrimeWalletKey.Strength;
			}
			
			if(source is ISecretKey sourceSecretWalletKey && destination is ISecretKey destinationSecretWalletKey) {

				destinationSecretWalletKey.NextKeyHashSha2.Entry = sourceSecretWalletKey.NextKeyHashSha2.Entry;
				destinationSecretWalletKey.NextKeyHashSha3.Entry = sourceSecretWalletKey.NextKeyHashSha3.Entry;
			}
			
			if(source is ISecretComboKey sourceSecretComboWalletKey && destination is ISecretComboKey destinationSecretComboWalletKey) {
				
				destinationSecretComboWalletKey.NonceHash = sourceSecretComboWalletKey.NonceHash;
			}
			
			if(source is ISecretDoubleKey sourceSecretDoubleWalletKey && destination is ISecretDoubleKey destinationSecretDoubleWalletKey) {

				SetFromKey(sourceSecretDoubleWalletKey.SecondKey, destinationSecretDoubleWalletKey.SecondKey);
			}
			
			if(source is ISecretPentaKey sourceSecretPentaWalletKey && destination is ISecretPentaKey destinationSecretPentaWalletKey) {

				SetFromKey(sourceSecretPentaWalletKey.ThirdKey, destinationSecretPentaWalletKey.ThirdKey);
				SetFromKey(sourceSecretPentaWalletKey.FourthKey, destinationSecretPentaWalletKey.FourthKey);
				SetFromKey(sourceSecretPentaWalletKey.FifthKey, destinationSecretPentaWalletKey.FifthKey);
			}
			
			if(source is ITripleXmssKey sourceTripleXmssWalletKey && destination is ITripleXmssKey destinationTripleXmssWWalletKey) {

				SetFromKey(sourceTripleXmssWalletKey.FirstKey, destinationTripleXmssWWalletKey.FirstKey);
				SetFromKey(sourceTripleXmssWalletKey.SecondKey, destinationTripleXmssWWalletKey.SecondKey);
				SetFromKey(sourceTripleXmssWalletKey.ThirdKey, destinationTripleXmssWWalletKey.ThirdKey);
			}
		}
	}
}