namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders {
	public class SingleDigestChannelBandFileNamingProvider : DigestChannelBandFileNamingProvider {

		public override string GeneratedExpandedFileName(string bandName, string scope, object[] parameters) {

			return this.GenerateExpandedName(bandName, scope);
		}

		public override string GeneratedArchivedFileName(string bandName, string scope, object[] parameters) {

			return this.GenerateArchivedName(bandName, scope);
		}
	}
}