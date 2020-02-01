using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainState {

	public interface IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
	}

	/// <summary>
	///     Here we store various metadata state about our chain
	/// </summary>
	public abstract class ChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new()
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>, new() {

		[Key]
		public int Id { get; set; }

		/// <summary>
		///     The date of the first genesisModeratorAccountPresentation transaction
		/// </summary>
		[Required]
		public DateTime ChainInception { get; set; } = DateTime.MinValue;

		public byte[] LastBlockHash { get; set; }
		public byte[] GenesisBlockHash { get; set; }

		/// <summary>
		///     The date of the first genesisModeratorAccountPresentation transaction
		/// </summary>
		[Required]
		public long BlockHeight { get; set; }

		[Required]
		public long DiskBlockHeight { get; set; }

		[Required]
		public long DownloadBlockHeight { get; set; }

		public DateTime LastBlockTimestamp { get; set; } = DateTime.MinValue;
		public ushort LastBlockLifespan { get; set; }
		public ChainStateEntryFields.BlockInterpretationStatuses BlockInterpretationStatus { get; set; }

		/// <summary>
		///     The latest public block height we have found. if its further ahead then BlockHeight, then we need to sync
		/// </summary>
		[Required]
		public long PublicBlockHeight { get; set; }

		[Required]
		public int DigestHeight { get; set; }

		public long DigestBlockHeight { get; set; }

		public byte[] LastDigestHash { get; set; }
		public DateTime LastDigestTimestamp { get; set; } = DateTime.MinValue;

		[Required]
		public int PublicDigestHeight { get; set; }

		public DateTime LastSync { get; set; }

		[Required]
		public string MaximumVersionAllowed { get; set; }

		[Required]
		public string MinimumWarningVersionAllowed { get; set; }

		[Required]
		public string MinimumVersionAllowed { get; set; }

		[Required]
		public int MaxBlockInterval { get; set; }
		[Required]
		public bool AllowGossipPresentations { get; set; }

		public long MiningPassword { get; set; }
		
		public byte[] MiningAutograph { get; set; }

		public DateTime? LastMiningRegistrationUpdate { get; set; }

		public void Copy(MODEL_SNAPSHOT other) {
			IEnumerable<string> excludeProperties = new[] {nameof(this.ModeratorKeys)};

			foreach(PropertyInfo property in typeof(MODEL_SNAPSHOT).GetProperties().Where(p => !excludeProperties.Contains(p.Name))) {

				property.SetValue(this, property.GetValue(other));
			}

			foreach(MODERATOR_KEYS_SNAPSHOT otherkey in other.ModeratorKeys) {
				MODERATOR_KEYS_SNAPSHOT key = this.ModeratorKeys.SingleOrDefault(k => k.OrdinalId == otherkey.OrdinalId);

				if(key == null) {
					key = new MODERATOR_KEYS_SNAPSHOT();
					key.OrdinalId = otherkey.OrdinalId;

					this.ModeratorKeys.Add(key);
				}

				key.Copy(otherkey);
			}

		}

		public List<MODERATOR_KEYS_SNAPSHOT> ModeratorKeys { get; set; } = new List<MODERATOR_KEYS_SNAPSHOT>();
	}
}