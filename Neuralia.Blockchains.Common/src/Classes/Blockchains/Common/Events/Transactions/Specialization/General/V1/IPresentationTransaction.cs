using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1 {
	public interface IPresentationTransaction : IIndexedTransaction, IPresentation {
		AccountId AssignedAccountId { get; set; }

		long? CorrelationId { get; set; }

		List<ITransactionAccountAttribute> Attributes { get; }
	}
}