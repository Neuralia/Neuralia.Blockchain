using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {

	public class BlockchainDigestSimpleChannelSetDescriptor : IBinarySerializable {
		public Dictionary<DigestChannelType, BlockchainDigestSimpleChannelDescriptor> Channels { get; } = new Dictionary<DigestChannelType, BlockchainDigestSimpleChannelDescriptor>();

		public void Rehydrate(IDataRehydrator rehydrator) {

			int count = rehydrator.ReadInt();

			for(int i = 0; i < count; i++) {
				DigestChannelType type = rehydrator.ReadUShort();
				BlockchainDigestSimpleChannelDescriptor channel = new BlockchainDigestSimpleChannelDescriptor();
				channel.Rehydrate(rehydrator);

				this.Channels.Add(type, channel);
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Channels.Count);

			foreach(KeyValuePair<DigestChannelType, BlockchainDigestSimpleChannelDescriptor> channel in this.Channels) {
				dehydrator.Write(channel.Key.Value);
				channel.Value.Dehydrate(dehydrator);

			}
		}
	}

	public class BlockchainDigestSimpleChannelDescriptor : Versionable<DigestChannelType>, IBinarySerializable {
		public int GroupSize { get; set; }

		public long TotalEntries { get; set; }
		public long LastEntryId { get; set; }
		public ushort ChannelType { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.ChannelType = rehydrator.ReadUShort();
			this.GroupSize = rehydrator.ReadInt();
			this.TotalEntries = rehydrator.ReadLong();
			this.LastEntryId = rehydrator.ReadLong();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.ChannelType);
			dehydrator.Write(this.GroupSize);
			dehydrator.Write(this.TotalEntries);
			dehydrator.Write(this.LastEntryId);
		}

		public void SetVersion(ComponentVersion<DigestChannelType> version) {
			this.Version = version;
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (1, 0, 1);
		}
	}
}