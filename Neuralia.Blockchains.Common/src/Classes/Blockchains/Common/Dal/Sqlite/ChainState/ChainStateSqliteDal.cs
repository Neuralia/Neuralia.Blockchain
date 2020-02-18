using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.ChainState {

	public interface IChainStateSqliteDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : ISqliteDal<IChainStateSqliteContext<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>>, IChainStateDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_CONTEXT : class, IChainStateSqliteContext<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_SNAPSHOT : class, IChainStateSqliteEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {
	}

	public abstract class ChainStateSqliteDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> : SqliteDal<CHAIN_STATE_CONTEXT>, IChainStateSqliteDal<CHAIN_STATE_CONTEXT, CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_CONTEXT : DbContext, IChainStateSqliteContext<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where CHAIN_STATE_SNAPSHOT : class, IChainStateSqliteEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT>
		where MODERATOR_KEYS_SNAPSHOT : class, IChainStateSqliteModeratorKeysEntry<CHAIN_STATE_SNAPSHOT, MODERATOR_KEYS_SNAPSHOT> {

		public ChainStateSqliteDal(string folderPath, BlockchainServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateChainStateContext<CHAIN_STATE_CONTEXT>, serializationType) {

		}

		public Func<CHAIN_STATE_SNAPSHOT> CreateNewEntry { get; set; }

		public void PerformOperation(Action<CHAIN_STATE_CONTEXT> process) {

			base.PerformOperation(process);
		}

		public T PerformOperation<T>(Func<CHAIN_STATE_CONTEXT, T> process) {

			return base.PerformOperation(process);
		}

		public Task PerformOperationAsync(Func<CHAIN_STATE_CONTEXT, Task> process) {
			return base.PerformOperationAsync(process);
		}

		public Task<T> PerformOperationAsync<T>(Func<CHAIN_STATE_CONTEXT, Task<T>> process) {
			return base.PerformOperationAsync(process);
		}

		public CHAIN_STATE_SNAPSHOT LoadFullState(CHAIN_STATE_CONTEXT db) {
			CHAIN_STATE_SNAPSHOT entry = db.ChainMetadatas.AsNoTracking().Include(e => e.ModeratorKeys).SingleOrDefault();

			if(entry != null) {
				return entry;
			}

			entry = this.CreateNewEntry();
			entry.Id = 1; // we only ever have 1
			var dbEntry = db.Entry(entry);
			db.ChainMetadatas.Add(entry);

			db.SaveChanges();

			return entry;
		}

		public CHAIN_STATE_SNAPSHOT LoadSimpleState(CHAIN_STATE_CONTEXT db) {

			CHAIN_STATE_SNAPSHOT entry = db.ChainMetadatas.AsNoTracking().OrderBy(e => e.Id).Take(1).SingleOrDefault();

			if(entry != null) {
				return entry;
			}

			entry = this.CreateNewEntry();
			entry.Id = 1; // we only ever have 1
			var dbEntry = db.Entry(entry);
			db.ChainMetadatas.Add(entry);

			db.SaveChanges();

			return entry;
		}
	}
}