using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.BlockInsertionTransaction {

	public interface IBlockInsertionTransactionProcessor : IDisposableExtended {
		
		long PublicBlockHeight { get; set; }
		long DiskBlockHeight { get; set; }
		DateTime LastBlockTimestamp { get; set; }
		ushort LastBlockLifespan { get; set; }
		SafeArrayHandle LastBlockHash { get;  }
		int BlockInsertionStatus { get; set; }
		(long index, long startingBlockId, long endingBlockId) BlockIndex { get; set; }
		Dictionary<string, long> FileSizes { get; set; }
		SafeArrayHandle BlockIdFile { get;  }
		SafeArrayHandle ModeratorKey { get;  }
		void CreateSnapshot();
		void Commit();
		void Uncommit();
		void Rollback();
		byte ModeratorKeyOrdinal { get; }
	}

	public abstract class BlockInsertionTransactionProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IBlockInsertionTransactionProcessor
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CENTRAL_COORDINATOR centralCoordinator;

		public byte ModeratorKeyOrdinal { get; }

		private bool commited;
		private bool restored;
		
		public BlockInsertionTransactionProcessor(CENTRAL_COORDINATOR centralCoordinator, byte moderatorKeyOrdinal) {
			this.centralCoordinator = centralCoordinator;
			this.ModeratorKeyOrdinal = moderatorKeyOrdinal;
		}

		public long PublicBlockHeight { get; set; }
		public long DiskBlockHeight { get; set; }

		public DateTime LastBlockTimestamp { get; set; }
		public ushort LastBlockLifespan { get; set; }
		public SafeArrayHandle LastBlockHash { get;  } = SafeArrayHandle.Create();
		public int BlockInsertionStatus { get; set; }

		public (long index, long startingBlockId, long endingBlockId) BlockIndex { get; set; }

		public Dictionary<string, long> FileSizes { get; set; }

		public  SafeArrayHandle BlockIdFile { get;  } = SafeArrayHandle.Create();
		public  SafeArrayHandle ModeratorKey { get;  } = SafeArrayHandle.Create();

		public void CreateSnapshot() {

			this.restored = false;
			IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			this.PublicBlockHeight = chainStateProvider.PublicBlockHeight;
			this.DiskBlockHeight = chainStateProvider.DiskBlockHeight;
			this.LastBlockTimestamp = chainStateProvider.LastBlockTimestamp;
			this.LastBlockLifespan = chainStateProvider.LastBlockLifespan;
			this.LastBlockHash.Entry =  ByteArray.Wrap(chainStateProvider.LastBlockHash);
			this.BlockInsertionStatus = (int) chainStateProvider.BlockInterpretationStatus;

			if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
				this.ModeratorKey.Entry = chainStateProvider.GetModeratorKeyBytes(this.ModeratorKeyOrdinal).Entry;
			}
			else if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID) {
				this.ModeratorKey.Entry = chainStateProvider.GetModeratorKeyBytes(this.ModeratorKeyOrdinal).Entry;
			}
			
			this.BlockIndex = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FindBlockIndex(this.DiskBlockHeight);

			this.FileSizes = this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GetBlockFileSizes(this.DiskBlockHeight);

		}

		public void Commit() {
			this.commited = true;
		}

		public void Uncommit() {
			this.commited = false;
		}

		private void PerformRestore() {
			if(this.restored) {
				return;
			}
			
			IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;
				
			List<Action> actions = new List<Action>();
			
			actions.Add(() => chainStateProvider.DiskBlockHeight = this.DiskBlockHeight);
				
			actions.Add(() => chainStateProvider.PublicBlockHeight = this.PublicBlockHeight);
			actions.Add(() => chainStateProvider.LastBlockTimestamp = this.LastBlockTimestamp);
			actions.Add(() => chainStateProvider.LastBlockLifespan = this.LastBlockLifespan);
			actions.Add(() => chainStateProvider.LastBlockHash = this.LastBlockHash.ToExactByteArrayCopy());
			actions.Add(() => chainStateProvider.BlockInterpretationStatus = (ChainStateEntryFields.BlockInterpretationStatuses) this.BlockInsertionStatus);

			actions.Add(() => ((IChainDataWriteProvider) this.centralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase).TruncateBlockFileSizes(this.DiskBlockHeight, this.FileSizes));
				

			actions.Add(() => {
				if(this.BlockIdFile?.HasData ?? false) {
					FileExtensions.WriteAllBytes(this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetBlocksIdFilePath(), this.BlockIdFile, this.centralCoordinator.FileSystem);
				}
			});
			actions.Add(() => {
				if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) {
					chainStateProvider.UpdateModeratorKey(new TransactionId(), this.ModeratorKeyOrdinal, this.ModeratorKey);
				}
				else if(this.ModeratorKeyOrdinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID) {
					chainStateProvider.UpdateModeratorKey(new TransactionId(),  this.ModeratorKeyOrdinal, this.ModeratorKey);
				}
			});
				
			IndependentActionRunner.Run(actions);

			this.restored = true;
		}
		public void Rollback() {
			if(!this.commited) {

				this.PerformRestore();
			}
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