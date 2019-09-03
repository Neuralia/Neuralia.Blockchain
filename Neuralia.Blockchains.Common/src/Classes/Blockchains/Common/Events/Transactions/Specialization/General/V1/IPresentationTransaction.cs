using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IPresentationTransaction : ITransaction, IPresentation, IRateLimitedTransaction {
		AccountId AssignedAccountId { get; set; }

		long? CorrelationId { get; set; }

		List<ITransactionAccountFeature> Features { get; }
	}
}