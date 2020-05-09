using System;
using System.Collections.Generic;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState {
	public interface IChainStateEntryFields {

		/// <summary>
		///     The date of the first genesisModeratorAccountPresentation transaction
		/// </summary>
		public DateTime ChainInception { get; set; }

		/// <summary>
		///     The hash of the last block received
		/// </summary>
		public byte[] LastBlockHash { get; set; }

		/// <summary>
		///     We keep the genesis block has, its too nice :)
		/// </summary>
		public byte[] GenesisBlockHash { get; set; }

		/// <summary>
		///     The current indexed (interpreted) block height
		/// </summary>
		public long BlockHeight { get; set; }

		/// <summary>
		///     the block height saved on disk
		/// </summary>
		public long DiskBlockHeight { get; set; }

		/// <summary>
		///     the block height downloaded to disk
		/// </summary>
		public long DownloadBlockHeight { get; set; }

		/// <summary>
		///     the last timestamp of the last block
		/// </summary>
		public DateTime LastBlockTimestamp { get; set; }

		/// <summary>
		///     amount of time in increments of 10 seconds in which we should be expecting the next block.
		///     0 means infinite.
		/// </summary>
		public ushort LastBlockLifespan { get; set; }

		/// <summary>
		///     The status of the last block insertion
		/// </summary>
		public ChainStateEntryFields.BlockInterpretationStatuses BlockInterpretationStatus { get; set; }

		/// <summary>
		///     The latest public block height we have found. if its further ahead then BlockHeight, then we need to sync
		/// </summary>
		public long PublicBlockHeight { get; set; }

		/// <summary>
		///     our own local digest height
		/// </summary>
		public int DigestHeight { get; set; }

		/// <summary>
		///     the last block height that is governed by the digest
		/// </summary>
		public long DigestBlockHeight { get; set; }

		/// <summary>
		///     The hash of the last block received
		/// </summary>
		public byte[] LastDigestHash { get; set; }

		/// <summary>
		///     the last timestamp of the last digest
		/// </summary>
		public DateTime LastDigestTimestamp { get; set; }

		/// <summary>
		///     The latest public digest height we have found. if its further ahead then DigestHeight, then we need to sync
		/// </summary>
		public int PublicDigestHeight { get; set; }

		/// <summary>
		///     The time that we last attempted a sync
		/// </summary>
		public DateTime LastSync { get; set; }

		// now various settingsBase set by the moderators to improve performance

		/// <summary>
		///     The maximum client version allowed on the network
		/// </summary>
		public string MaximumVersionAllowed { get; set; }

		/// <summary>
		///     minimum version which is still accepted, but produces a warning.
		/// </summary>
		public string MinimumWarningVersionAllowed { get; set; }

		/// <summary>
		///     The minimum client version allowed on the network
		/// </summary>
		public string MinimumVersionAllowed { get; set; }

		/// <summary>
		///     The average amount of time in seconds between each block. Allows to help determine how often to check for syncing.
		/// </summary>
		public int MaxBlockInterval { get; set; }

		/// <summary>
		///     are presentations allowed to pass on gossip
		/// </summary>
		public bool AllowGossipPresentations { get; set; }

		public long MiningPassword { get; set; }

		public byte[] MiningAutograph { get; set; }

		public DateTime? LastMiningRegistrationUpdate { get; set; }
	}

	public interface IChainStateEntry : IChainStateEntryFields {
	}

	public interface IChainStateEntry<in MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateEntry
		where MODEL_SNAPSHOT : class, IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		int Id { get; set; }

		List<MODERATOR_KEYS_SNAPSHOT> ModeratorKeys { get; set; }

		void Copy(MODEL_SNAPSHOT other);
	}

	public static class ChainStateEntryFields {
		[Flags]
		public enum BlockInterpretationStatuses {
			Blank = 0,
			SnapshotInterpretationDone = 1 << 0,
			InterpretationSerializationDone = 1 << 1,
			ImmediateImpactDone = 1 << 2,

			FullSnapshotInterpretationCompleted = InterpretationSerializationDone | SnapshotInterpretationDone,
			InterpretationCompleted = FullSnapshotInterpretationCompleted | ImmediateImpactDone

		}
	}
}