using System.Collections.Generic;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem {
	public interface IElectedChoice {
		List<TransactionId> TransactionIds { get; set; }
		Enums.MiningTiers ElectedTier { get; set; }
		SafeArrayHandle ElectionHash { get; }
		AccountId DelegateAccountId { get; set; }
	}

	public abstract class ElectedChoice : IElectedChoice {
		public List<TransactionId> TransactionIds { get; set; }
		public Enums.MiningTiers ElectedTier { get; set; }
		public SafeArrayHandle ElectionHash { get; } = SafeArrayHandle.Create();
		public AccountId DelegateAccountId { get; set; }
	}
}