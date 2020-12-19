using Neuralia.Blockchains.Common.Classes.Blockchains.Common;
using Neuralia.Blockchains.Core.Serialization.OffsetCalculators;

namespace Neuralia.Blockchains.Common.Classes.Tools.Serialization {

	/// <summary>
	///     an account offset calculator that starts at position 0 at the first poblicly available account. excludes all
	///     moderator accounts
	/// </summary>
	public class AccountsOffsetsCalculator : SequentialLongOffsetCalculator {
		public AccountsOffsetsCalculator() : base(Constants.FIRST_PUBLIC_ACCOUNT_NUMBER) {
		}
	}
}