using System;
using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletAccountChainState {

		[BsonId]
		Guid AccountUuid { get; set; }

		long LastBlockSynced { get; set; }

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
			FullySynced = BlockHeightUpdated | KeyLogSynced | WalletImmediateImpactPerformed | InterpretationCompleted
		}

		static WalletAccountChainState() {
			LiteDBMappers.RegisterGuidDictionary<IWalletAccountChainStateKey>();
		}

		[BsonId]
		public Guid AccountUuid { get; set; }

		public long LastBlockSynced { get; set; } = 0;

		/// <summary>
		///     The status of the last block insertion
		/// </summary>
		public int BlockSyncStatus { get; set; } = (int)BlockSyncStatuses.FullySynced;

		public Dictionary<byte, IWalletAccountChainStateKey> Keys { get; set; } = new Dictionary<byte, IWalletAccountChainStateKey>();
	}
}