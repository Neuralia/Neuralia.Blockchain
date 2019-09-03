using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Contents {
	public interface IPresentationContents : IInternalTransactionContent {
		Guid? ReferenceCode { get; set; }
	}

	public class PresentationContents : InternalTransactionContent, IPresentationContents {

		/// <summary>
		///     A code that can be provided by a central authority if we need to limit presentation registration
		/// </summary>
		public Guid? ReferenceCode { get; set; }

		/// <summary>
		///     a presentation transaction is a very special case where the contents are not merkle
		///     hashed, since they are an
		///     external POW. so we do not hash the POW
		/// </summary>
		/// <returns></returns>
		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());
			nodeList.Add(this.ReferenceCode);

			return nodeList;
		}

		public override IDataDehydrator Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.ReferenceCode);

			return dehydrator;
		}

		public override void Rehydrate(IDataRehydrator rehydrator, IBlockchainEventsRehydrationFactory blockchainEventsRehydrationCreator) {
			this.ReferenceCode = rehydrator.ReadNullableGuid();

		}

		protected override ComponentVersion<TransactionContentType> SetIdentity() {
			throw new NotImplementedException();
		}
	}
}