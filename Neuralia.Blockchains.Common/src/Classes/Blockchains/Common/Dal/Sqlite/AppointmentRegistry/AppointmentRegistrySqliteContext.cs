using System;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry {
	public interface IAppointmentRegistrySqliteContext : ISqliteDbContext, IAppointmentRegistryContext{

		DbSet<AppointmentContextGossipMessageSqlite> AppointmentContexts { get; set; }
		DbSet<AppointmentRequestConfirmationGossipMessageSqlite> AppointmentRequestConfirmations { get; set; }
		DbSet<AppointmentTriggerGossipMessageSqlite> AppointmentTriggers { get; set; }
		DbSet<AppointmentVerificationConfirmationGossipMessageSqlite> AppointmentVerificationConfirmations { get; set; }
		
		DbSet<AppointmentResponseEntrySqlite> AppointmentResponseEntries { get; set; }
		DbSet<AppointmentVerificationConfirmationEntrySqlite> AppointmentVerificationConfirmationEntries { get; set; }
		
		DbSet<AppointmentValidatorSessionSqlite> AppointmentValidatorSessions { get; set; }
		DbSet<AppointmentRequesterResultSqlite> AppointmentRequesterResults { get; set; }
	}

	public class AppointmentRegistrySqliteContext : SqliteDbContext, IAppointmentRegistrySqliteContext {

		protected override string DbName => "appointment-registry.db";

		public DbSet<AppointmentContextGossipMessageSqlite> AppointmentContexts { get; set; }
		public DbSet<AppointmentTriggerGossipMessageSqlite> AppointmentTriggers { get; set; }
		public DbSet<AppointmentRequestConfirmationGossipMessageSqlite> AppointmentRequestConfirmations { get; set; }
		public DbSet<AppointmentVerificationConfirmationGossipMessageSqlite> AppointmentVerificationConfirmations { get; set; }
		
		public DbSet<AppointmentResponseEntrySqlite> AppointmentResponseEntries { get; set; }
		public DbSet<AppointmentVerificationConfirmationEntrySqlite> AppointmentVerificationConfirmationEntries { get; set; }
		public DbSet<AppointmentValidatorSessionSqlite> AppointmentValidatorSessions { get; set; }

		public DbSet<AppointmentRequesterResultSqlite> AppointmentRequesterResults { get; set; }
		
		protected override void OnModelCreating(ModelBuilder modelBuilder) {

			modelBuilder.Entity<AppointmentContextGossipMessageSqlite>(eb => {
				eb.HasKey(c => c.MessageUuid);
				eb.Property(b => b.MessageUuid).ValueGeneratedNever();
				eb.HasIndex(b => b.MessageUuid).IsUnique();
				
				eb.HasIndex(b => b.Appointment).IsUnique();
				
				eb.ToTable("AppointmentContexts");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentRequestConfirmationGossipMessageSqlite>(eb => {
				eb.HasKey(c => c.MessageUuid);
				eb.Property(b => b.MessageUuid).ValueGeneratedNever();
				eb.HasIndex(b => b.MessageUuid).IsUnique();
				
				eb.HasIndex(b => b.Appointment).IsUnique();
				
				eb.ToTable("AppointmentRequestConfirmations");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentTriggerGossipMessageSqlite>(eb => {
				eb.HasKey(c => c.MessageUuid);
				eb.Property(b => b.MessageUuid).ValueGeneratedNever();
				eb.HasIndex(b => b.MessageUuid).IsUnique();
				
				eb.HasIndex(b => b.Appointment).IsUnique();
				
				eb.ToTable("AppointmentTriggers");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentVerificationConfirmationGossipMessageSqlite>(eb => {
				eb.HasKey(c => c.MessageUuid);
				eb.Property(b => b.MessageUuid).ValueGeneratedNever();
				eb.HasIndex(b => b.MessageUuid).IsUnique();
				
				eb.HasIndex(b => b.Appointment).IsUnique();
				
				eb.ToTable("AppointmentVerificationConfirmations");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentResponseEntrySqlite>(eb => {
				eb.HasKey(c => c.RequesterId);
				eb.Property(b => b.RequesterId).ValueGeneratedNever();
				eb.HasIndex(b => b.RequesterId).IsUnique();
				
				eb.HasIndex(b => b.MessageUuid);
				eb.ToTable("AppointmentResponseEntries");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)); 
			});
			
			modelBuilder.Entity<AppointmentVerificationConfirmationEntrySqlite>(eb => {
				eb.HasKey(c => c.RequesterId);
				eb.Property(b => b.RequesterId).ValueGeneratedNever();
				eb.HasIndex(b => b.RequesterId).IsUnique();
				
				eb.HasIndex(b => b.MessageUuid);
				eb.ToTable("AppointmentVerificationConfirmationEntries");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentValidatorSessionSqlite>(eb => {
				eb.HasKey(c => c.Id);
				eb.Property(b => b.Id).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.Id).IsUnique();
				
				eb.HasIndex(b => b.ValidatorHash).IsUnique();
				eb.ToTable("AppointmentValidatorSessions");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
				eb.Property(c => c.Dispatch).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
			});
			
			modelBuilder.Entity<AppointmentRequesterResultSqlite>(eb => {
				eb.HasKey(c => c.Id);
				eb.Property(b => b.Id).ValueGeneratedOnAdd();
				eb.HasIndex(b => b.Id).IsUnique();
				
				eb.ToTable("AppointmentRequesterResults");
				
				eb.Property(c => c.Appointment).HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
				
				eb.Property(c => c.RequestedCodeCompleted).HasConversion(v => v.HasValue?v.Value.ToUniversalTime():(DateTime?)null, v => v.HasValue?DateTime.SpecifyKind(v.Value, DateTimeKind.Utc):(DateTime?)null);
				eb.Property(c => c.TriggerCompleted).HasConversion(v => v.HasValue?v.Value.ToUniversalTime():(DateTime?)null, v => v.HasValue?DateTime.SpecifyKind(v.Value, DateTimeKind.Utc):(DateTime?)null);
				eb.Property(c => c.PuzzleCompleted).HasConversion(v => v.HasValue?v.Value.ToUniversalTime():(DateTime?)null, v => v.HasValue?DateTime.SpecifyKind(v.Value, DateTimeKind.Utc):(DateTime?)null);
				eb.Property(c => c.THSCompleted).HasConversion(v => v.HasValue?v.Value.ToUniversalTime():(DateTime?)null, v => v.HasValue?DateTime.SpecifyKind(v.Value, DateTimeKind.Utc):(DateTime?)null);
			});
			
			
		}
	}
}