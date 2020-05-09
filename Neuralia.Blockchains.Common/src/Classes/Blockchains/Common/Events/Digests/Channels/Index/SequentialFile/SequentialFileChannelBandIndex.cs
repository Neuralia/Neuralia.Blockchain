using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileNamingProviders;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index.SequentialFile {
	public abstract class SequentialFileChannelBandIndex<CHANEL_BANDS, INPUT_KEY> : DigestChannelBandIndex<CHANEL_BANDS, SafeArrayHandle, int, INPUT_KEY, (uint offset, uint length), GroupDigestChannelBandFileNamingProvider<uint>>
		where CHANEL_BANDS : struct, Enum, IConvertible {

		protected readonly ImmutableList<CHANEL_BANDS> EnabledBands;

		protected SequentialFileChannelBandIndex(string filename, string baseFolder, string scopeFolder, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem) : base(filename, baseFolder, scopeFolder, enabledBands, fileSystem) {

			List<CHANEL_BANDS> enabledBandsList = new List<CHANEL_BANDS>();

			EnumsUtils.RunForFlags(this.enabledBands, band => {

				enabledBandsList.Add(band);

			});

			this.EnabledBands = enabledBandsList.ToImmutableList();
		}

		protected SequentialFileChannelBandIndex(List<(string filename, CHANEL_BANDS band)> filenames, string baseFolder, string scopeFolder, CHANEL_BANDS enabledBands, FileSystemWrapper fileSystem) : base("", baseFolder, scopeFolder, enabledBands, fileSystem) {
		}

		protected CHANEL_BANDS ChannelBand => this.Providers.Single().Key;

		public DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> QueryFiles(DigestChannelBandEntries<(uint offset, uint length), CHANEL_BANDS> offsets, uint index) {
			DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS> results = new DigestChannelBandEntries<SafeArrayHandle, CHANEL_BANDS>(this.enabledBands);

			foreach(KeyValuePair<CHANEL_BANDS, IDigestChannelBandFileInterpretationProvider<SafeArrayHandle, GroupDigestChannelBandFileNamingProvider<uint>>> providerSet in this.Providers) {
				SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>> provider = (SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>>) providerSet.Value;

				provider.SetActiveFilename(this.GetExpandedBandName(providerSet.Key, index));

				(uint offset, uint length) entry = offsets[providerSet.Key];
				results[providerSet.Key] = provider.QueryCard(entry.offset, entry.length);
			}

			return results;
		}

		//				string expandedFilename = this.GenerateFullPath(provider.NamingProvider.GeneratedExpandedFileName(providerSet.Key.ToString(),this.scopeFolder, new object[]{index}));

		public override void Initialize() {
			base.Initialize();

			this.CreateProviders();
			this.CreateIndexProviders();
		}

		public override List<int> GetFileTypes() {
			return this.EnabledBands.Select(b => b.ToInt32(null)).ToList();
		}

		protected virtual void CreateProviders() {
			foreach(CHANEL_BANDS band in this.EnabledBands) {
				this.Providers.Add(band, new SequentialChannelBandFileInterpretationProvider<GroupDigestChannelBandFileNamingProvider<uint>>(new GroupDigestChannelBandFileNamingProvider<uint>(), this.fileSystem));
			}
		}

		protected virtual void CreateIndexProviders() {

		}

		protected virtual List<string> EnsureFilesetExtracted(uint index) {

			List<(string bandName, object[] parameters, CHANEL_BANDS band)> entries = new List<(string bandName, object[] parameters, CHANEL_BANDS band)>();

			EnumsUtils.RunForFlags(this.enabledBands, band => {

				entries.Add((band.ToString(), new object[] {index}, band));
			});

			List<string> results = this.EnsureFilesetExtracted(entries.ToArray()).Select(r => r.extractedName).ToList();

			results.AddRange(this.EnsureIndexFilesetExtracted(index));

			return results;
		}

		protected abstract List<string> EnsureIndexFilesetExtracted(uint index);
	}
}