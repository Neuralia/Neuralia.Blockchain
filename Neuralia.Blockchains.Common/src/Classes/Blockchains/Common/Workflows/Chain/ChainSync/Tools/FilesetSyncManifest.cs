using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Structures;
using Neuralia.Blockchains.Components.Blocks;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools {

	public abstract class FilesetSyncManifest {
	}

	public abstract class FilesetSyncManifest<FILE_KEY, DATA_SLICE> : FilesetSyncManifest
		where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY> {

		public int Attempts { get; set; } = 1;
		public string Id { get; set; }
		public Dictionary<FILE_KEY, DataSlice> Files { get; } = new Dictionary<FILE_KEY, DataSlice>();
		public List<DATA_SLICE> Slices { get; } = new List<DATA_SLICE>();

		[JsonIgnore]
		public List<DATA_SLICE> RemainingSlices => this.Slices.Where(c => c.Downloaded == false).ToList();

		[JsonIgnore]
		public virtual bool IsComplete => this.Slices.All(c => c.Downloaded);

		public bool IsFileCompleted(FILE_KEY id) {

			return this.Slices.Where(s => s.fileSlices.ContainsKey(id)).All(s => s.Downloaded);
		}

		public abstract string FileKeyToString(FILE_KEY fileKey);

		public class SyncingDataSlice<FILE_KEY> {

			public Dictionary<FILE_KEY, DataSliceInfo> fileSlices = new Dictionary<FILE_KEY, DataSliceInfo>();

			public SyncingDataSlice() {
				this.Downloaded = false;
			}

			public Guid ClientGuid { get; set; }
			public int Hash { get; set; }

			public bool Downloaded { get; set; }
			public ushort sliceId { get; set; }

#if DEVNET || TESTNET
			// statistics
			public DateTime Start { get; set; }
			public DateTime End { get; set; }
#endif
		}
	}

	public abstract class FilesetSyncManifest<KEY, FILE_KEY, DATA_SLICE> : FilesetSyncManifest<FILE_KEY, DATA_SLICE>
		where DATA_SLICE : FilesetSyncManifest<FILE_KEY, DATA_SLICE>.SyncingDataSlice<FILE_KEY> {

		private readonly Func<string, KEY> getKey;

		private readonly Func<KEY, string> setKey;

		public FilesetSyncManifest(Func<KEY, string> setKey, Func<string, KEY> getKey) {
			this.setKey = setKey;
			this.getKey = getKey;
		}

		public KEY Key {
			get => this.getKey(this.Id);
			set => this.Id = this.setKey(value);
		}

		public abstract string KeyToString();
	}

	public class BlockFilesetSyncManifest : FilesetSyncManifest<BlockId, BlockChannelUtils.BlockChannelTypes, BlockFilesetSyncManifest.BlockSyncingDataSlice> {
		public bool IsWeb { get; set; } = false;

		public BlockFilesetSyncManifest() : base(BlockId.ToString, id => new BlockId(id)) {

		}

		public override string KeyToString() {
			return this.Key.ToString();
		}

		public override bool IsComplete => base.IsComplete || this.IsWeb;

		public override string FileKeyToString(BlockChannelUtils.BlockChannelTypes fileKey) {
			return fileKey.ToString();
		}

		public class BlockSyncingDataSlice : SyncingDataSlice<BlockChannelUtils.BlockChannelTypes> {
		}
	}

	public class DigestFilesetSyncManifest : FilesetSyncManifest<int, int, DigestFilesetSyncManifest.DigestSyncingDataSlice> {
		public DigestFilesetSyncManifest() : base(key => key.ToString(), id => int.Parse(id)) {

		}

		[JsonIgnore]
		public DataSliceInfo DigestInfo => this.Files[1];

		public bool IsDigestFileCompleted() {

			return this.Slices.Where(s => s.fileSlices.ContainsKey(1)).All(s => s.Downloaded);
		}

		public override string KeyToString() {
			return this.Key.ToString();
		}

		public override string FileKeyToString(int fileKey) {
			return fileKey.ToString();
		}

		public class DigestSyncingDataSlice : SyncingDataSlice<int> {
		}
	}

	public class ChannelsFilesetSyncManifest : FilesetSyncManifest<int, ChannelFileSetKey, ChannelsFilesetSyncManifest.ChannelsSyncingDataSlice> {

		public ChannelsFilesetSyncManifest() : base(key => key.ToString(), id => int.Parse(id)) {
			this.Id = "1"; // not really used
		}

		public override string KeyToString() {
			return this.Key.ToString();
		}

		public override string FileKeyToString(ChannelFileSetKey fileKey) {
			return fileKey.ToString();
		}

		public class ChannelsSyncingDataSlice : SyncingDataSlice<ChannelFileSetKey> {
		}
	}
}