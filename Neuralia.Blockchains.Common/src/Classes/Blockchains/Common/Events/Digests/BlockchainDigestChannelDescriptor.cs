using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {
	public class BlockchainDigestDescriptor : ITreeHashable, IBinaryRehydratable {
		public Dictionary<DigestChannelType, BlockchainDigestChannelDescriptor> Channels { get; } = new Dictionary<DigestChannelType, BlockchainDigestChannelDescriptor>();

		public IByteArray Hash { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.Hash = rehydrator.ReadNonNullableArray();
			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				DigestChannelType type = rehydrator.ReadUShort();
				BlockchainDigestChannelDescriptor channel = new BlockchainDigestChannelDescriptor();
				channel.Rehydrate(rehydrator);

				this.Channels.Add(type, channel);
			}
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Hash);

			foreach(var channel in this.Channels.OrderBy(c => c.Key)) {
				nodeList.Add(channel.Key.Value);
				nodeList.Add(channel.Value);
			}

			return nodeList;
		}
	}

	public class BlockchainDigestChannelDescriptor : BlockchainDigestSimpleChannelDescriptor, ITreeHashable {

		public IByteArray Hash { get; set; }

		public Dictionary<int, DigestChannelIndexDescriptor> DigestChannelIndexDescriptors { get; } = new Dictionary<int, DigestChannelIndexDescriptor>();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.GroupSize);
			nodeList.Add(this.TotalEntries);
			nodeList.Add(this.LastEntryId);
			nodeList.Add(this.Hash);

			foreach(var channel in this.DigestChannelIndexDescriptors.OrderBy(c => c.Key)) {
				nodeList.Add(channel.Key);
				nodeList.Add(channel.Value);
			}

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);
			this.Hash = rehydrator.ReadNonNullableArray();

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				int type = rehydrator.ReadInt();
				DigestChannelIndexDescriptor channelIndexDescriptor = new DigestChannelIndexDescriptor();
				channelIndexDescriptor.Rehydrate(rehydrator);

				this.DigestChannelIndexDescriptors.Add(type, channelIndexDescriptor);
			}
		}

		public class DigestChannelIndexDescriptor : ITreeHashable, IBinaryRehydratable {

			public IByteArray Hash { get; set; }

			public Dictionary<int, DigestChannelIndexFileDescriptor> Files { get; } = new Dictionary<int, DigestChannelIndexFileDescriptor>();

			public void Rehydrate(IDataRehydrator rehydrator) {

				this.Hash = rehydrator.ReadNonNullableArray();

				int count = rehydrator.ReadInt();

				for(int i = 0; i < count; i++) {
					int type = rehydrator.ReadInt();
					DigestChannelIndexFileDescriptor file = new DigestChannelIndexFileDescriptor();
					file.Rehydrate(rehydrator);

					this.Files.Add(type, file);
				}
			}

			public HashNodeList GetStructuresArray() {
				HashNodeList nodeList = new HashNodeList();

				nodeList.Add(this.Hash);

				foreach(var channel in this.Files.OrderBy(c => c.Key)) {
					nodeList.Add(channel.Key);
					nodeList.Add(channel.Value);
				}

				return nodeList;
			}

			public class DigestChannelIndexFileDescriptor : ITreeHashable, IBinaryRehydratable {

				public enum DigestChannelHashingTypes : byte {
					File = 1,
					Internal = 2
				}

				public IByteArray Hash { get; set; }

				/// <summary>
				///     If true, the channel files may be missing and it will be acceptable. He hash will be used only.
				/// </summary>
				public bool IsOptional { get; set; }

				/// <summary>
				///     How do we hash the channel. as file, we hash the file data. Otherwise we may need to expand the file and hash its
				///     internals.
				/// </summary>
				public DigestChannelHashingTypes HashingType { get; set; } = DigestChannelHashingTypes.File;

				public Dictionary<uint, DigestChannelIndexFilePartDescriptor> DigestChannelIndexFilePartDescriptors { get; } = new Dictionary<uint, DigestChannelIndexFilePartDescriptor>();

				public void Rehydrate(IDataRehydrator rehydrator) {

					this.Hash = rehydrator.ReadNonNullableArray();
					this.IsOptional = rehydrator.ReadBool();
					this.HashingType = (DigestChannelHashingTypes) rehydrator.ReadByte();

					int count = rehydrator.ReadInt();

					for(int i = 0; i < count; i++) {
						uint type = rehydrator.ReadUInt();
						DigestChannelIndexFilePartDescriptor channelIndexFilePartDescriptor = new DigestChannelIndexFilePartDescriptor();
						channelIndexFilePartDescriptor.Rehydrate(rehydrator);

						this.DigestChannelIndexFilePartDescriptors.Add(type, channelIndexFilePartDescriptor);
					}
				}

				public HashNodeList GetStructuresArray() {
					HashNodeList nodeList = new HashNodeList();

					nodeList.Add(this.Hash);
					nodeList.Add(this.IsOptional);
					nodeList.Add((byte) this.HashingType);

					foreach(var channel in this.DigestChannelIndexFilePartDescriptors.OrderBy(c => c.Key)) {
						nodeList.Add(channel.Key);
						nodeList.Add(channel.Value);
					}

					return nodeList;
				}

				public class DigestChannelIndexFilePartDescriptor : ITreeHashable, IBinaryRehydratable {

					public long FileSize { get; set; }

					public IByteArray Hash { get; set; }

					public void Rehydrate(IDataRehydrator rehydrator) {

						this.FileSize = rehydrator.ReadLong();
						this.Hash = rehydrator.ReadNonNullableArray();
					}

					public HashNodeList GetStructuresArray() {
						HashNodeList nodeList = new HashNodeList();

						nodeList.Add(this.FileSize);
						nodeList.Add(this.Hash);

						return nodeList;
					}
				}
			}
		}
	}
}