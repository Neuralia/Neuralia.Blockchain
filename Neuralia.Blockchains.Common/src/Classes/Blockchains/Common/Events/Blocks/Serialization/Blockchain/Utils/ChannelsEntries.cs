using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils {
	public class ChannelsEntries<T> {

		public ChannelsEntries() {

		}

		public ChannelsEntries(BlockChannelUtils.BlockChannelTypes flag, T value) {

			this[flag] = value;

		}

		public ChannelsEntries(ChannelsEntries<T> other) : this(other, BlockChannelUtils.BlockChannelTypes.None) {
		}

		public ChannelsEntries(ChannelsEntries<T> other, BlockChannelUtils.BlockChannelTypes eclusions) : this(other.Entries, eclusions) {
		}

		
		public ChannelsEntries(IEnumerable<(BlockChannelUtils.BlockChannelTypes flag, T value)> entries) {
			foreach((BlockChannelUtils.BlockChannelTypes flag, T value) entry in entries) {
				this[entry.flag] = entry.value;
			}
		}

		public ChannelsEntries(BlockChannelUtils.BlockChannelTypes channels) {
			BlockChannelUtils.RunForFlags(channels, flag => {
				this.Entries[flag] = default;
			});
		}

		public ChannelsEntries(Dictionary<BlockChannelUtils.BlockChannelTypes, T> channels) : this(channels, BlockChannelUtils.BlockChannelTypes.None) {

		}
		
		public ChannelsEntries(Dictionary<BlockChannelUtils.BlockChannelTypes, T> channels, BlockChannelUtils.BlockChannelTypes exlusions) {
			foreach(var entry in channels) {
				if(!entry.Key.HasFlag(exlusions)) {
					this.Entries[entry.Key] = entry.Value;
				}
			}
		}

		public ChannelsEntries(IEnumerable<BlockChannelUtils.BlockChannelTypes> channels) {
			foreach(BlockChannelUtils.BlockChannelTypes flag in channels) {
				this.Entries[flag] = default;
			}
		}

		public ChannelsEntries(BlockChannelUtils.BlockChannelTypes channels, Func<BlockChannelUtils.BlockChannelTypes, T> action) {
			BlockChannelUtils.RunForFlags(channels, flag => {
				this.Entries[flag] = action(flag);
			});
		}

		public IEnumerable<BlockChannelUtils.BlockChannelTypes> EnabledChannels => this.Entries.Keys;
		public Dictionary<BlockChannelUtils.BlockChannelTypes, T> Entries { get; } = new Dictionary<BlockChannelUtils.BlockChannelTypes, T>();

		public T this[BlockChannelUtils.BlockChannelTypes i] {
			get => !this.Entries.ContainsKey(i) ? default : this.Entries[i];
			set {
				if(this.Entries.ContainsKey(i)) {
					this.Entries[i] = value;
				} else {
					this.Entries.Add(i, value);
				}
			}
		}

		public bool HasHighHeader => this.Entries.ContainsKey(BlockChannelUtils.BlockChannelTypes.HighHeader);
		public bool HasLowHeader => this.Entries.ContainsKey(BlockChannelUtils.BlockChannelTypes.LowHeader);
		public bool HasContents => this.Entries.ContainsKey(BlockChannelUtils.BlockChannelTypes.Contents);
		public bool HasErasables => this.Entries.ContainsKey(BlockChannelUtils.BlockChannelTypes.Erasables);
		public bool HasSlots => this.Entries.ContainsKey(BlockChannelUtils.BlockChannelTypes.Slots);

		public T HighHeaderData {
			get => this.HasHighHeader ? this.Entries[BlockChannelUtils.BlockChannelTypes.HighHeader] : default;
			set => this[BlockChannelUtils.BlockChannelTypes.HighHeader] = value;
		}

		public T LowHeaderData {
			get => this.HasLowHeader ? this.Entries[BlockChannelUtils.BlockChannelTypes.LowHeader] : default;
			set => this[BlockChannelUtils.BlockChannelTypes.LowHeader] = value;
		}

		public T ContentsData {
			get => this.HasContents ? this.Entries[BlockChannelUtils.BlockChannelTypes.Contents] : default;
			set => this[BlockChannelUtils.BlockChannelTypes.Contents] = value;
		}

		public T ErasablesData {
			get => this.HasErasables ? this.Entries[BlockChannelUtils.BlockChannelTypes.Erasables] : default;
			set => this[BlockChannelUtils.BlockChannelTypes.Erasables] = value;
		}

		public T SlotsData {
			get => this.HasSlots ? this.Entries[BlockChannelUtils.BlockChannelTypes.Slots] : default;
			set => this[BlockChannelUtils.BlockChannelTypes.Slots] = value;
		}

		public ChannelsEntries<K> ConvertAll<K>(Func<T, K> action, BlockChannelUtils.BlockChannelTypes excludeFlags = BlockChannelUtils.BlockChannelTypes.None) {

			var result = new ChannelsEntries<K>();

			this.RunForAll((flag, value) => {
				result[flag] = action(value);
			}, excludeFlags);

			return result;
		}

		public ChannelsEntries<K> ConvertAll<K>(Func<BlockChannelUtils.BlockChannelTypes, T, K> action, BlockChannelUtils.BlockChannelTypes excludeFlags = BlockChannelUtils.BlockChannelTypes.None) {

			var result = new ChannelsEntries<K>();

			this.RunForAll((flag, value) => {
				result[flag] = action(flag, value);
			}, excludeFlags);

			return result;
		}

		public void RunForAll(Action<BlockChannelUtils.BlockChannelTypes, T> action, BlockChannelUtils.BlockChannelTypes excludeFlags = BlockChannelUtils.BlockChannelTypes.None) {

			foreach(var entry in this.Entries.ToList()) {
				if(!excludeFlags.HasFlag(entry.Key)) {
					action(entry.Key, entry.Value);
				}
			}
		}

		public void RunForHighHeader(Func<T> action) {
			if(this.HasHighHeader) {
				this[BlockChannelUtils.BlockChannelTypes.HighHeader] = action();
			}
		}

		public void RunForLowHeader(Func<T> action) {
			if(this.HasLowHeader) {
				this[BlockChannelUtils.BlockChannelTypes.LowHeader] = action();
			}
		}

		public void RunForContents(Func<T> action) {
			if(this.HasContents) {
				this[BlockChannelUtils.BlockChannelTypes.Contents] = action();
			}
		}

		public void RunForErasables(Func<T> action) {
			if(this.HasErasables) {
				this[BlockChannelUtils.BlockChannelTypes.Erasables] = action();
			}
		}

		public void RunForSlots(Func<T> action) {
			if(this.HasSlots) {
				this[BlockChannelUtils.BlockChannelTypes.Slots] = action();
			}
		}

		public ChannelsEntries<T> GetSubset(IEnumerable<BlockChannelUtils.BlockChannelTypes> channels) {
			var subset = new ChannelsEntries<T>(channels);

			foreach(BlockChannelUtils.BlockChannelTypes entry in channels) {
				subset[entry] = this[entry];
			}

			return subset;
		}
	}
}