using System.IO;

using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Tools;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	public interface IDigestChannelBandFileInterpretationProvider<CARD_TYPE, out NAMING_PROVIDER>
		where CARD_TYPE : class
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {

		NAMING_PROVIDER NamingProvider { get; }

		string ActiveFullFilename { get; }
		string ActiveFilename { get; }
		string ActiveFolder { get; }
		void SetActiveFilename(string filename, string folder);

		void SetActiveFilename(string fullfilename);
	}

	public abstract class DigestChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER> : IDigestChannelBandFileInterpretationProvider<CARD_TYPE, NAMING_PROVIDER>
		where CARD_TYPE : class
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {

		protected readonly FileSystemWrapper fileSystem;

		protected DigestChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider, FileSystemWrapper fileSystem) {
			this.NamingProvider = namingProvider;
			this.fileSystem = fileSystem;
		}

		public string ActiveFullFilename { get; private set; }

		public string ActiveFilename { get; private set; }
		public string ActiveFolder { get; private set; }

		public NAMING_PROVIDER NamingProvider { get; }

		public void SetActiveFilename(string filename, string folder) {
			this.ActiveFilename = filename;
			this.ActiveFolder = folder;

			this.ActiveFullFilename = Path.Combine(this.ActiveFolder, this.ActiveFilename);
		}

		public void SetActiveFilename(string fullfilename) {
			this.SetActiveFilename(Path.GetFileName(fullfilename), Path.GetDirectoryName(fullfilename));
		}
	}
}