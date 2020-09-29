using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization {

	public interface IDehydratedBlockchainMessage : IDehydrateBlockchainEvent<IBlockchainMessage> {

		SafeArrayHandle Contents { get; }
	}

	public class DehydratedBlockchainMessage : IDehydratedBlockchainMessage {

		public SafeArrayHandle Contents { get; } = SafeArrayHandle.Create();
		public IBlockchainMessage RehydratedEvent { get; set; }

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.WriteRawArray(this.Contents);
		}

		public void Rehydrate(SafeArrayHandle data) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);

		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.Contents.Entry = rehydrator.ReadArrayToEnd();
		}

		public void Dehydrate(ChannelsEntries<IDataDehydrator> channelDehydrators) {
			throw new NotImplementedException();
		}

		public IBlockchainMessage Rehydrate(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if(this.RehydratedEvent == null) {

				this.RehydratedEvent = rehydrationFactory.CreateMessage(this);
				this.RehydratedEvent.Rehydrate(this, rehydrationFactory);
			}

			return this.RehydratedEvent;
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.RehydratedEvent.GetStructuresArray());

			return nodeList;
		}

		public void Clear() {
			
		}
	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if(this.IsDisposed) {
				return;
			}

			if(disposing) {
				this.Contents?.Dispose();
			}

			this.IsDisposed = true;
		}

		~DehydratedBlockchainMessage() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}