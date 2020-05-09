using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common {
	public static class Constants {
		public const int DEFAULT_MODERATOR_ACCOUNT_ID = 1;

		/// <summary>
		///     the id of the first account that will be publicly assigned.
		/// </summary>
		public const int FIRST_PUBLIC_ACCOUNT_NUMBER = 34;

		public static AccountId FirstStandardAccountId => new AccountId(FIRST_PUBLIC_ACCOUNT_NUMBER, Enums.AccountTypes.Standard);
		public static AccountId FirstJointAccountId => new AccountId(FIRST_PUBLIC_ACCOUNT_NUMBER, Enums.AccountTypes.Standard);
	}
}