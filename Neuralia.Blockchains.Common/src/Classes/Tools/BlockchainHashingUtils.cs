using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public static class BlockchainHashingUtils {

		public static SafeArrayHandle GenesisBlockHash => SafeArrayHandle.Create();

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3) HashSecretKey(ISecretWalletKey secretWalletKey) {

			return HashingUtils.HashSecretKey(secretWalletKey.PublicKey);
		}

		public static (SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) HashSecretComboKey(ISecretComboWalletKey secretWalletKey) {

			return HashingUtils.HashSecretComboKey(secretWalletKey.PublicKey, secretWalletKey.PromisedNonce1, secretWalletKey.PromisedNonce2);
		}

		/// <summary>
		///     as an optimization in case of large transaction count, we hash each transaction individually and then hash the
		///     hashes together. this prevents HUGE block structures from being generated.
		/// </summary>
		/// <param name="transactions"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static HashNodeList GenerateTransactionSetNodeList<T>(IEnumerable<T> transactions)
			where T : ITransaction {

			HashNodeList results = new HashNodeList();

			BlockHashingState state = new BlockHashingState();

			foreach(T transactionEnvelope in transactions.OrderBy(t => t.TransactionId)) {

				// perform lazy loading of the transaction hash.
				//note: this feature is important, as it will allow us to perform yielding of hashing, and clear memory as we go instead of keeping everything all at once.
				results.Add(new HashNodeList.LazyHashNode<T, BlockHashingState>(transactionEnvelope, (t, s) => {

					Sha3SakuraTree hasher = null;

					try {
						hasher = s.HasherPool.GetObject();

						return GenerateBlockTransactionHash(transactionEnvelope, hasher);
					} finally {
						if(hasher != null) {
							s.HasherPool.PutObject(hasher);
						}
					}
				}, state));
			}

			return results;
		}

		public static SafeArrayHandle GenerateRejectedTransactionHash(RejectedTransaction transaction) {

			using HashNodeList structures = transaction.GetStructuresArray();

			return HashingUtils.Hash3(structures);

		}

		public static SafeArrayHandle GenerateRejectedTransactionHash(RejectedTransaction transaction, Sha3SakuraTree hasher) {

			using HashNodeList structures = transaction.GetStructuresArray();

			return HashingUtils.Hash3(structures, hasher);

		}

		public static HashNodeList GenerateRejectedTransactionSetNodeList(IEnumerable<RejectedTransaction> rejectedTransactions) {

			HashNodeList results = new HashNodeList();

			BlockHashingState state = new BlockHashingState();

			foreach(RejectedTransaction rejectedTransaction in rejectedTransactions.OrderBy(t => t.TransactionId)) {

				// perform lazy loading of the transaction hash.
				//note: this feature is important, as it will allow us to perform yielding of hashing, and clear memory as we go instead of keeping everything all at once.
				results.Add(new HashNodeList.LazyHashNode<RejectedTransaction, BlockHashingState>(rejectedTransaction, (t, s) => {

					Sha3SakuraTree hasher = null;

					try {
						hasher = s.HasherPool.GetObject();

						return GenerateRejectedTransactionHash(rejectedTransaction, hasher);
					} finally {
						if(hasher != null) {
							s.HasherPool.PutObject(hasher);
						}
					}
				}, state));
			}

			return results;
		}

		public static SafeArrayHandle GenerateTransactionHash(ITransactionEnvelope envelope, AccountId multiSizgAccountId) {

			return GenerateTransactionHash(envelope, envelope.Contents.RehydratedTransaction, multiSizgAccountId);
		}

		public static SafeArrayHandle GenerateTransactionHash(ITransactionEnvelope envelope, ITransaction transaction, AccountId multiSizgAccountId) {

			using HashNodeList structures = transaction.GetStructuresArrayMultiSig(multiSizgAccountId);

			structures.Add(envelope.GetTransactionHashingStructuresArray());

			return HashingUtils.Hash3(structures);

		}

		public static SafeArrayHandle GenerateTransactionHash(ITransactionEnvelope envelope) {
			return GenerateTransactionHash(envelope, envelope.Contents.RehydratedTransaction);
		}

		public static SafeArrayHandle GenerateTransactionHash(ITransactionEnvelope envelope, ITransaction transaction) {
			using HashNodeList structures = transaction.GetStructuresArray();

			structures.Add(envelope.GetTransactionHashingStructuresArray());

			return HashingUtils.Hash3(structures);

		}

		public static SafeArrayHandle GenerateTransactionHash(ITransactionEnvelope envelope, Sha3SakuraTree hasher) {
			using HashNodeList structures = envelope.Contents.RehydratedTransaction.GetStructuresArray();

			structures.Add(envelope.GetTransactionHashingStructuresArray());

			return HashingUtils.Hash3(structures, hasher);

		}

		public static SafeArrayHandle GenerateBlockTransactionHash(ITransaction transaction) {

			using HashNodeList structures = transaction.GetCompleteStructuresArray();

			return HashingUtils.Hash3(structures);

		}

		public static SafeArrayHandle GenerateBlockTransactionHash(ITransaction transaction, Sha3SakuraTree hasher) {
			using HashNodeList structures = transaction.GetCompleteStructuresArray();

			return HashingUtils.Hash3(structures, hasher);

		}

		public static SafeArrayHandle GenerateBlockHash(IBlock block, SafeArrayHandle previousBlockHash) {

			if(block.BlockHashingMode == Enums.BlockHashingModes.Mode1) {
				using HashNodeList structures = block.GetStructuresArray(previousBlockHash);

				return HashingUtils.Hash3(structures);

			}

			throw new ApplicationException("Unsopported block hashing mode");
		}

		public static SafeArrayHandle GenerateGenesisBlockHash(IGenesisBlock genesisBlock) {

			if(genesisBlock.BlockHashingMode == Enums.BlockHashingModes.Mode1) {
				using HashNodeList structures = genesisBlock.GetStructuresArray(GenesisBlockHash);

				return HashingUtils.Hash3(structures);

			}

			throw new ApplicationException("Unsopported block hashing mode");
		}

		// election results:

		public static HashNodeList GenerateFinalElectionResultNodeList<T>(Dictionary<AccountId, T> accounts)
			where T : ITreeHashable {

			HashNodeList results = new HashNodeList();

			BlockHashingState state = new BlockHashingState();

			foreach(KeyValuePair<AccountId, T> entry in accounts.OrderBy(e => e.Key)) {

				// perform lazy loading of the elected info hash.
				//note: this feature is important, as it will allow us to perform yielding of hashing, and clear memory as we go instead of keeping everything all at once.
				results.Add(new HashNodeList.LazyHashNode<KeyValuePair<AccountId, T>, BlockHashingState>(entry, (e, s) => {

					Sha3SakuraTree hasher = null;

					try {
						hasher = s.HasherPool.GetObject();

						using HashNodeList structures = new HashNodeList();

						structures.Add(entry.Key);
						structures.Add(entry.Value);

						return HashingUtils.Hash3(structures, hasher);

					} finally {
						if(hasher != null) {
							s.HasherPool.PutObject(hasher);
						}
					}
				}, state));
			}

			return results;
		}

		public static int BlockxxHash(IBlock block) {
			return BlockxxHash(block.Hash);
		}

		public static int BlockxxHash(BlockElectionDistillate blockDistillate) {
			return BlockxxHash(blockDistillate.blockHash);
		}

		public static int BlockxxHash(SafeArrayHandle blockHash) {
			return HashingUtils.XxHash32(blockHash);
		}

		/// <summary>
		///     a state for lazy loading of transaction hashing
		/// </summary>
		private class BlockHashingState {
			public ObjectPool<Sha3SakuraTree> HasherPool { get; } = new ObjectPool<Sha3SakuraTree>(() => new Sha3SakuraTree(Enums.ThreadMode.Single), 2, 2);
		}
	}
}