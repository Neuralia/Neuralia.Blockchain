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

			foreach(var channel in this.Channels) {
				dehydrator.Write(channel.Key.Value);
				channel.Value.Dehydrate(dehydrator);

			}
		}
	}

	public class BlockchainDigestSimpleChannelDescriptor : Versionable<DigestChannelType>, IBinarySerializable {
		public int GroupSize { get; set; }

		public long TotalEntries { get; set; }
		public long LastEntryId { get; set; }

		public new ComponentVersion<DigestChannelType> Version { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.GroupSize = rehydrator.ReadInt();
			this.TotalEntries = rehydrator.ReadLong();
			this.LastEntryId = rehydrator.ReadLong();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.GroupSize);
			dehydrator.Write(this.TotalEntries);
			dehydrator.Write(this.LastEntryId);
		}

		protected override ComponentVersion<DigestChannelType> SetIdentity() {
			return (1, 0, 1);
		}
	}
}