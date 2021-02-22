using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Nito.AsyncEx.Synchronous;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction {

	public interface IBlockInsertionTransactionProcessor : IDisposableExtended {

		long PublicBlockHeight { get; set; }
		long DiskBlockHeight { get; set; }
		long BlockHeight { get; set; }

		DateTime LastBlockTimestamp { get; set; }
		ushort LastBlockLifespan { get; set; }
		SafeArrayHandle LastBlockHash { get; }
		int BlockInsertionStatus { get; set; }
		(long index, long startingBlockId, long endingBlockId) BlockIndex { get; set; }
		Dictionary<string, long> FileSizes { get; set; }
		SafeArrayHandle BlockIdFile { get; }
		SafeArrayHandle ModeratorKey { get; }
		KeyUseIndexSet ModeratorKeyIndex { get; }
		byte ModeratorKeyOrdinal { get; }
		Task CreateSnapshot();
		void Commit();
		void UnCommit();
		void Rollback();
	}

	public abstract class BlockInsertionTransactionProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockInsertionTransactionProcessor
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CENTRAL_COORDINATOR centralCoordinator;

		private bool commited;
		private bool restored;

		public BlockInsertionTransactionProcessor(CENTRAL_COORDINATOR centralCoordinator, byte moderatorKeyOrdinal) {
			this.centralCoordinator = centralCoordinator;
			this.ModeratorKeyOrdinal = moderatorKeyOrdinal;
		}

		public byte ModeratorKeyOrdinal { get; }

		public long PublicBlockHeight { get; set; }
		public long DiskBlockHeight { get; set; }
		public long BlockHeight { get; set; }

		public DateTime LastBlockTimestamp { get; set; }
		public ushort LastBlockLifespan { get; set; }
		public SafeArrayHandle LastBlockHash { get; } = SafeArrayHandle.Create();
		public int BlockInsertionStatus { get; set; }

		public (long index, long startingBlockId, long endingBlockId) BlockIndex { get; set; }

		public Dictionary<string, long> FileSizes { get; set; }

		public SafeArrayHandle BlockIdFile { get; } = SafeArrayHandle.Create();
		public SafeArrayHandle ModeratorKey { get; } = SafeArrayHandle.Create();
		public KeyUseIndexSet ModeratorKeyIndex { get; set; } = new KeyUseIndexSet();

		public async Task CreateSnapshot() {

			this.restored = false;
			IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			this.PublicBlockHeight = chainStateProvider.PublicBlockHeight;
			this.DiskBlockHeight = chainStateProvider.DiskBlockHeight;
			this.BlockHeight = chainStateProvider.BlockHeight;

			this.LastBlockTimestamp = chainStateProvider.LastBlockTimestamp;
			this.LastBlockLifespan = chainStateProvider.LastBlockLifespan;
			this.LastBlockHash.Entry = ByteArray.WrapAndOwn(chainStateProvider.LastBlockHash);
			this.BlockInsertionStatus = (int) chainStateProvider.BlockInterpretationStatus;

			// if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
			// 	this.ModeratorKey.Entry = chainStateProvider.GetModeratorKeyBytes(this.ModeratorKeyOrdinal).Entry;
			// } else 
			if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID) {
				this.ModeratorKey.Entry = (await chainStateProvider.GetModeratorKeyBytes(ModeratorKeyOrdinal).ConfigureAwait(false)).Entry;
				this.ModeratorKeyIndex = await chainStateProvider.GetModeratorKeyIndex(this.ModeratorKeyOrdinal).ConfigureAwait(false);
			}

			this.BlockIndex = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FindBlockIndex(this.DiskBlockHeight);

			this.FileSizes = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockFileSizes(this.DiskBlockHeight);

		}

		public void Commit() {
			this.commited = true;
		}

		public void UnCommit() {
			this.commited = false;
		}

		public void Rollback() {
			if(!this.commited) {

				this.PerformRestore();
			}
		}

		private void PerformRestore() {
			if(this.restored) {
				return;
			}

			IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			List<Action> actions = new List<Action>();

			actions.Add(() => chainStateProvider.PublicBlockHeight = this.PublicBlockHeight);
			actions.Add(() => chainStateProvider.DiskBlockHeight = this.DiskBlockHeight);
			actions.Add(() => chainStateProvider.BlockHeight = this.BlockHeight);

			actions.Add(() => chainStateProvider.LastBlockTimestamp = this.LastBlockTimestamp);
			actions.Add(() => chainStateProvider.LastBlockLifespan = this.LastBlockLifespan);
			actions.Add(() => {

				if(this.LastBlockHash.IsZero) {
					chainStateProvider.LastBlockHash = null;
				} else {
					chainStateProvider.LastBlockHash = this.LastBlockHash.ToExactByteArrayCopy();
				}
			});
			actions.Add(() => chainStateProvider.BlockInterpretationStatus = (ChainStateEntryFields.BlockInterpretationStatuses) this.BlockInsertionStatus);

			actions.Add(() => ((IChainDataWriteProvider) this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase).TruncateBlockFileSizes(this.DiskBlockHeight, this.FileSizes));

			actions.Add(() => {
				if(this.BlockIdFile?.HasData ?? false) {
					FileExtensions.WriteAllBytes(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetBlocksIdFilePath(), this.BlockIdFile, this.centralCoordinator.FileSystem);
				}
			});

			actions.Add(() => {
				// if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
				// 	chainStateProvider.UpdateModeratorKey(new TransactionId(), this.ModeratorKeyOrdinal, this.ModeratorKey);
				// } else 
				if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID) {
					chainStateProvider.UpdateModeratorKey(new TransactionId(), this.ModeratorKeyOrdinal, this.ModeratorKey, false).WaitAndUnwrapException();
					chainStateProvider.UpdateModeratorExpectedNextKeyIndex(this.ModeratorKeyOrdinal, this.ModeratorKeyIndex.KeyUseSequenceId.Value, this.ModeratorKeyIndex.KeyUseIndex).WaitAndUnwrapException();
				}
			});

			IndependentActionRunner.Run(actions);

			this.restored = true;
		}

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if(!this.IsDisposed && disposing) {

				this.Rollback();
			}

			this.IsDisposed = true;
		}

		~BlockInsertionTransactionProcessor() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}