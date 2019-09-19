using System.IO.Abstractions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders {

	public interface ISequentialChannelBandFileInterpretationProvider {
		SafeArrayHandle QueryCard(uint offset, uint length);
	}

	public interface ISequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER> : IDigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ISequentialChannelBandFileInterpretationProvider
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {
	}

	public class SequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER> : DigestChannelBandFileInterpretationProvider<SafeArrayHandle, NAMING_PROVIDER>, ISequentialChannelBandFileInterpretationProvider<NAMING_PROVIDER>
		where NAMING_PROVIDER : DigestChannelBandFileNamingProvider {

		public SequentialChannelBandFileInterpretationProvider(NAMING_PROVIDER namingProvider, IFileSystem fileSystem) : base(namingProvider, fileSystem) {

		}

		public SafeArrayHandle QueryCard(uint offset, uint length) {

			if(offset > this.fileSystem.FileInfo.FromFileName(this.ActiveFullFilename).Length) {
				return null;
			}

			return FileExtensions.ReadBytes(this.ActiveFullFilename, offset, (int) length, this.fileSystem);
		}
	}
}