// namespace Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto {
// 	public class POWXChachaCrypto : POWCryptoBase {
// 		
// 		private const int AES_IV_SIZE  = 16;
// 		private const int AES_KEY_SIZE = 32;
// 		
// 		public override void EncryptStringToBytes(SafeArrayHandle message, SafeArrayHandle encrypted) {
// 			
// 			using(Aes aesAlg = Aes.Create()) {
// 				
// 				using ByteArray key = ByteArray.CreateSimpleArray(AES_KEY_SIZE);
// 				using ByteArray iv  = ByteArray.CreateSimpleArray(AES_IV_SIZE);
// 				
// 				key.CopyFrom(message.Entry, message.Length - AES_KEY_SIZE, 0, AES_KEY_SIZE);
// 				iv.CopyFrom(message.Entry, message.Length  - AES_KEY_SIZE -AES_IV_SIZE, 0, AES_IV_SIZE);
// 				
// 				aesAlg.Key = key.Bytes;
// 				aesAlg.IV  = iv.Bytes;
//
// 				aesAlg.Mode    = CipherMode.CBC;
// 				aesAlg.Padding = PaddingMode.None;
// 				
// 				using(ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV)) {
// 					using(MemoryStream msEncrypt = new MemoryStream(encrypted.Bytes, encrypted.Offset, encrypted.Length)) {
// 						using(CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)) {
// 							csEncrypt.Write(message.Bytes, message.Offset, message.Length);
// 						}
// 					}
// 				}
// 			}
// 		}
// 	}
// }