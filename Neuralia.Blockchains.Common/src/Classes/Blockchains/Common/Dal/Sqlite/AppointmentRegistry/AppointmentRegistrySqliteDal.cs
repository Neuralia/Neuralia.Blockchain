using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.GossipMessages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry.PeerEntries;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.Extensions.DbSet;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Nito.AsyncEx;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.AppointmentRegistry {

	public interface IAppointmentRegistrySqliteDal : ISqliteDal<IAppointmentRegistrySqliteContext>, IAppointmentRegistryDal {
	}

	public class AppointmentRegistrySqliteDal : SqliteDal<AppointmentRegistrySqliteContext>, IAppointmentRegistrySqliteDal{

		private readonly ITimeService timeService;
		protected readonly ICentralCoordinator centralCoordinator;
		protected ICentralCoordinator CentralCoordinator => this.centralCoordinator;
		private readonly bool enablePuzzleTHS;
		private readonly AsyncLock locker = new AsyncLock();
		public AppointmentRegistrySqliteDal(string folderPath, ICentralCoordinator centralCoordinator, bool enablePuzzleTHS, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, centralCoordinator.BlockchainServiceSet, softwareVersion, chainDalCreationFactory.CreateAppointmentRegistryContext<AppointmentRegistrySqliteContext>, serializationType) {

			this.enablePuzzleTHS = enablePuzzleTHS;
			this.centralCoordinator = centralCoordinator;
			this.timeService = serviceSet.TimeService;
		}
		
		public Task InsertAppointmentContextGossipMessage(Guid messageUuid, DateTime appointment, int start, int end) {

			return this.PerformOperationAsync(async (db, lc) => {

				using(await locker.LockAsync().ConfigureAwait(false)) {
					
					if(await db.AppointmentContexts.AnyAsync(m => m.Appointment == appointment && m.MessageUuid != messageUuid).ConfigureAwait(false)) {
						// already there, delete the old one
						db.AppointmentContexts.Delete(db, m => m.Appointment == appointment && m.MessageUuid != messageUuid);
						return;
					}
					
					if(await db.AppointmentContexts.AnyAsync(m => m.MessageUuid == messageUuid).ConfigureAwait(false)) {
						// already there
						return;
					}

					var message = new AppointmentContextGossipMessageSqlite();

					message.MessageUuid = messageUuid;
					message.Appointment = appointment;
					message.Start = start;
					message.End = end;

					db.AppointmentContexts.Add(message);
					
					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}
		
		public async Task<IAppointmentContextGossipMessage> GetAppointmentContext(int requesterIndex, DateTime appointment) {

			return (await this.PerformOperationAsync((db, lc) => {
				
					       return db.AppointmentContexts.SingleOrDefaultAsync(m => m.Appointment == appointment && m.Start >= requesterIndex && m.End <= requesterIndex);
				       }).ConfigureAwait(false));
		}
		
		public Task InsertAppointmentTriggerGossipMessage(Guid messageUuid, DateTime appointment) {

			return this.PerformOperationAsync(async (db, lc) => {

				using(await locker.LockAsync().ConfigureAwait(false)) {
					
					if(await db.AppointmentTriggers.AnyAsync(m => m.Appointment == appointment && m.MessageUuid != messageUuid).ConfigureAwait(false)) {
						// already there, delete the old one
						db.AppointmentTriggers.Delete(db, m => m.Appointment == appointment && m.MessageUuid != messageUuid);
						return;
					}
					
					if(await db.AppointmentTriggers.AnyAsync(m => m.MessageUuid == messageUuid).ConfigureAwait(false)) {
						// already there
						return;
					}

					var message = new AppointmentTriggerGossipMessageSqlite();

					message.MessageUuid = messageUuid;
					message.Appointment = appointment;

					db.AppointmentTriggers.Add(message);
					
					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}
		
		public async Task<IAppointmentTriggerGossipMessage> GetAppointmentTrigger(DateTime appointment) {

			return (await this.PerformOperationAsync((db, lc) => {
				
		       return db.AppointmentTriggers.SingleOrDefaultAsync(e => e.Appointment == appointment);
	       }).ConfigureAwait(false));
		}
		
		
		public Task InsertAppointmentRequestConfirmationMessage(List<Guid> requesterIds, Guid messageUuid, DateTime appointment) {

			return this.PerformOperationAsync(async (db, lc) => {

				using(await locker.LockAsync().ConfigureAwait(false)) {
					
					if(await db.AppointmentRequestConfirmations.AnyAsync(m => m.Appointment == appointment && m.MessageUuid != messageUuid).ConfigureAwait(false)) {
						// already there, delete the old one
						db.AppointmentRequestConfirmations.Delete(db, m => m.Appointment == appointment && m.MessageUuid != messageUuid);
						return;
					}
					
					if(await db.AppointmentRequestConfirmations.AnyAsync(m => m.MessageUuid == messageUuid).ConfigureAwait(false)) {
						// already there
						return;
					}

					//var changeTracker = db.ChangeTracker;
					try {
						//changeTracker.AutoDetectChangesEnabled = false;
						var message = new AppointmentRequestConfirmationGossipMessageSqlite();

						message.MessageUuid = messageUuid;
						message.Appointment = appointment;

						db.AppointmentRequestConfirmations.Add(message);

						foreach(var requester in requesterIds) {
							var entry = new AppointmentResponseEntrySqlite();

							entry.RequesterId = requester;
							entry.Appointment = appointment;
							entry.MessageUuid = messageUuid;
							
							db.AppointmentResponseEntries.Add(entry);
						}
					} finally {
						//changeTracker.AutoDetectChangesEnabled = true;
					}

					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}
		
		public async Task<IAppointmentResponseEntry> GetAppointmentRequestConfirmation(Guid requesterId, DateTime? appointment) {

			return (await this.PerformOperationAsync((db, lc) => {

		       if(appointment.HasValue) {
			       return db.AppointmentResponseEntries.SingleOrDefaultAsync(a => a.RequesterId == requesterId && a.Appointment == appointment);
		       } else {
			       return db.AppointmentResponseEntries.FirstOrDefaultAsync(a => a.RequesterId == requesterId);
		       }
	       }).ConfigureAwait(false));
		}
		
		public Task InsertAppointmentVerificationConfirmationMessage(List<Guid> requesterIds, Guid messageUuid, DateTime appointment) {

			return this.PerformOperationAsync(async (db, lc) => {

				using(await locker.LockAsync().ConfigureAwait(false)) {
					
					if(await db.AppointmentVerificationConfirmations.AnyAsync(m => m.Appointment == appointment && m.MessageUuid != messageUuid).ConfigureAwait(false)) {
						// already there, delete the old one
						db.AppointmentVerificationConfirmations.Delete(db, m => m.Appointment == appointment && m.MessageUuid != messageUuid);
						return;
					}
					
					if(await db.AppointmentVerificationConfirmations.AnyAsync(m => m.MessageUuid == messageUuid).ConfigureAwait(false)) {
						// already there
						return;
					}

					//var changeTracker = db.ChangeTracker;
					try {
						//changeTracker.AutoDetectChangesEnabled = false;
						var message = new AppointmentVerificationConfirmationGossipMessageSqlite();

						message.MessageUuid = messageUuid;
						message.Appointment = appointment;

						db.AppointmentVerificationConfirmations.Add(message);

						foreach(var requester in requesterIds) {
							var entry = new AppointmentVerificationConfirmationEntrySqlite();

							entry.RequesterId = requester;
							entry.Appointment = appointment;
							entry.MessageUuid = messageUuid;
							
							db.AppointmentVerificationConfirmationEntries.Add(entry);
						}
					} finally {
						//changeTracker.AutoDetectChangesEnabled = true;
					}

					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}
		
		public async Task<IAppointmentVerificationConfirmationEntry> GetAppointmentVerificationConfirmations(Guid requesterId, DateTime appointment) {
			return (await this.PerformOperationAsync((db, lc) => {
				
				       return db.AppointmentVerificationConfirmationEntries.SingleOrDefaultAsync(a => a.RequesterId == requesterId && a.Appointment == appointment);
			       }).ConfigureAwait(false));
		}

		public async Task<IAppointmentValidatorSession> GetAppointmentValidatorSession(DateTime appointment) {
			return await this.PerformOperationAsync((db, lc) => {
				
				return db.AppointmentValidatorSessions.SingleOrDefaultAsync(a => a.Appointment == appointment);
			}).ConfigureAwait(false);
		}

		public Task<DateTime?> GetInRangeAppointments() {
			return this.PerformOperationAsync(async (db, lc) => {

				DateTime min = DateTimeEx.CurrentTime+AppointmentsValidatorProvider.AppointmentWindowHead;
				DateTime max = DateTimeEx.CurrentTime+AppointmentsValidatorProvider.AppointmentWindowTail;

				return await db.AppointmentValidatorSessions.Where(a => a.Appointment >= min && a.Appointment <= max).Select(e => (DateTime?) e.Appointment).SingleOrDefaultAsync().ConfigureAwait(false);
			});
		}

		public async Task<List<(DateTime appointment, TimeSpan window, int requesterCount)>> GetAppointments() {
			return (await this.PerformOperationAsync((db, lc) => {

				return db.AppointmentValidatorSessions.Select(e => new {e.Appointment, e.Window, e.RequesterCount}).ToListAsync();
			}).ConfigureAwait(false)).Select(e => (e.Appointment, TimeSpan.FromSeconds(e.Window), e.RequesterCount)).ToList();
		}
		
		public Task InsertAppointmentValidatorSession(IAppointmentValidatorSession appointmentValidatorSession) {
			return this.PerformOperationAsync(async (db, lc) => {

				if(!await db.AppointmentValidatorSessions.AnyAsync(a => a.Appointment == appointmentValidatorSession.Appointment).ConfigureAwait(false)) {

					db.AppointmentValidatorSessions.Add((AppointmentValidatorSessionSqlite)appointmentValidatorSession);

					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}

		public Task UpdateAppointmentValidatorSession(IAppointmentValidatorSession appointmentValidatorSession) {
			return this.PerformOperationAsync(async (db, lc) => {

				var entry = await db.AppointmentValidatorSessions.SingleOrDefaultAsync(a => a.Id == ((AppointmentValidatorSessionSqlite)appointmentValidatorSession).Id).ConfigureAwait(false);

				entry.SecretCodes = appointmentValidatorSession.SecretCodes;
				entry.Indices = appointmentValidatorSession.Indices;
				
				await db.SaveChangesAsync().ConfigureAwait(false);
			});
		}

		public async Task<List<Guid>> ClearExpired() {
			try {
				
				return await this.PerformOperationAsync(async (db, lc) => {
					List<Guid> clearedMessageids = new List<Guid>();
					DateTime time = DateTimeEx.CurrentTime+AppointmentsValidatorProvider.AppointmentWindowTail;

					var removeMessages = await db.AppointmentContexts.Where(t => t.Appointment < time).ToListAsync().ConfigureAwait(false);
					var messageIds = removeMessages.Select(m => m.MessageUuid).ToList();
					clearedMessageids.AddRange(messageIds);
					db.AppointmentContexts.RemoveRange(removeMessages);
					
					
					var removeMessages2 = await db.AppointmentTriggers.Where(t => t.Appointment < time).ToListAsync().ConfigureAwait(false);
					messageIds = removeMessages2.Select(m => m.MessageUuid).ToList();
					clearedMessageids.AddRange(messageIds);
					db.AppointmentTriggers.RemoveRange(removeMessages2);

					// request confirmations
					var removeMessages3 = await db.AppointmentRequestConfirmations.Where(t => t.Appointment < time).ToListAsync().ConfigureAwait(false);
					db.AppointmentRequestConfirmations.RemoveRange(removeMessages3);
					messageIds = removeMessages3.Select(m => m.MessageUuid).ToList();
					clearedMessageids.AddRange(messageIds);
					db.AppointmentResponseEntries.RemoveRange(db.AppointmentResponseEntries.Where(t => messageIds.Contains(t.MessageUuid)));
					
					// verification confirmations
					var removeMessages4 = await db.AppointmentVerificationConfirmations.Where(t => t.Appointment < time).ToListAsync().ConfigureAwait(false);
					db.AppointmentVerificationConfirmations.RemoveRange(removeMessages4);
					messageIds = removeMessages4.Select(m => m.MessageUuid).ToList();
					clearedMessageids.AddRange(messageIds);
					db.AppointmentVerificationConfirmationEntries.RemoveRange(db.AppointmentVerificationConfirmationEntries.Where(t => messageIds.Contains(t.MessageUuid)));

					try {
						DateTime timeLimit = DateTimeEx.CurrentTime;
						db.AppointmentValidatorSessions.RemoveRange(db.AppointmentValidatorSessions.Where(s => s.Appointment.AddDays(1) < timeLimit));
						db.AppointmentRequesterResults.RemoveRange(db.AppointmentRequesterResults.Where(s => s.Appointment.AddDays(1) < timeLimit));
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Error(ex, $"Failed to clear obsolete appointments");
					}
					

					// now remove appointments that are over
					DateTime timeLimit2 = DateTimeEx.CurrentTime;
					var remainings = await db.AppointmentValidatorSessions.Where(s => s.Appointment < timeLimit2).Select(a => a.Appointment).ToListAsync().ConfigureAwait(false);

					foreach(var appointment in remainings) {
						try {
							if(await db.AppointmentRequesterResults.AllAsync(e => e.Sent && e.Appointment == appointment).ConfigureAwait(false)) {
								db.AppointmentValidatorSessions.RemoveRange(db.AppointmentValidatorSessions.Where(s => s.Appointment == appointment));
								db.AppointmentRequesterResults.RemoveRange(db.AppointmentRequesterResults.Where(s => s.Appointment == appointment));
							}
						} catch(Exception ex) {
							this.CentralCoordinator.Log.Error(ex, $"Failed to clear remaining appointment {appointment}");
						}
					}
					await db.SaveChangesAsync().ConfigureAwait(false);

					return clearedMessageids;
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				//TODO: what to do?
				this.CentralCoordinator.Log.Error("Failed to clear expired appointments", ex);
			}
			return new List<Guid>();
		}

		public async Task<IAppointmentRequesterResult> GetAppointmentRequesterResult(DateTime appointment, int index) {
			return await this.PerformOperationAsync((db, lc) => {
				
				return db.AppointmentRequesterResults.SingleOrDefaultAsync(a => a.Appointment == appointment && a.Index == index);
			}).ConfigureAwait(false);
		}

		public Task InsertAppointmentRequesterResult(IAppointmentRequesterResult appointmentRequesterResult) {
			return this.PerformOperationAsync(async (db, lc) => {

				if(!await db.AppointmentRequesterResults.AnyAsync(a => a.Appointment == appointmentRequesterResult.Appointment && a.Index == appointmentRequesterResult.Index).ConfigureAwait(false)) {

					db.AppointmentRequesterResults.Add((AppointmentRequesterResultSqlite)appointmentRequesterResult);

					await db.SaveChangesAsync().ConfigureAwait(false);
				}
			});
		}

		public Task UpdateAppointmentRequesterResult(IAppointmentRequesterResult appointmentRequesterResult) {
			return this.PerformOperationAsync(async (db, lc) => {

				var entry = await db.AppointmentRequesterResults.SingleOrDefaultAsync(a => a.Id == ((AppointmentRequesterResultSqlite)appointmentRequesterResult).Id).ConfigureAwait(false);

				entry.RequestedCodeCompleted = appointmentRequesterResult.RequestedCodeCompleted;
				entry.ValidatorCode = appointmentRequesterResult.ValidatorCode;
				entry.SecretCode = appointmentRequesterResult.SecretCode;
				entry.TriggerCompleted = appointmentRequesterResult.TriggerCompleted;
				entry.PuzzleCompleted = appointmentRequesterResult.PuzzleCompleted;
				entry.PuzzleResults = appointmentRequesterResult.PuzzleResults;
				entry.THSCompleted = appointmentRequesterResult.THSCompleted;
				entry.THSResults = appointmentRequesterResult.THSResults;
				
				entry.Valid = appointmentRequesterResult.Valid;
				
				await db.SaveChangesAsync().ConfigureAwait(false);
			});
		}

		/// <summary>
		/// get all the sessions that are ready to be processed
		/// </summary>
		/// <returns></returns>
		public Task<List<DateTime>> GetReadyAppointmentSessions() {
			DateTime time = DateTimeEx.CurrentTime;

			return this.PerformOperationAsync((db, lc) => {
				return db.AppointmentValidatorSessions.Where(s => s.Dispatch < time).Select(e => e.Appointment).Distinct().ToListAsync();
			});
		}
				        

		public async Task<List<IAppointmentRequesterResult>> GetReadyAppointmentRequesterResult(DateTime appointment, int skip, int take) {
			return (await this.PerformOperationAsync(async (db, lc) => { 
					       return await db.AppointmentRequesterResults.Where(a => a.Sent == false && a.Appointment == appointment && a.TriggerCompleted.HasValue && a.PuzzleCompleted.HasValue && (this.enablePuzzleTHS?a.THSCompleted.HasValue:true)).OrderBy(e => e.Id).Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
					       
			}).ConfigureAwait(false)).Cast<IAppointmentRequesterResult>().ToList();
		}

		public Task<int> GetReadyAppointmentRequesterResultCount(DateTime appointment) {
			return  this.PerformOperationAsync((db, lc) => { 
		       return db.AppointmentRequesterResults.CountAsync(a => a.Sent == false && a.Appointment == appointment && a.TriggerCompleted.HasValue && a.PuzzleCompleted.HasValue && (this.enablePuzzleTHS?a.THSCompleted.HasValue:true));
			});
		}

		public Task ClearReadyAppointmentRequesterResult(List<int> ids) {
			return this.PerformOperationAsync(async (db, lc) => {

				foreach(var id in ids) {
					
					AppointmentRequesterResultSqlite entry = new AppointmentRequesterResultSqlite();
					entry.Id = id;
					entry.Sent = true;
					
					EntityEntry<AppointmentRequesterResultSqlite> dbEntry = db.Entry(entry);

					dbEntry.Property(e => e.Sent).IsModified = true;
				}

				await db.SaveChangesAsync().ConfigureAwait(false);
			});
		}
	}
}