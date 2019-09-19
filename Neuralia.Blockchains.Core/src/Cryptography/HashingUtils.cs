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


		public static SafeArrayHandle Hash2(IHashNodeList sliceHashNodeList) {
			using(Sha2SakuraTree Hasher2 = new Sha2SakuraTree(512)) {
				return Hasher2.Hash(sliceHashNodeList);
			}
		}
		
		public static SafeArrayHandle Hash3(IHashNodeList sliceHashNodeList) {
			using(Sha3SakuraTree Hasher3 = new Sha3SakuraTree(512)) {
				return Hasher3.Hash(sliceHashNodeList);
			}
		}
		
		public static long HashxxTree(IHashNodeList sliceHashNodeList) {
			xxHashSakuraTree XxhasherTree = new xxHashSakuraTree();
			return XxhasherTree.HashLong(sliceHashNodeList);
		}
		
		public static int HashxxTree32(IHashNodeList sliceHashNodeList) {
			xxHashSakuraTree32 XxhasherTree32 = new xxHashSakuraTree32();
			return XxhasherTree32.HashInt(sliceHashNodeList);
		}

		
		public static long XxHash64(SafeArrayHandle data) {
			xxHasher64 XxHasher64 = new xxHasher64();
			return XxHasher64.Hash(data);
		}

		public static int XxHash32(SafeArrayHandle data) {
			xxHasher32 XxHasher32 = new xxHasher32();
			return XxHasher32.Hash(data);
		}
			

		public static bool ValidateGossipMessageSetHash(IGossipMessageSet gossipMessageSet) {
			
			using(HashNodeList structure = gossipMessageSet.GetStructuresArray()) {
				long ownHash = HashxxTree(structure);
				
				return ownHash == gossipMessageSet.BaseHeader.Hash;
			}
		}

		public static void HashGossipMessageSet(IGossipMessageSet gossipMessageSet) {

			if(!gossipMessageSet.MessageCreated) {
				throw new ApplicationException("Message must have been created and be valid");
			}

			using(HashNodeList structure = gossipMessageSet.GetStructuresArray()) {
				
				((IGossipMessageRWSet) gossipMessageSet).RWBaseHeader.Hash = HashxxTree(structure);
			}
		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3) HashSecretKey(byte[] publicKey) {

			SafeArrayHandle sha2 = null;
			SafeArrayHandle sha3 = null;


			BinarySliceHashNodeList sliceHashNodeList = new BinarySliceHashNodeList(publicKey);
			// sha2
			sha2 =Hash2(sliceHashNodeList);
			
			// sha3
			sha3 = Hash3(sliceHashNodeList);

			return (sha2, sha3);

		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) HashSecretComboKey(byte[] publicKey, long promisedNonce1, long promisedNonce2) {

			// sha2
			BinarySliceHashNodeList sliceHashNodeList = new BinarySliceHashNodeList(publicKey);

			SafeArrayHandle sha2 = null;

			using(HashNodeList hashNodeList = new HashNodeList()) {

				SafeArrayHandle hash2 = Hash2(sliceHashNodeList);
				
				hashNodeList.Add(hash2);
				hashNodeList.Add(promisedNonce1);
				hashNodeList.Add(promisedNonce2);


				sha2 = Hash2(hashNodeList);

				hash2.Return();
			}


			SafeArrayHandle sha3 = null;

			// sha3
			using(HashNodeList hashNodeList = new HashNodeList()) {

				SafeArrayHandle hash3 = Hash3(sliceHashNodeList);
				

				hashNodeList.Add(hash3);
				hashNodeList.Add(promisedNonce1);
				hashNodeList.Add(promisedNonce2);

				sha3 = Hash3(hashNodeList);

				hash3.Return();
			}


			int nonceHash = 0;

			using(HashNodeList hashNodeList = new HashNodeList()) {
				hashNodeList.Add(promisedNonce1);
				hashNodeList.Add(promisedNonce2);

				nonceHash = HashxxTree32(hashNodeList);
			}

			return (sha2, sha3, nonceHash);
		

		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3) GenerateDualHash(ITreeHashable hashable) {
			SafeArrayHandle hash3 = null;
			SafeArrayHandle hash2 = null;

			using(HashNodeList structure = hashable.GetStructuresArray()) {

				hash3 = Hash3(structure);
				
				//TODO: make sure that reusing the structure is non destructive. otherwise this could be an issue
				hash2 = Hash2(structure);
			}

			return (hash2, hash3);
		}

		public static SafeArrayHandle GenerateDualHashCombined(ITreeHashable hashable) {

			(SafeArrayHandle sha2, SafeArrayHandle sha3) results = GenerateDualHash(hashable);

			SafeArrayHandle result = DataSerializationFactory.CreateDehydrator().WriteNonNullable(results.sha2).WriteNonNullable(results.sha3).ToArray();

			results.sha2.Return();
			results.sha3.Return();

			return result;
		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3) ExtractCombinedDualHash(SafeArrayHandle combinedHash) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(combinedHash)) {

				SafeArrayHandle sha2 = rehydrator.ReadNonNullableArray();
				SafeArrayHandle sha3 = rehydrator.ReadNonNullableArray();

				return (sha2, sha3);
			}
		}

		public static bool VerifyCombinedHash(SafeArrayHandle hash, SafeArrayHandle sha2, SafeArrayHandle sha3) {

			Sha512Hasher sha512Hasher = new Sha512Hasher();
			SafeArrayHandle newsha2 = sha512Hasher.Hash(hash);

			bool result = newsha2.Equals(sha2);
			newsha2.Return();
			
			if(!result) {
				return false;
			}

			Sha3_512Hasher sha3Hasher = new Sha3_512Hasher();
			SafeArrayHandle newsha3 = sha3Hasher.Hash(hash);

			result = newsha3.Equals(sha3);
			
			newsha3.Return();

			return result;
		}

		public static SafeArrayHandle GenerateHash(ITreeHashable hashable) {

			using(HashNodeList structure = hashable.GetStructuresArray()) {
				return Hash3(structure);
			}
		}

		public static SafeArrayHandle GenerateHash256(ITreeHashable hashable) {
			using(HashNodeList structure = hashable.GetStructuresArray()) {

				using(Sha3SakuraTree Hasher3256 = new Sha3SakuraTree(256)) {
					return Hasher3256.Hash(structure);
				}
			}
		}

		public static long Generate_xxHash(ITreeHashable hashable) {
			using(HashNodeList structure = hashable.GetStructuresArray()) {
				
				return HashxxTree(structure);
			}
		}

		public static ImmutableList<SafeArrayHandle> GenerateMd5Hash(List<SafeArrayHandle> data) {

			using(MD5 md5Hash = MD5.Create()) {
				return data.Select(h => (SafeArrayHandle) md5Hash.ComputeHash(h.ToExactByteArray())).ToImmutableList();
			}
		}

		public static ImmutableList<Guid> GenerateMd5GuidHash(List<SafeArrayHandle> data) {

			using(MD5 md5Hash = MD5.Create()) {
				return data.Select(h => new Guid(md5Hash.ComputeHash(h.ToExactByteArray()))).ToImmutableList();
			}
		}

		public static SafeArrayHandle GenerateMd5Hash(SafeArrayHandle data) {

			using(MD5 md5Hash = MD5.Create()) {
				return (ByteArray) md5Hash.ComputeHash(data.ToExactByteArray());
			}
		}

		public static Guid GenerateMd5GuidHash(SafeArrayHandle data) {

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

		public static int GenerateBlockDataSliceHash(List<SafeArrayHandle> channels) {

			var hashes = new List<int>();

			foreach(SafeArrayHandle channelslice in channels) {
				hashes.Add(XxHash32(channelslice));
			}

			using(HashNodeList nodes = new HashNodeList()) {

				// we insert them in size order
				foreach(int hash in hashes.OrderBy(h => h)) {
					nodes.Add(hash);
				}
				
				return HashxxTree32(nodes);
			}
		}
	}
}