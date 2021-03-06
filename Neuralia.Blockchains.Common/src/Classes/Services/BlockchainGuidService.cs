using System;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Cryptography.Hash;

namespace Neuralia.Blockchains.Common.Classes.Services {

	public interface IBlockchainGuidService : IGuidService {
		AccountId CreateTemporaryAccountId();
		AccountId CreateTemporaryAccountId(string accountCode);

		TransactionId CreateTransactionId(AccountId accountId, DateTime chainInception);
		TransactionId CreateTransactionId(long accountSequenceId, Enums.AccountTypes accountType, DateTime chainInception);
		TransactionId CreateTransactionId(AccountId accountId, long timestamp);
		TransactionId CreateTransactionId(long accountSequenceId, Enums.AccountTypes accountType, long timestamp);
	}

	public class BlockchainGuidService : GuidService, IBlockchainGuidService {

		public BlockchainGuidService(IBlockchainTimeService timeService) : base(timeService) {
		}

		public TransactionId CreateTransactionId(AccountId accountId, DateTime chainInception) {

			long timestamp = this.timeService.GetChainDateTimeOffset(chainInception);

			return this.CreateTransactionId(accountId, timestamp);
		}

		public TransactionId CreateTransactionId(long accountSequenceId, Enums.AccountTypes accountType, DateTime chainInception) {

			long timestamp = this.timeService.GetChainDateTimeOffset(chainInception);

			return this.CreateTransactionId(accountSequenceId, accountType, timestamp);
		}
		
		public TransactionId CreateTransactionId(long accountSequenceId, Enums.AccountTypes accountType, long timestamp) {
			return CreateTransactionId(new AccountId(accountSequenceId, accountType),timestamp);
		}
		
		public TransactionId CreateTransactionId(AccountId accountId, long timestamp) {
			return new TransactionId(accountId, timestamp, this.GetValidScope(timestamp));
		}


		public AccountId CreateTemporaryAccountId() {
			return this.CreateTemporaryAccountId(this.CreateAccountCode());
		}

		public AccountId CreateTemporaryAccountId(string accountCode) {

			// hash the temporary guid
			using xxHasher64 hasher = new xxHasher64();

			using var bytes = accountCode.GetBytes();
			return new AccountId(hasher.HashLong(bytes), Enums.AccountTypes.Presentation);
		}
	}
}