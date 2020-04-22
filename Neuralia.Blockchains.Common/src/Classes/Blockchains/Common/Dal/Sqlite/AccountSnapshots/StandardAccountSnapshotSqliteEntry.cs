using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots {

	public interface IStandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE> : IStandardAccountSnapshotEntry<ACCOUNT_ATTRIBUTE>, IAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : class, IAccountAttributeSqliteEntry, new() {
	}

	/// <summary>
	///     Here we store various metadata state about our chain
	/// </summary>
	public abstract class StandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE> : StandardAccountSnapshot<ACCOUNT_ATTRIBUTE>, IStandardAccountSnapshotSqliteEntry<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : AccountAttributeSqliteEntry, new() {
		
		public override void AddCollectionEntry(IAccountAttribute entry){
			
			((ACCOUNT_ATTRIBUTE) entry).AccountId = this.AccountId;
			
			base.AddCollectionEntry(entry);
		}
	}
}