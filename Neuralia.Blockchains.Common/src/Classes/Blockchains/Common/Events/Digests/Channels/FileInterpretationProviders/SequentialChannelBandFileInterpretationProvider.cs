
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Zio;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	public interface ISequentialChannelBandFileInterpretationProvider {
		SafeArrayHandle QueryCard(uint offset, uint length);
	}

	public interface ISequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER> : IDigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ISequentialChannelBandFileInterpretationProvider
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {
	}

	public class SequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER> : DigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ISequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER>
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {

		public SequentialChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider, FileSystemWrapper fileSystem) : base(namingProvider, fileSystem) {

		}

		public SafeArrayHandle QueryCard(uint offset, uint length) {

			if(offset > this.fileSystem.GetFileLength(this.ActiveFullFilename)) {
				return null;
			}

			return FileExtensions.ReadBytes(this.ActiveFullFilename, offset, (int) length, this.fileSystem);
		}
	}
}