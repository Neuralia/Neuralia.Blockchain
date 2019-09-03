using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization {
	public class BlockSerializationParameters : IBinarySerializable {
		public long accountIdsBaseline;
		public long timestampsBaseline;

		public void Rehydrate(IDataRehydrator rehydrator) {
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			this.accountIdsBaseline = adaptiveLong.Value;

			adaptiveLong.Rehydrate(rehydrator);
			this.timestampsBaseline = adaptiveLong.Value;
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = this.accountIdsBaseline;
			adaptiveLong.Dehydrate(dehydrator);

			adaptiveLong.Value = this.timestampsBaseline;
			adaptiveLong.Dehydrate(dehydrator);
		}
	}
}