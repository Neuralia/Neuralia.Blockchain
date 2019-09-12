using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography {
	public static class HashingUtils {

		public static readonly Sha3SakuraTree Hasher3 = new Sha3SakuraTree(512);
		public static readonly Sha2SakuraTree Hasher2 = new Sha2SakuraTree(512);

		public static readonly Sha3SakuraTree Hasher3256 = new Sha3SakuraTree(256);
		public static readonly Sha2SakuraTree Hasher2256 = new Sha2SakuraTree(256);

		public static readonly xxHashSakuraTree XxhasherTree = new xxHashSakuraTree();
		public static readonly xxHashSakuraTree32 XxhasherTree32 = new xxHashSakuraTree32();

		public static readonly xxHasher64 XxHasher64 = new xxHasher64();
		public static readonly xxHasher32 XxHasher32 = new xxHasher32();

		private static readonly object Hasher3Locker = new object();
		private static readonly object Hasher2Locker = new object();
		private static readonly object Hasher3256Locker = new object();
		private static readonly object Hasher2256Locker = new object();

		private static readonly object XxhasherTreeLocker = new object();
		private static readonly object XxhasherTree32Locker = new object();

		private static readonly object XxHasher64Locker = new object();
		private static readonly object XxHasher32Locker = new object();

		public static long XxHash64(IByteArray data) {
			lock(XxHasher64Locker) {
				return XxHasher64.Hash(data);
			}
		}

		public static int XxHash32(IByteArray data) {
			lock(XxHasher32Locker) {
				return XxHasher32.Hash(data);
			}
		}

		public static bool ValidateGossipMessageSetHash(IGossipMessageSet gossipMessageSet) {

			HashNodeList structure = gossipMessageSet.GetStructuresArray();

			long ownHash = 0;

			lock(XxhasherTreeLocker) {
				ownHash = XxhasherTree.HashLong(structure);
			}

			return ownHash == gossipMessageSet.BaseHeader.Hash;
		}

		public static void HashGossipMessageSet(IGossipMessageSet gossipMessageSet) {

			if(!gossipMessageSet.MessageCreated) {
				throw new ApplicationException("Message must have been created and be valid");
			}

			HashNodeList structure = gossipMessageSet.GetStructuresArray();

			lock(XxhasherTreeLocker) {
				((IGossipMessageRWSet) gossipMessageSet).RWBaseHeader.Hash = XxhasherTree.HashLong(structure);
			}
		}

		public static (IByteArray sha2, IByteArray sha3) HashSecretKey(byte[] publicKey) {

			// sha2
			BinarySliceHashNodeList sliceHashNodeList = new BinarySliceHashNodeList(publicKey);

			IByteArray sha2 = null;
			
			lock(Hasher2Locker) {
				sha2 = Hasher2.Hash(sliceHashNodeList);
			}

			IByteArray sha3 = null;
			// sha3

			lock(Hasher3Locker) {
				sha3 = Hasher3.Hash(sliceHashNodeList);
			}

			return (sha2, sha3);

		}

		public static (IByteArray sha2, IByteArray sha3, int nonceHash) HashSecretComboKey(byte[] publicKey, long promisedNonce1, long promisedNonce2) {

			// sha2
			BinarySliceHashNodeList sliceHashNodeList = new BinarySliceHashNodeList(publicKey);

			IByteArray sha2 = null;
			HashNodeList hashNodeList = new HashNodeList();

			IByteArray hash2 = null;
			lock(Hasher2Locker) {
				hash2 = Hasher2.Hash(sliceHashNodeList);
			}

			hashNodeList.Add(hash2);
			hashNodeList.Add(promisedNonce1);
			hashNodeList.Add(promisedNonce2);
			
			lock(Hasher2Locker) {
				sha2 = Hasher2.Hash(hashNodeList);
			}
			
			hash2.Return();
			

			IByteArray sha3 = null;
			// sha3
			hashNodeList = new HashNodeList();

			IByteArray hash3 = null;
			lock(Hasher3Locker) {
				hash3 = Hasher3.Hash(sliceHashNodeList);
			}

			hashNodeList.Add(hash3);
			hashNodeList.Add(promisedNonce1);
			hashNodeList.Add(promisedNonce2);

			lock(Hasher3Locker) {
				sha3 = Hasher3.Hash(hashNodeList);
			}
			
			hash3.Return();
			

			int nonceHash = 0;
			hashNodeList = new HashNodeList();
			hashNodeList.Add(promisedNonce1);
			hashNodeList.Add(promisedNonce2);

			lock(XxhasherTree32Locker) {
				nonceHash = XxhasherTree32.HashInt(hashNodeList);
			}
		

			return (sha2, sha3, nonceHash);

		}

		public static (IByteArray sha2, IByteArray sha3) GenerateDualHash(ITreeHashable hashable) {
			IByteArray hash3 = null;
			IByteArray hash2 = null;

			HashNodeList structure = hashable.GetStructuresArray();

			lock(Hasher3Locker) {
				hash3 = Hasher3.Hash(structure);
			}

			lock(Hasher2Locker) {
				//TODO: make sure that reusing the structure is non destructive. otherwise this could be an issue
				hash2 = Hasher2.Hash(structure);
			}

			return (hash2, hash3);
		}

		public static IByteArray GenerateDualHashCombined(ITreeHashable hashable) {

			(IByteArray sha2, IByteArray sha3) results = GenerateDualHash(hashable);

			IByteArray result = DataSerializationFactory.CreateDehydrator().WriteNonNullable(results.sha2).WriteNonNullable(results.sha3).ToArray();

			results.sha2.Return();
			results.sha3.Return();

			return result;
		}

		public static (IByteArray sha2, IByteArray sha3) ExtractCombinedDualHash(IByteArray combinedHash) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(combinedHash);

			IByteArray sha2 = rehydrator.ReadNonNullableArray();
			IByteArray sha3 = rehydrator.ReadNonNullableArray();

			return (sha2, sha3);
		}

		public static bool VerifyCombinedHash(IByteArray hash, IByteArray sha2, IByteArray sha3) {

			Sha512Hasher sha512Hasher = new Sha512Hasher();
			IByteArray newsha2 = sha512Hasher.Hash(hash);

			bool result = newsha2.Equals(sha2);
			newsha2.Return();
			
			if(!result) {
				return false;
			}

			Sha3_512Hasher sha3Hasher = new Sha3_512Hasher();
			IByteArray newsha3 = sha3Hasher.Hash(hash);

			result = newsha3.Equals(sha3);
			
			newsha3.Return();

			return result;
		}

		public static IByteArray GenerateHash(ITreeHashable hashable) {

			HashNodeList structure = hashable.GetStructuresArray();

			lock(Hasher3Locker) {
				return Hasher3.Hash(structure);
			}
			
		}

		public static IByteArray GenerateHash256(ITreeHashable hashable) {
			HashNodeList structure = hashable.GetStructuresArray();

			lock(Hasher3256Locker) {
				return Hasher3256.Hash(structure);
			}
			
		}

		public static long Generate_xxHash(ITreeHashable hashable) {
			HashNodeList structure = hashable.GetStructuresArray();

			lock(XxhasherTreeLocker) {
				return XxhasherTree.HashLong(structure);
			}
		}

		public static ImmutableList<IByteArray> GenerateMd5Hash(List<IByteArray> data) {

			using(MD5 md5Hash = MD5.Create()) {
				return data.Select(h => (IByteArray) (ByteArray) md5Hash.ComputeHash(h.ToExactByteArray())).ToImmutableList();
			}
		}

		public static ImmutableList<Guid> GenerateMd5GuidHash(List<IByteArray> data) {

			using(MD5 md5Hash = MD5.Create()) {
				return data.Select(h => new Guid(md5Hash.ComputeHash(h.ToExactByteArray()))).ToImmutableList();
			}
		}

		public static IByteArray GenerateMd5Hash(IByteArray data) {

			using(MD5 md5Hash = MD5.Create()) {
				return (ByteArray) md5Hash.ComputeHash(data.ToExactByteArray());
			}
		}

		public static Guid GenerateMd5GuidHash(IByteArray data) {

			using(MD5 md5Hash = MD5.Create()) {
				return new Guid(md5Hash.ComputeHash(data.ToExactByteArray()));
			}
		}

		public static Guid GenerateMd5GuidHash(in byte[] data) {

			using(MD5 md5Hash = MD5.Create()) {
				return new Guid(md5Hash.ComputeHash(data));
			}
		}

		public static Guid GenerateMd5GuidHash(Span<byte> data) {

			using(MD5 md5Hash = MD5.Create()) {
				return new Guid(md5Hash.ComputeHash(data.ToArray()));
			}
		}

		public static int GenerateBlockDataSliceHash(List<IByteArray> channels) {

			var hashes = new List<int>();

			foreach(IByteArray channelslice in channels) {
				hashes.Add(XxHash32(channelslice));
			}

			HashNodeList nodes = new HashNodeList();

			// we insert them in size order
			foreach(int hash in hashes.OrderBy(h => h)) {
				nodes.Add(hash);
			}

			lock(XxhasherTree32Locker) {
				return XxhasherTree32.HashInt(nodes);
			}
		}
	}
}