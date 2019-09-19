using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization {

	public interface IDehydratedBlockchainMessage : IDehydrateBlockchainEvent {
		IBlockchainMessage RehydratedMessage { get; set; }

		SafeArrayHandle Contents { get; }
		IBlockchainMessage RehydrateMessage(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}

	public class DehydratedBlockchainMessage : IDehydratedBlockchainMessage {

		public SafeArrayHandle Contents { get; } = SafeArrayHandle.Create();
		public IBlockchainMessage RehydratedMessage { get; set; }

		public SafeArrayHandle Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.WriteRawArray(this.Contents);
		}

		public void Rehydrate(SafeArrayHandle data) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data)) {

				this.Rehydrate(rehydrator);
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.Contents.Entry = rehydrator.ReadArrayToEnd();
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