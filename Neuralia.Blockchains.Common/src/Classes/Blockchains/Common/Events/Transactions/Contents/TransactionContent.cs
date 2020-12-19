using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Contents {
	public interface ITransactionContent : ITreeHashable {
	}

	public abstract class TransactionContent : ITransactionContent {

		protected TransactionContent() {
			this.Version = this.SetIdentity();

			if(this.Version.IsNull) {
				throw new ApplicationException("Version has not been set for this component");
			}
		}

		public ComponentVersion<TransactionContentType> Version { get; protected set; }

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Version);

			return nodeList;
		}

		protected abstract ComponentVersion<TransactionContentType> SetIdentity();

		public abstract IDataDehydrator Dehydrate(IDataDehydrator dehydrator);

		public abstract void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory blockchainEventsRehydrationFactory);
	}
}