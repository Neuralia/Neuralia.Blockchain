using System.IO;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders {
	public class SingleDigestChannelBandFileNamingProvider : DigestChannelBandFileNamingProvider {

		public override string GeneratedExpandedFolderName(string bandName, string scope, object[] parameters) {
			return this.GeneratedExpandedFolderName(Path.Combine(scope, bandName.ToLower()));
		}

		public override string GeneratedArchivedFolderName(string bandName, string scope, object[] parameters) {
			return this.GeneratedArchivedFolderName(Path.Combine(scope, bandName.ToLower()));
		}

		public override string GeneratedExpandedFileName(string bandName, string filename, string scope, object[] parameters) {

			return this.GenerateExpandedName(bandName, scope);
		}

		public override string GeneratedArchivedFileName(string bandName, string filename, string scope, object[] parameters) {

			return this.GenerateArchivedName(bandName, scope);
		}
	}
}