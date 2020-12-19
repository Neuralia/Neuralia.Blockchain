using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Tools {

	/// <summary>
	///     Tools to manage and limit memory
	/// </summary>
	public sealed class MemoryCheck {

		private long cachedCurrentMemory;
		private long cachedVirtualMemory;
		private DateTime lastCheck = DateTimeEx.MinValue;

		private Process myProcess;

		/// <summary>
		/// Ability to override the mechanism by which we check for memory
		/// </summary>
		public Func<Task<(long currentMemory, long totalMemory)>> GetMemoryUsage = null;
		
		public void RegisterCGroupMemoryCheck() {
			this.GetMemoryUsage = async () => {
				long currentMemory = 0;
				long totalMemory = 0;

				if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
					try {
					#region other
						// var totalMemoryData = ((await File.ReadAllLinesAsync("/sys/fs/cgroup/memory/memory.stat").ConfigureAwait(false)).SingleOrDefault(l => l.StartsWith("hierarchical_memory_limit", StringComparison.InvariantCulture)))?.Split(" ")?? Array.Empty<string>();
						//
						// if(totalMemoryData.Length == 2) {
						// 	string value = totalMemoryData[1];
						//
						// 	if(!string.IsNullOrWhiteSpace(value)) {
						// 		if(!long.TryParse(value, out totalMemory)) {
						// 	
						// 		}
						// 	}
						// }
						#endregion
						var totalMemoryData = (await File.ReadAllLinesAsync("/sys/fs/cgroup/memory/memory.limit_in_bytes").ConfigureAwait(false)).Single();

						if(!string.IsNullOrWhiteSpace(totalMemoryData)) {
							if(!long.TryParse(totalMemoryData, out totalMemory)) {
						
							}
						}
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to query total memory bytes");
					}
			
					try {
						var currentMemoryData = (await File.ReadAllLinesAsync("/sys/fs/cgroup/memory/memory.usage_in_bytes").ConfigureAwait(false)).Single();

						if(!string.IsNullOrWhiteSpace(currentMemoryData)) {
							if(!long.TryParse(currentMemoryData, out currentMemory)) {
						
							}
						}
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to query current usage bytes");
					}

				} else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
					//TODO: implement this!
					throw new NotImplementedException();
				}

				return (currentMemory, totalMemory);
			};
		}
		
		/// <summary>
		///     make sure we dont query too often. we can cache the values for a little while
		/// </summary>
		/// <param name="useVirtualMemory"></param>
		private async Task UpdateProcessInfo(bool useVirtualMemory) {
			
			if(this.lastCheck.AddSeconds(30) < DateTime.Now) {

				if(this.GetMemoryUsage != null) {
					(this.cachedCurrentMemory, this.cachedVirtualMemory) = await this.GetMemoryUsage().ConfigureAwait(false);
				} else {
					if(this.myProcess == null) {
						this.myProcess = Process.GetCurrentProcess();
					}

					this.cachedCurrentMemory = this.myProcess.WorkingSet64;

					if(useVirtualMemory) {
						this.cachedVirtualMemory = this.myProcess.VirtualMemorySize64;
					}
				}

				this.lastCheck = DateTime.Now;
			}
		}

		/// <summary>
		///     check memory used versus available, and detect potential out of memory issues.
		/// </summary>
		/// <param name="appSettingsBase"></param>
		/// <returns></returns>
		public async Task<bool> CheckAvailableMemory(AppSettingsBase appSettingsBase) {
			try {
				if(appSettingsBase.MemoryLimitCheckMode == AppSettingsBase.MemoryCheckModes.Disabled) {
					return true;
				}

				if(appSettingsBase.MemoryLimitCheckMode == AppSettingsBase.MemoryCheckModes.CGroup && this.GetMemoryUsage == null) {
					this.RegisterCGroupMemoryCheck();
				}
				
				//TODO: using VirtualMemorySize64 is not right. we should investigate the best way to get true total memory dynamically
				await this.UpdateProcessInfo(appSettingsBase.TotalUsableMemory == 0).ConfigureAwait(false);

				long currentMemory = this.cachedCurrentMemory;
				long virtualMemory = appSettingsBase.TotalUsableMemory;

				if(virtualMemory == 0) {
					virtualMemory = this.cachedVirtualMemory;
				}
				if(virtualMemory == 0) {
					// just in case
					virtualMemory = 1;
				}
				double percent = Math.Round((double) currentMemory / virtualMemory, 6);
				double percentAdjusted = Math.Round(percent * 100, 4);

				NLog.Default.Verbose($"Verifying memory. Current memory used: {FormatBytes(currentMemory)}. Total available memory: {FormatBytes(virtualMemory)}. Using {percentAdjusted}% of the available memory. Circuit breaker will trigger at {appSettingsBase.MemoryLimit*100}% or {FormatBytes((long)(virtualMemory*appSettingsBase.MemoryLimit))}.");

				if(percent > appSettingsBase.MemoryLimit) {
					// ok, we are low on memory, we shut down
					NLog.Default.Fatal($"we are using {percentAdjusted}% of available memory. running ultra low.");

					return false;
				}

				if(percent > appSettingsBase.MemoryLimitWarning) {
					NLog.Default.Warning($"We are using {percentAdjusted}% of available memory. running low...");
				}
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to check available memory!");
			}

			return true;
		}

		public static string FormatBytes(long length) {
			string[] sizes = {"B", "KB", "MB", "GB", "TB"};

			var order = 0;

			while((length >= 1024) && (order < (sizes.Length - 1))) {
				order++;
				length /= 1024;
			}

			return $"{length:0.##} {sizes[order]}";
		}

	#region Singleton

		static MemoryCheck() {
		}

		private MemoryCheck() {
		}

		public static MemoryCheck Instance { get; } = new MemoryCheck();

	#endregion

	}
}