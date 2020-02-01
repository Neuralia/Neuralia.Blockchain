using System;
using System.IO;
using System.Runtime.InteropServices;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Core {
	public static class FileUtilities {

		public static string getUserHomePath() {
			if((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX)) {
				return Environment.GetEnvironmentVariable("HOME");
			}

			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				// windows
				if(Environment.OSVersion.Version.Major <= 5) {
					return Environment.ExpandEnvironmentVariables("%USERPROFILE%");
				}

				return Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
			}

			throw new ApplicationException("Operating system not recognized");
		}

		public static string GetSystemFilesPath() {
			return Path.Combine(getUserHomePath(), GlobalsService.DEFAULT_SYSTEM_FILES_FOLDER_NAME);
		}
	}
}