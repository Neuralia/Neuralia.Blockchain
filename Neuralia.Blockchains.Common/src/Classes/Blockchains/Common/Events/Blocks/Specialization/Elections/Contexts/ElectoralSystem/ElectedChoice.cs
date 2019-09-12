using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem {
	public interface IElectedChoice {
		List<TransactionId> TransactionIds { get; set; }
		Enums.ElectedPeerShareTypes PeerShareType { get; set; }
		IByteArray ElectionHash { get; set; }
		AccountId DelegateAccountId { get; set; }
	}

	public abstract class ElectedChoice : IElectedChoice {
		public List<TransactionId> TransactionIds { get; set; }
		public Enums.ElectedPeerShareTypes PeerShareType { get; set; }
		public IByteArray ElectionHash { get; set; }
		public AccountId DelegateAccountId { get; set; }
	}
}