using System;
using System.Collections.Generic;
using System.IO;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels {
	public interface IDigestChannelValidator {
		Dictionary<int, Dictionary<int, SafeArrayHandle>> HashChannel(int groupIndex);

		SafeArrayHandle GetFileBytes(int indexId, int bandId, uint partIndex, long offset, int length);
	}

	public interface IDigestChannel : IVersionable<DigestChannelType>, IDigestChannelValidator {
		DigestChannelType ChannelType { get; }

		Dictionary<int, List<int>> GetFileTypes();

		void Initialize();

		SafeArrayHandle DehydrateVersionInfo();
	}

	public abstract class DigestChannel<CHANEL_BANDS, CARD, KEY, INPUT_QUERY_KEY, QUERY_KEY> : Versionable<DigestChannelType>, IDigestChannel
		where CHANEL_BANDS : struct, Enum
		where CARD : class
		where KEY : struct, IEquatable<KEY> {

		public const string CHANNELS_FOLDER = "channels";
		protected readonly string baseFolder;

		protected readonly DigestChannelBandIndexSet<CHANEL_BANDS, CARD, KEY, INPUT_QUERY_KEY, QUERY_KEY> channelBandIndexSet;
		protected readonly string scopeFolder;

		public DigestChannel(string folder, string channelName) {

			//TODO: the entire digest channel structure was developped in a rush in 2 days, so its really clunky. It needs a major refactor.
			this.baseFolder = folder;
			this.scopeFolder = Path.Combine(CHANNELS_FOLDER, channelName);

			this.channelBandIndexSet = new DigestChannelBandIndexSet<CHANEL_BANDS, CARD, KEY, INPUT_QUERY_KEY, QUERY_KEY>();
		}

		public long Size { get; set; }

		public DigestChannelType Type { get; protected set; }
		public byte Major { get; protected set; }
		public byte Minor { get; protected set; }

		public SafeArrayHandle DehydrateVersionInfo() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Version.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public abstract DigestChannelType ChannelType { get; }

		public virtual void Initialize() {
			this.BuildBandsIndices();
		}

		public Dictionary<int, Dictionary<int, SafeArrayHandle>> HashChannel(int groupIndex) {
			return this.channelBandIndexSet.HashIndexes(groupIndex);
		}

		public virtual Dictionary<int, List<int>> GetFileTypes() {
			return this.channelBandIndexSet.GetFileTypes();
		}

		public SafeArrayHandle GetFileBytes(int indexId, int fileId, uint partIndex, long offset, int length) {
			return this.channelBandIndexSet.BandIndices[indexId].GetFileBytes(fileId, partIndex, offset, length);
		}

		protected abstract void BuildBandsIndices();
	}
}