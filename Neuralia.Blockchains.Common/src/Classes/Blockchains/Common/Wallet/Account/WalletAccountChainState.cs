using System;
using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccountChainState {

		[BsonId]
		string AccountCode { get; set; }

		long LastBlockSynced { get; set; }
		long PreviousBlockSynced { get; set; }
		
		int BlockSyncStatus { get; set; }

		Dictionary<byte, IWalletAccountChainStateKey> Keys { get; set; }
		
	
	}

	public abstract class WalletAccountChainState : IWalletAccountChainState {

		[Flags]
		public enum BlockSyncStatuses {
			Blank = 0,
			BlockHeightUpdated = 1 << 0,
			KeyLogSynced = 1 << 1,
			WalletImmediateImpactPerformed = 1 << 2,
			SnapshotInterpretationDone = 1 << 3,
			InterpretationCompleted = SnapshotInterpretationDone,
			FullySyncedBlockHeightNotUpdated = KeyLogSynced | WalletImmediateImpactPerformed | InterpretationCompleted,
			FullySynced = BlockHeightUpdated | FullySyncedBlockHeightNotUpdated
		}

		static WalletAccountChainState() {
			LiteDBMappers.RegisterGuidDictionary<IWalletAccountChainStateKey>();
		}

		[BsonId]
		public string AccountCode { get; set; }

		/// <summary>
		/// the last actual block that we synced
		/// </summary>
		public long LastBlockSynced { get; set; } = 0;
		
		/// <summary>
		/// the block that we had synced before that
		/// </summary>
		public long PreviousBlockSynced { get; set; } = 0;
		
		/// <summary>
		///     The status of the last block insertion
		/// </summary>
		public int BlockSyncStatus { get; set; } = (int) BlockSyncStatuses.FullySynced;

		public Dictionary<byte, IWalletAccountChainStateKey> Keys { get; set; } = new Dictionary<byte, IWalletAccountChainStateKey>();
		
	}
}