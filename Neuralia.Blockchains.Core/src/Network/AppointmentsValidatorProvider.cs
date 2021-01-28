using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.Network {
	public interface IAppointmentsValidatorProvider : IDisposableExtended {
		void EnableVerificationWindow();
		void RegisterValidationServer(BlockchainType blockchainType, List<(DateTime appointment, TimeSpan window, int requesterCount)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate);
		bool InAppointmentWindow { get; }
		bool InAppointmentWindowProximity { get; }
		bool IsInAppointmentWindow(DateTime appointment);
		void AddAppointmentWindow(DateTime appointment, TimeSpan window, int requesterCount);
		void UnregisterValidationServer(BlockchainType blockchainType);
	}

	// the validation server use to operate an appointment validator
	public class AppointmentsValidatorProvider : IAppointmentsValidatorProvider {

		protected ITcpValidatorServer validationServer = null;
		protected RESTValidatorServer restValidatorServer = null;
		private ManagedTimer pollingTimer;

		public static TimeSpan AppointmentWindowHead = TimeSpan.FromMinutes(-5);
		public static TimeSpan AppointmentWindowTail = TimeSpan.FromMinutes(10);

		protected virtual TimeSpan ServerPollDelay => TimeSpan.FromMinutes(1);

		protected IAppointmentValidatorDelegate appointmentValidatorDelegate;
		private BlockchainType blockchainType;
		public virtual void RegisterValidationServer(BlockchainType blockchainType, List<(DateTime appointment, TimeSpan window, int requesterCount)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate) {

			this.appointmentValidatorDelegate = appointmentValidatorDelegate;
			this.blockchainType = blockchainType;
			
			foreach((DateTime appointment, TimeSpan window, int requesterCount) in appointmentWindows) {
				this.AddAppointmentWindow(appointment, window, requesterCount);
			}

#if NET5_0
			if(this.pollingTimer == null) {
				this.pollingTimer = new ManagedTimer(state => {

					bool verificationWindowValid = (this.verificationEnd.HasValue && this.verificationEnd >= DateTimeEx.CurrentTime);

					if(this.InAppointmentWindow || verificationWindowValid) {
						// ok, we are inside a window, lets run the server
						this.StartServer(this.InAppointmentWindowRequesterCount);
						
					} else if(this.validationServer?.IsRunning ?? false) {
						// outside of windows, no server
						this.validationServer?.Stop();
						this.validationServer = null;
						this.restValidatorServer?.Stop();
						this.restValidatorServer = null;
					}

					if(!verificationWindowValid) {
						this.verificationEnd = null;
					}
					
					return Task.CompletedTask;

				}, TimeSpan.FromSeconds(3), this.ServerPollDelay);
				this.pollingTimer.Start();
			}
#endif
		}

		protected virtual int MinimumRequesterCount => GlobalSettings.ApplicationSettings.TargetAppointmentRequesterCount;
		/// <summary>
		/// if we are opening for verification, this is the timeout
		/// </summary>
		protected DateTime? verificationEnd = null;

		public void EnableVerificationWindow() {
			this.verificationEnd = DateTimeEx.CurrentTime.AddMinutes(1);

			if(this.validationServer == null || !this.validationServer.IsRunning) {
				this.StartServer(10, ValidationServers.Default);
			}
		}

		[Flags]
		public enum ValidationServers:byte {
			None = 0, Default = 1, Backup = 1 << 1, Both = Default | Backup
		}

		protected virtual void StartServer(int requesterCount, ValidationServers servers = ValidationServers.Both) {
			
			if(this.validationServer == null || !this.validationServer.IsRunning || (this.validationServer.IsRunning && this.InAppointmentWindow && this.validationServer.RequesterCount < requesterCount)) {

				if(this.validationServer != null) {
					this.validationServer?.Dispose();
					this.validationServer = null;
				}

				if(this.restValidatorServer != null) {
					this.restValidatorServer?.Dispose();
					this.restValidatorServer = null;
				}
				
				var ipMode = IPMode.IPv4;
				var address = IPAddress.Any;

				if(Socket.OSSupportsIPv6) {
					ipMode = IPMode.IPv6;
					address = IPAddress.IPv6Any;
				} else {
					ipMode = IPMode.IPv4;
					address = IPAddress.Any;
				}
				
#if NET5_0

				ValidationServers startedServers = ValidationServers.None;
				if(servers.HasFlag(ValidationServers.Default)) {
					try {
						this.validationServer = new TcpValidatorServer(Math.Max(requesterCount, this.MinimumRequesterCount), new NetworkEndPoint(address, GlobalSettings.ApplicationSettings.ValidatorPort, ipMode));
						this.validationServer.Initialize();

						this.validationServer.RegisterBlockchainDelegate(blockchainType, appointmentValidatorDelegate, () => this.InAppointmentWindow);

						this.validationServer.Start();

						startedServers |= ValidationServers.Default;
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to start validator TCP server. This is not critical if backup HTTP server can start");
					}
				}

				if(GlobalSettings.ApplicationSettings.EnableAppointmentValidatorBackupProtocol && servers.HasFlag(ValidationServers.Backup)) {
					try {
						this.restValidatorServer = new RESTValidatorServer(GlobalSettings.ApplicationSettings.ValidatorHttpPort);

						this.restValidatorServer.RegisterBlockchainDelegate(blockchainType, appointmentValidatorDelegate, () => this.InAppointmentWindow);

						this.restValidatorServer.Start();
						
						startedServers |= ValidationServers.Backup;
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to start validator backup http server. This is not critical");
					}
				}

				if(startedServers == ValidationServers.None) {
					throw new ApplicationException("Failed to start ALL validation servers");
				}
				
				this.ValidatorServersStarted(startedServers);
#else
				NLog.Default.Warning("Appointments validators are not possible in netstandard mode");
#endif
			} 
		}
		
		protected virtual void ValidatorServersStarted(ValidationServers servers) {
			
		}
		
		public int InAppointmentWindowRequesterCount {
			get {
				int count = 10;

				foreach(var window in this.appointmentWindows.Where(a => a.start < DateTimeEx.CurrentTime && a.end >= DateTimeEx.CurrentTime)) {
					count = Math.Max(count, window.requesterCount);
				}

				return count;
			}
		}

		public bool InAppointmentWindow => this.appointmentWindows.Any(a => a.start < DateTimeEx.CurrentTime && a.end >= DateTimeEx.CurrentTime);
		public bool InAppointmentWindowProximity => this.appointmentWindows.Any(a => (a.start + AppointmentWindowHead) < DateTimeEx.CurrentTime && (a.end + AppointmentWindowTail) >= DateTimeEx.CurrentTime);

		public bool IsInAppointmentWindow(DateTime appointment) {
			return this.appointmentWindows.Any(a => a.start < appointment && a.end >= appointment && a.start < DateTimeEx.CurrentTime && a.end >= DateTimeEx.CurrentTime);
		}

		protected readonly List<(DateTime start, DateTime end, int requesterCount)> appointmentWindows = new List<(DateTime start, DateTime end, int requesterCount)>();

		private void ClearExpiredAppointmentWindows() {
			foreach(var entry in this.appointmentWindows.Where(a => a.end < DateTimeEx.CurrentTime).ToArray()) {
				this.appointmentWindows.Remove(entry);
			}
		}

		public void AddAppointmentWindow(DateTime appointment, TimeSpan window, int requesterCount) {
			this.ClearExpiredAppointmentWindows();

			int minutes = 5;
			DateTime start = DateTime.SpecifyKind(appointment.AddMinutes(-minutes), DateTimeKind.Utc);
			DateTime end = DateTime.SpecifyKind((appointment + window).AddMinutes(minutes), DateTimeKind.Utc);

			if(end > DateTimeEx.CurrentTime) {
				if(!this.appointmentWindows.Any(a => a.start == start && a.end == end)) {
					NLog.Default.Information($"Adding validation appointment window from {start} to {end} with {requesterCount} requesters for appointment time {appointment}.");
					this.appointmentWindows.Add((start, end, requesterCount));
				} else {

					foreach(var entry in this.appointmentWindows.Where(a => a.start == start && a.end == end && a.requesterCount != requesterCount).ToArray()) {
						this.appointmentWindows.Remove(entry);
					}

					if(!this.appointmentWindows.Any(a => a == (start, end, requesterCount))) {
						NLog.Default.Information($"Updating validation appointment window from {start} to {end} with {requesterCount} requesters for appointment time {appointment}.");

						this.appointmentWindows.Add((start, end, requesterCount));
					}
				}
			}
		}

		public void UnregisterValidationServer(BlockchainType blockchainType) {
#if NET5_0
			this.validationServer?.UnregisterBlockchainDelegate(blockchainType);
			this.restValidatorServer?.UnregisterBlockchainDelegate(blockchainType);
			
			if(this.validationServer?.BlockchainDelegateEmpty ?? false) {

				this.pollingTimer?.Dispose();
				this.pollingTimer = null;

				this.validationServer?.Stop();
				this.validationServer?.Dispose();
				this.validationServer = null;
				
				this.restValidatorServer?.Stop();
				this.restValidatorServer?.Dispose();
				this.restValidatorServer = null;
			}
#endif
			this.ClearExpiredAppointmentWindows();
		}

	#region dispose

		protected virtual void Dispose(bool disposing) {
			if(disposing && !this.IsDisposed) {

				try {
					this.validationServer?.Dispose();
					this.validationServer = null;
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to dispose of validation server");
				}
				
				try {
					this.restValidatorServer?.Dispose();
					this.restValidatorServer = null;
				} catch(Exception ex) {
					NLog.Default.Error(ex, "Failed to dispose of REST validation server");
				}
			}

			this.IsDisposed = true;
		}

		~AppointmentsValidatorProvider() {
			this.Dispose(false);
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}

}