using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Operations {
	public interface IOperation : ITreeHashable {
	}

	public abstract class Operation : IOperation {

		public virtual HashNodeList GetStructuresArray() {
			throw new NotImplementedException();
		}

		public abstract IDataDehydrator Dehydrate(IDataDehydrator dehydrator);

		public abstract void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory blockchainEventsRehydrationCreator);
	}
}