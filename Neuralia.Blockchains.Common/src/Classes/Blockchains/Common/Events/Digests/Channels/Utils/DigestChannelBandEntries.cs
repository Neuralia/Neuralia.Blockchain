using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils {
	public class DigestChannelBandEntries<T, CHANEL_BANDS>
		where CHANEL_BANDS : struct, Enum {

		public DigestChannelBandEntries() {

		}

		public DigestChannelBandEntries(CHANEL_BANDS bands, T value) {

			this[bands] = value;

		}

		public DigestChannelBandEntries(DigestChannelBandEntries<T, CHANEL_BANDS> other) : this(other.Entries) {
		}

		public DigestChannelBandEntries(IEnumerable<(CHANEL_BANDS bands, T value)> entries) {
			foreach((CHANEL_BANDS bands, T value) entry in entries) {
				this[entry.bands] = entry.value;
			}
		}

		public DigestChannelBandEntries(CHANEL_BANDS channels) {
			EnumsUtils.RunForFlags(channels, bands => {
				this.Entries[bands] = default;
			});
		}

		public DigestChannelBandEntries(Dictionary<CHANEL_BANDS, T> channels) {
			foreach(KeyValuePair<CHANEL_BANDS, T> entry in channels) {
				this.Entries[entry.Key] = entry.Value;
			}
		}

		public DigestChannelBandEntries(IEnumerable<CHANEL_BANDS> channels) {
			foreach(CHANEL_BANDS bands in channels) {
				this.Entries[bands] = default;
			}
		}

		public DigestChannelBandEntries(CHANEL_BANDS channels, Func<CHANEL_BANDS, T> action) {
			EnumsUtils.RunForFlags(channels, bands => {
				this.Entries[bands] = action(bands);
			});
		}

		public bool IsEmpty => !this.Entries.Any();
		public Dictionary<CHANEL_BANDS, T> Entries { get; } = new Dictionary<CHANEL_BANDS, T>();

		public T this[CHANEL_BANDS i] {
			get => !this.Entries.ContainsKey(i) ? default : this.Entries[i];
			set {
				if(this.Entries.ContainsKey(i)) {
					this.Entries[i] = value;
				} else {
					this.Entries.Add(i, value);
				}
			}
		}

		public DigestChannelBandEntries<K, CHANEL_BANDS> ConvertAll<K>(Func<T, K> action, CHANEL_BANDS excludeFlags) {

			DigestChannelBandEntries<K, CHANEL_BANDS> result = new DigestChannelBandEntries<K, CHANEL_BANDS>();

			this.RunForAll((bands, value) => {
				result[bands] = action(value);
			}, excludeFlags);

			return result;
		}

		public void RunForAll(Action<CHANEL_BANDS, T> action, CHANEL_BANDS excludeFlags) {

			foreach(KeyValuePair<CHANEL_BANDS, T> entry in this.Entries.ToList()) {
				if(!excludeFlags.HasFlag(entry.Key)) {
					action(entry.Key, entry.Value);
				}
			}
		}

		//		public void RunForHeader(Func<T> action) {
		//			if(this.HasHeader) {
		//				this[CHANEL_BANDS.Header] = action();
		//			}
		//		}
		//		
		//		public void RunForContents(Func<T> action) {
		//			if(this.HasContents) {
		//				this[CHANEL_BANDS.Contents] = action();
		//			}
		//		}
		//		
		//		public void RunForErasables(Func<T> action) {
		//			if(this.HasErasables) {
		//				this[CHANEL_BANDS.Erasables] = action();
		//			}
		//		}
		//		
		//		public void RunForSlots(Func<T> action) {
		//			if(this.HasSlots) {
		//				this[CHANEL_BANDS.Slots] = action();
		//			}
		//		}
		//
		//		public bool HasHeader => this.Entries.ContainsKey(CHANEL_BANDS.Header);
		//		public bool HasContents => this.Entries.ContainsKey(CHANEL_BANDS.Contents);
		//		public bool HasErasables => this.Entries.ContainsKey(CHANEL_BANDS.Erasables);
		//		public bool HasSlots => this.Entries.ContainsKey(CHANEL_BANDS.Slots);
		//		
		//		public T HeaderData {
		//			get => this.HasHeader ? this.Entries[CHANEL_BANDS.Header] : default;
		//			set => this[CHANEL_BANDS.Header] = value;
		//		}
		//		public T ContentsData {
		//			get => this.HasContents ? this.Entries[CHANEL_BANDS.Contents] : default;
		//			set => this[CHANEL_BANDS.Contents] = value;
		//		}
		//		
		//		public T ErasablesData {
		//			get => this.HasErasables ? this.Entries[CHANEL_BANDS.Erasables] : default;
		//			set => this[CHANEL_BANDS.Erasables] = value;
		//		}
		//		public T SlotsData {
		//			get => this.HasSlots ? this.Entries[CHANEL_BANDS.Slots] : default;
		//			set => this[CHANEL_BANDS.Slots] = value;
		//		}

		public DigestChannelBandEntries<T, CHANEL_BANDS> GetSubset(IEnumerable<CHANEL_BANDS> channels) {
			DigestChannelBandEntries<T, CHANEL_BANDS> subset = new DigestChannelBandEntries<T, CHANEL_BANDS>(channels);

			foreach(CHANEL_BANDS entry in channels) {
				subset[entry] = this[entry];
			}

			return subset;
		}
	}
}