using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainState {

	public interface IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
	}

	public abstract class ChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODEL_SNAPSHOT : class, IChainStateSqliteEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<MODEL_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		[Key]
		public byte OrdinalId { get; set; }

		public int ChainStateId { get; set; }
		public MODEL_SNAPSHOT ChainStateEntry { get; set; }

		public byte[] PublicKey { get; set; }

		public string DeclarationTransactionId { get; set; }
		public bool IsCurrent { get; set; }

		public void Copy(MODERATOR_KEYS_SNAPSHOT other) {
			IEnumerable<string> excludeProperties = new[] {nameof(this.ChainStateEntry), nameof(this.ChainStateId)};

			foreach(PropertyInfo property in typeof(MODERATOR_KEYS_SNAPSHOT).GetProperties().Where(p => !excludeProperties.Contains(p.Name))) {

				property.SetValue(this, property.GetValue(other));
			}
		}
	}
}