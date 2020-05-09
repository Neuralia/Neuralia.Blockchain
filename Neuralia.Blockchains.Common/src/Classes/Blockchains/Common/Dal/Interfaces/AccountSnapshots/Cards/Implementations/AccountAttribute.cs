using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {

	public class AccountAttribute : IAccountAttribute {

		public AccountAttributeType AttributeType { get; set; }
		public uint CorrelationId { get; set; }

		public byte[] Context { get; set; }
		public DateTime? Start { get; set; }
		public DateTime? Expiration { get; set; }
	}
}