using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Services;

namespace Neuralia.Blockchains.Core {
	public static class FileUtilities {

		public enum PathType
		{
			UserHome,
			ExecutingDirectory,
		}

		private static Func<string, int, Task<byte[]>> getExternalJsLibraryResource;
		public static void SetExternalJsLibraryResourceFunc(Func<string, int, Task<byte[]>> func)
			=> getExternalJsLibraryResource = func;
		public static Task<byte[]> GetExternalJsLibraryResource(string fileName, int engineVersion)
			=> getExternalJsLibraryResource?.Invoke(fileName, engineVersion);

		public static void ConfigureEmbeddedResourcesLocation(Func<Assembly> getAssemblyFunc, string resourcesRoot)
		{
			if (getAssemblyFunc == null)
				throw new ArgumentNullException(nameof(getAssemblyFunc));
			if (resourcesRoot == null)
				throw new ArgumentNullException(nameof(resourcesRoot));

			getEmbeddedResourceAssemblyFunc = getAssemblyFunc;
			embeddedResourcesRoot = resourcesRoot;
		}

		private static Func<Assembly> getEmbeddedResourceAssemblyFunc;
		public static Assembly GetEmbeddedResourceAssembly()
        {
			if (getEmbeddedResourceAssemblyFunc != null)
				return getEmbeddedResourceAssemblyFunc();
			else
				return null;
		}

		private static string embeddedResourcesRoot;
		public static string GetEmbeddedResourceRootLocation()
        {
			return embeddedResourcesRoot;
		}

		private static string userHomePath;
        public static string getUserHomePath() {
			if (!string.IsNullOrWhiteSpace(userHomePath))
				return userHomePath;

			if((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX)) {
				userHomePath = Environment.GetEnvironmentVariable("HOME");
			}
			else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				// windows
				if(Environment.OSVersion.Version.Major <= 5) {
					userHomePath = Environment.ExpandEnvironmentVariables("%USERPROFILE%");
				}
				else
					userHomePath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
			}
			else
				throw new ApplicationException("Operating system not recognized");

			return userHomePath;
		}

		public static string GetSystemFilesPath() {
			return Path.Combine(getUserHomePath(), GlobalsService.DEFAULT_SYSTEM_FILES_FOLDER_NAME);
		}

		private static string executingDirectoryPath;
		public static string GetExecutingDirectory()
        {
			return !string.IsNullOrWhiteSpace(executingDirectoryPath) ? executingDirectoryPath : AppDomain.CurrentDomain.BaseDirectory;
		}

		public static void UpdatePath(PathType pathType, string path)
		{
			switch (pathType)
            {
				case PathType.UserHome:
					userHomePath = path;
					break;
				case PathType.ExecutingDirectory:
					executingDirectoryPath = path;
					break;
				default:
					throw new InvalidEnumArgumentException(nameof(pathType), (int)pathType, typeof(PathType));
			}
		}
	}
}