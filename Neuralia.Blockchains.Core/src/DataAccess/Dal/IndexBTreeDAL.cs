//using System;
//using System.IO;
//using BTree.Serialization;
//
//namespace Blockchains.Classes.General.DAL {
//	public class IndexBTreeDAL : BTreeDAL<Guid, byte[]> {
//		public IndexBTreeDAL(string filename) : base(filename, sizeof(long) + (2 * sizeof(int)), PrimitiveSerializer.Guid, PrimitiveSerializer.Bytes) {
//		}
//
//		public bool KeyExists(Guid key) {
//			bool keyExists = false;
//
//			if(!File.Exists(this.options.FileName)) {
//				return false;
//			}
//
//			this.OpenRead(tree => {
//				keyExists = tree.ContainsKey(key);
//			});
//
//			return keyExists;
//		}
//
//		public bool SaveIndex(Guid key, long start, int hashlength, int transactionLength) {
//			if(this.KeyExists(key)) {
//				return false;
//			}
//
//			// transform our indexes into an array
//			byte[] indexes = new byte[sizeof(long) + (sizeof(int) * 2)];
//
//			byte[] longBytes = BitConverter.GetBytes(start);
//			Buffer.BlockCopy(longBytes, 0, indexes, 0, longBytes.Length);
//			longBytes = BitConverter.GetBytes(hashlength);
//			Buffer.BlockCopy(longBytes, 0, indexes, longBytes.Length, longBytes.Length);
//			longBytes = BitConverter.GetBytes(transactionLength);
//			Buffer.BlockCopy(longBytes, 0, indexes, longBytes.Length * 2, longBytes.Length);
//
//			this.OpenReadWrite(tree => {
//				tree.Add(key, indexes);
//			});
//
//			return true;
//		}
//
//		public (long, int, int)? FindIndex(Guid key) {
//			byte[] indexes = null;
//
//			this.OpenRead(tree => {
//				if(!tree.TryGetValue(key, out indexes)) {
//					// failed to retreive
//					//TODO: error handling here   
//				}
//			});
//
//			if(indexes == null) {
//				return null;
//			}
//
//			// transform back our indexes
//			long start = BitConverter.ToInt64(indexes, 0);
//			int hashLength = BitConverter.ToInt32(indexes, sizeof(long));
//			int transactionLength = BitConverter.ToInt32(indexes, sizeof(long) + sizeof(int));
//
//			return (start, hashLength, transactionLength);
//		}
//	}
//}

