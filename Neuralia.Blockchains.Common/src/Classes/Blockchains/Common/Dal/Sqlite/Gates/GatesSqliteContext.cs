using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.Gates {
	public interface IGatesSqliteContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : ISplitSqliteDbContext, IGatesContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>
		where STANDARD_ACCOUNT_GATES : StandardAccountGatesSqlite 
		where JOINT_ACCOUNT_GATES : JointAccountGatesSqlite{


		DbSet<STANDARD_ACCOUNT_GATES> StandardGates { get; set; }
		DbSet<JOINT_ACCOUNT_GATES> JointGates { get; set; }
	}

	public abstract class GatesSqliteContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : SplitSqliteDbContext, IGatesSqliteContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>
		where STANDARD_ACCOUNT_GATES : StandardAccountGatesSqlite 
		where JOINT_ACCOUNT_GATES : JointAccountGatesSqlite{

		public override string GroupRoot => "gates";

		public DbSet<STANDARD_ACCOUNT_GATES> StandardGates { get; set; }
		public DbSet<JOINT_ACCOUNT_GATES> JointGates { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<STANDARD_ACCOUNT_GATES>(eb => {
				eb.HasKey(c => c.AccountId);
				eb.Property(b => b.AccountId).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.AccountId).IsUnique();
				eb.ToTable("StandardGates");
			});
			
			modelBuilder.Entity<JOINT_ACCOUNT_GATES>(eb => {
				eb.HasKey(c => c.AccountId);
				eb.Property(b => b.AccountId).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.AccountId).IsUnique();
				eb.ToTable("JointGates");
			});
			//
			// modelBuilder.Entity<STANDARD_ACCOUNT_GATES>(eb => {
			// 	eb.HasKey(c => c.OrdinalId);
			// 	eb.Property(b => b.OrdinalId).ValueGeneratedNever();
			// 	eb.HasIndex(b => b.OrdinalId).IsUnique();
			// 	eb.ToTable("ModeratorKeys");
			// });
			//
			// modelBuilder.Entity<STANDARD_ACCOUNT_GATES>().HasOne(pt => pt.ChainStateEntry).WithMany(p => p.ModeratorKeys).HasForeignKey(pt => pt.ChainStateId);
		}
	}
}