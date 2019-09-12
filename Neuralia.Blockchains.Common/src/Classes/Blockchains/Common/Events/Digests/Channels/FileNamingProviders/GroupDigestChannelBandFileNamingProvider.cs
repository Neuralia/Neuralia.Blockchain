namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders {
	public class GroupDigestChannelBandFileNamingProvider<GROUP_TYPE> : DigestChannelBandFileNamingProvider {

		public const string GROUP_FILE_MASK = "{0}-groups/{1}.{2}";

		public override string GeneratedExpandedFileName(string bandName, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_FILE_MASK, bandName, bandName, parameters[0]);

			return this.GenerateExpandedName(scoppedName, scopeFolder);
		}

		public override string GeneratedArchivedFileName(string bandName, string scopeFolder, object[] parameters) {

			string scoppedName = string.Format(GROUP_FILE_MASK, bandName, bandName, parameters[0]);

			return this.GenerateArchivedName(scoppedName, scopeFolder);
		}
	}
}