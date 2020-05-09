using System.IO;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders {
	public class GroupDigestChannelBandFileNamingProvider<GROUP_TYPE> : DigestChannelBandFileNamingProvider {

		public const string GROUP_MASK = "{0}-groups/{}";
		public const string GROUP_FILE_MASK = "{0}-groups/{2}/{1}.{2}";

		public override string GeneratedExpandedFolderName(string bandName, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_MASK, bandName.ToLower(), parameters[0]);

			return this.GeneratedExpandedFolderName(Path.Combine(scopeFolder, scoppedName));
		}

		public override string GeneratedArchivedFolderName(string bandName, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_MASK, bandName.ToLower(), parameters[0]);

			return this.GeneratedArchivedFolderName(Path.Combine(scopeFolder, scoppedName));
		}

		public override string GeneratedExpandedFileName(string bandName, string filename, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_FILE_MASK, bandName.ToLower(), filename, parameters[0]);

			return this.GenerateExpandedName(scoppedName, scopeFolder);
		}

		public override string GeneratedArchivedFileName(string bandName, string filename, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_FILE_MASK, bandName.ToLower(), filename, parameters[0]);

			return this.GenerateArchivedName(scoppedName, scopeFolder);
		}
	}
}