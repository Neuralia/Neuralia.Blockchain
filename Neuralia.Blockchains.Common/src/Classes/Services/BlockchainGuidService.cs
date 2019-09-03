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
		Guid GetTransactionGuid(TransactionId transactionId);
		TransactionId ParseTransactionGuid(Guid transactionGuid);
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

		/// <summary>
		///     Parse a transction Guid and return a transaction Scope object
		/// </summary>
		/// <param name="transactionGuid"></param>
		/// <returns></returns>
		public TransactionId ParseTransactionGuid(Guid transactionGuid) {
			Span<byte> guidSpan = stackalloc byte[16];

#if (NETSTANDARD2_0)
			Span<byte> tempspan = transactionGuid.ToByteArray();
			tempspan.CopyTo(guidSpan);
#elif (NETCOREAPP2_2)
			transactionGuid.TryWriteBytes(guidSpan);
#else
	throw new NotImplementedException();
#endif

			Span<byte> span = stackalloc byte[8];
			guidSpan.Slice(0, 8).CopyTo(span);
			TypeSerializer.Deserialize(span, out long accountSequenceId);

			Enums.AccountTypes accountType = (Enums.AccountTypes) guidSpan[8];

			span = stackalloc byte[8];
			guidSpan.Slice(9, 6).CopyTo(span);
			TypeSerializer.Deserialize(span, out long timestamp);

			byte scope = guidSpan[15];

			return new TransactionId(accountSequenceId, accountType, timestamp, scope);
		}

		/// <summary>
		///     Here we create a guid from our transaction information
		/// </summary>
		/// <param name="transactionId"></param>
		/// <returns></returns>
		public Guid GetTransactionGuid(TransactionId transactionId) {
			Span<byte> guidSpan = stackalloc byte[16];

			Span<byte> span = stackalloc byte[8];
			TypeSerializer.Serialize(transactionId.Account.ToLongRepresentation(), span);
			span.CopyTo(guidSpan.Slice(0, 8));

			guidSpan[8] = transactionId.Account.AccountTypeRaw;

			span = stackalloc byte[6];
			TypeSerializer.Serialize(transactionId.Timestamp.Value, span);
			span.CopyTo(guidSpan.Slice(9, 6));

			guidSpan[15] = transactionId.Scope;

#if (NETSTANDARD2_0)
			return new Guid(guidSpan.ToArray());
#elif (NETCOREAPP2_2)
			return new Guid(guidSpan);
#else
	throw new NotImplementedException();
#endif

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