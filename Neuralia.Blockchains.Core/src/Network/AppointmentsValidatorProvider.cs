using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.Network {
	public interface IAppointmentsValidatorProvider : IDisposableExtended {
		void RegisterValidationServer(BlockchainType blockchainType, List<(DateTime appointment, TimeSpan window)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate);
		bool InAppointmentWindow { get; }
		bool InAppointmentWindowProximity { get; }
		bool IsInAppointmentWindow(DateTime appointment);
		void AddAppointmentWindow(DateTime appointment, TimeSpan window);
		void UnregisterValidationServer(BlockchainType blockchainType);
	}

	// the validation server use to operate an appointment validator
	public class AppointmentsValidatorProvider : IAppointmentsValidatorProvider {
		private ITcpValidatorServer validationServer = null;
		private Timer pollingTimer;

		protected virtual TimeSpan ServerPollDelay => TimeSpan.FromMinutes(1);
		public virtual void RegisterValidationServer(BlockchainType blockchainType, List<(DateTime appointment, TimeSpan window)> appointmentWindows, IAppointmentValidatorDelegate appointmentValidatorDelegate) {
			if(this.validationServer == null) {
				
				var ipMode = IPMode.IPv4;
				var address = IPAddress.Any;
				if(Socket.OSSupportsIPv6) {
					ipMode = IPMode.IPv6;
					address = IPAddress.IPv6Any;
				} else {
					ipMode = IPMode.IPv4;
					address = IPAddress.Any;
				}

				this.validationServer = new TcpValidatorServer(new NetworkEndPoint(address, GlobalSettings.ApplicationSettings.ValidatorPort, ipMode), ex => {
					Log.Error(ex, $"Error occued with validator server");
				});
			}

			this.validationServer.RegisterBlockchainDelegate(blockchainType, appointmentValidatorDelegate);

			foreach((DateTime appointment, TimeSpan window) in appointmentWindows) {
				this.AddAppointmentWindow(appointment, window);
			}

			if(this.pollingTimer == null) {
				this.pollingTimer = new Timer(state => {

					if(this.InAppointmentWindow) {
						// ok, we are inside a window, lets run the server
						if(!this.validationServer.IsRunning) {
							this.validationServer.Start();
						}
					} else {
						// outside of windows, no server
						this.validationServer?.Stop();
					}
					
				}, this, TimeSpan.FromSeconds(3), this.ServerPollDelay);
			}
		}

		public bool InAppointmentWindow => this.appointmentWindows.Any(a => a.start < DateTimeEx.CurrentTime && a.end >= DateTimeEx.CurrentTime);
		public bool InAppointmentWindowProximity => this.appointmentWindows.Any(a => a.start.AddMinutes(-5) < DateTimeEx.CurrentTime && a.end.AddMinutes(5) >= DateTimeEx.CurrentTime);

		public bool IsInAppointmentWindow(DateTime appointment) {
			return this.appointmentWindows.Any(a => a.start < appointment && a.end >= appointment && a.start < DateTimeEx.CurrentTime && a.end >= DateTimeEx.CurrentTime);
		}
		
		private readonly List<(DateTime start, DateTime end)> appointmentWindows = new List<(DateTime start, DateTime end)>();

		private void ClearExpiredAppointmentWindows() {
			foreach(var entry in this.appointmentWindows.Where(a => a.end < DateTimeEx.CurrentTime).ToArray()) {
				this.appointmentWindows.Remove(entry);
			}
		}
		
		public void AddAppointmentWindow(DateTime appointment, TimeSpan window) {
			this.ClearExpiredAppointmentWindows();

			int minutes = 5;
			DateTime start = DateTime.SpecifyKind(appointment.AddMinutes(-minutes), DateTimeKind.Utc);
			DateTime end = DateTime.SpecifyKind((appointment + window).AddMinutes(minutes), DateTimeKind.Utc);

			if(!this.appointmentWindows.Any(a => a.start == start && a.end == end)) {
				NLog.Default.Information($"Adding validation appointment window from {start} to {end} for appointment time {appointment}.");
				this.appointmentWindows.Add((start, end));
			}
		}
		
		public void UnregisterValidationServer(BlockchainType blockchainType) {
			
			this.validationServer?.UnregisterBlockchainDelegate(blockchainType);
			if(this.validationServer?.BlockchainDelegateEmpty??false) {

				this.pollingTimer?.Dispose();
				this.pollingTimer = null;
				
				this.validationServer?.Stop();
				this.validationServer?.Dispose();
				this.validationServer = null;
			}
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