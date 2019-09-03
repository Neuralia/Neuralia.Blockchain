using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization {

	public interface IDehydratedBlockchainMessage : IDehydrateBlockchainEvent {
		IBlockchainMessage RehydratedMessage { get; set; }

		IByteArray Contents { get; set; }
		IBlockchainMessage RehydrateMessage(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}

	public class DehydratedBlockchainMessage : IDehydratedBlockchainMessage {

		public IByteArray Contents { get; set; }
		public IBlockchainMessage RehydratedMessage { get; set; }

		public IByteArray Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.WriteRawArray(this.Contents);
		}

		public void Rehydrate(IByteArray data) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.Contents = rehydrator.ReadArrayToEnd();
		}

		public IBlockchainMessage RehydrateMessage(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if(this.RehydratedMessage == null) {

				this.RehydratedMessage = rehydrationFactory.CreateMessage(this);
				this.RehydratedMessage.Rehydrate(this, rehydrationFactory);
			}

			return this.RehydratedMessage;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.RehydratedMessage.GetStructuresArray());

			return nodeList;
		}
	}
}