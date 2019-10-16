using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AccountSnapshots.Storage {
	public interface IAccreditationCertificateSnapshotSqliteContext : IIndexedSqliteDbContext, IAccreditationCertificatesSnapshotContext {
	}

	public interface IAccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, IAccreditationCertificateSnapshotSqliteContext
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotSqliteEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountSqliteEntry {
	}

	public abstract class AccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> : IndexedSqliteDbContext, IAccreditationCertificateSnapshotSqliteContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : AccreditationCertificateSnapshotSqliteEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : AccreditationCertificateSnapshotAccountSqliteEntry {

		public override string GroupRoot => "accreditation-certificates-snapshots";

		public DbSet<ACCREDITATION_CERTIFICATE_SNAPSHOT> AccreditationCertificates { get; set; }
		public DbSet<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT> AccreditationCertificateAccounts { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<ACCREDITATION_CERTIFICATE_SNAPSHOT>(eb => {
				eb.HasKey(c => c.CertificateId);

				eb.Property(b => b.CertificateState).HasConversion(v => (byte) v, v => (Enums.CertificateStates) v);

				eb.Property(e => e.CertificateVersion).HasConversion(v => v.ToString(), v => new ComponentVersion(v));

				eb.Property(b => b.CertificateAccountPermissionType).HasConversion(v => (int) v, v => (Enums.CertificateAccountPermissionTypes) v);

				eb.Ignore(b => b.PermittedAccounts);

				eb.ToTable("AccreditationCertificates");
			});

			modelBuilder.Entity<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>(eb => {
				eb.HasKey(c => c.Id);
				eb.HasIndex(e => e.CertificateId);

				eb.ToTable("AccreditationCertificatesPermittedAccounts");
			});

		}
	}
}