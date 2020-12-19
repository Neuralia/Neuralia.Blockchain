using System;
using System.Collections.Generic;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Index;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils {
	public class DigestChannelBandIndexSet<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY>
		where CHANEL_BANDS : struct, Enum
		where CARD_TYPE : class
		where KEY : struct, IEquatable<KEY> {

		public DigestChannelBandIndexSet() {
			foreach(CHANEL_BANDS flag in EnumsUtils.GetSimpleEntries<CHANEL_BANDS>()) {

			}
		}

		public Dictionary<int, IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY>> BandIndices { get; } = new Dictionary<int, IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY>>();

		public void AddIndex(int key, IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY> bandIndex) {
			bandIndex.Initialize();
			this.BandIndices.Add(key, bandIndex);
		}

		public Dictionary<int, Dictionary<int, SafeArrayHandle>> HashIndexes(int groupIndex) {

			Dictionary<int, Dictionary<int, SafeArrayHandle>> indicesResults = new Dictionary<int, Dictionary<int, SafeArrayHandle>>();

			foreach(KeyValuePair<int, IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY>> index in this.BandIndices) {

				indicesResults.Add(index.Key, index.Value.HashFiles(groupIndex));
			}

			return indicesResults;
		}

		public Dictionary<int, List<int>> GetFileTypes() {
			Dictionary<int, List<int>> fileTypes = new Dictionary<int, List<int>>();

			foreach(KeyValuePair<int, IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY>> index in this.BandIndices) {

				fileTypes.Add(index.Key, index.Value.GetFileTypes());
			}

			return fileTypes;
		}

		public DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> QueryCard(INPUT_QUERY_KEY key) {

			DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> results = new DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS>();

			foreach(IDigestChannelBandIndex<CHANEL_BANDS, CARD_TYPE, KEY, INPUT_QUERY_KEY, QUERY_KEY> index in this.BandIndices.Values) {

				DigestChannelBandEntries<CARD_TYPE, CHANEL_BANDS> subResults = index.QueryCard(key);

				if(subResults != null) {
					subResults.Entries.ForEach(entry => results[entry.Key] = entry.Value);
				}
			}

			return results;
		}
	}
}