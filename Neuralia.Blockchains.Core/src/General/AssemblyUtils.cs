using System;
using System.Reflection;

namespace Neuralia.Blockchains.Core.General {
	public static class AssemblyUtils {

		public static DateTime GetBuildTimestamp(Type assemblyType) {
			Version version = Assembly.GetAssembly(assemblyType).GetName().Version;

			return new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.MinorRevision * 2);
		}
	}
}