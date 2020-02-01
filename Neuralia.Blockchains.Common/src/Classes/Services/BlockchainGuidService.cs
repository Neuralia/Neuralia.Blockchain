using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Services {

	public interface IBlockchainGuidService : IGuidService {
		AccountId CreateTemporaryAccountId(Enums.AccountTypes accountType);
		AccountId CreateTemporaryAccountId(Guid guid, Enums.AccountTypes accountType);

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

		public TransactionId CreateTransactionId(AccountId accountId, long timestamp) {

			return new TransactionId(accountId, timestamp, this.GetValidScope(timestamp));
		}

		public TransactionId CreateTransactionId(long accountSequenceId, Enums.AccountTypes accountType, long timestamp) {

			return new TransactionId(accountSequenceId, accountType, timestamp, this.GetValidScope(timestamp));
		}
		
		public AccountId CreateTemporaryAccountId(Enums.AccountTypes accountType) {
			return this.CreateTemporaryAccountId(this.Create(), accountType);
		}

		public AccountId CreateTemporaryAccountId(Guid tempGuid, Enums.AccountTypes accountType) {

			// hash the temporary guid
			xxHasher64 hasher = new xxHasher64();

			return new AccountId(hasher.HashLong(tempGuid.ToByteArray()), accountType);

		}
	}
}