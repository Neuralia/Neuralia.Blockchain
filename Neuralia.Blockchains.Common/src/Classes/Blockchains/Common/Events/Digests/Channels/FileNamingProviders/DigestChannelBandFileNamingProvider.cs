using System.IO;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders {
	public abstract class DigestChannelBandFileNamingProvider {

		public const string EXPANDED_DIRECTORY_NAME = "operations";
		public const string ARCHIVE_DIRECTORY_NAME = "packed";

		public const string EXPANDED_FILE_MASK = "{0}.neuralia";
		public const string ARCHIVE_FILE_MASK = "{0}.arch";
		public abstract string GeneratedExpandedFileName(string bandName, string scope, object[] parameters);
		public abstract string GeneratedArchivedFileName(string bandName, string scope, object[] parameters);

		public string GenerateExpandedName(string filename, string scopeFolder) {
			return Path.Combine(EXPANDED_DIRECTORY_NAME, Path.Combine(scopeFolder, string.Format(EXPANDED_FILE_MASK, filename)));
		}

		public string GenerateArchivedName(string filename, string scopeFolder) {
			return Path.Combine(ARCHIVE_DIRECTORY_NAME, Path.Combine(scopeFolder, string.Format(ARCHIVE_FILE_MASK, filename)));
		}
	}
}