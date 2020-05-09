using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions {

	public interface ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class CreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER> : GenerateNewTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER>, ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly byte changingKeyOrdinal;
		protected readonly string keyChangeName;
		protected TransactionId transactionId;

		public CreateChangeKeyTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, byte expiration, string note, byte changingKeyOrdinal, CorrelationContext correlationContext) : base(centralCoordinator, expiration, note, correlationContext) {

			this.changingKeyOrdinal = changingKeyOrdinal;

			if(this.changingKeyOrdinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				this.keyChangeName = GlobalsService.TRANSACTION_KEY_NAME;
			} else if(this.changingKeyOrdinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				this.keyChangeName = GlobalsService.MESSAGE_KEY_NAME;
			} else if(this.changingKeyOrdinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
				this.keyChangeName = GlobalsService.CHANGE_KEY_NAME;
			} else if(this.changingKeyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				// this would be very rare, but the change key can sign a message to change itself
				this.keyChangeName = GlobalsService.SUPER_KEY_NAME;
			} else {
				throw new EventGenerationException("Invalid key ordinal");
			}
		}

		protected override Task CheckSyncStatus(LockContext lockContext) {

			// for this transaction type, we dont care about the sync status. we launch it when we have to.
			return Task.CompletedTask;
		}

		protected override ValidationResult ValidateContents(ITransactionEnvelope envelope) {
			ValidationResult result = base.ValidateContents(envelope);

			if(result.Invalid) {
				return result;
			}

			if(envelope.Contents.Uuid.Scope != 0) {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ONLY_ONE_TRANSACTION_PER_SCOPE);
			}

			return new ValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected override async Task PreProcess(LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);

			IWalletAccount account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, this.keyChangeName, lockContext).ConfigureAwait(false);

			// we can not update again, if it is still happening
			if(key.Status == Enums.KeyStatus.Changing) {
				throw new EventGenerationException("The key is already in the process of changing. we can not do it again.");
			}
		}

		protected override async Task PostProcess(LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);

			// first thing, lets mark the key as changing status

			// now we publish our keys
			IWalletAccount account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, this.keyChangeName, lockContext).ConfigureAwait(false);

			key.Status = Enums.KeyStatus.Changing;
			key.KeyChangeTimeout = this.GetTransactionExpiration();
			key.ChangeTransactionId = this.transactionId;

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lockContext).ConfigureAwait(false);

		}

		protected override async Task<ITransactionEnvelope> AssembleEvent(LockContext lockContext) {
			ITransactionEnvelope envelope = await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateKeyChangeTransaction(this.changingKeyOrdinal, this.keyChangeName, this.correlationContext, lockContext, this.expiration).ConfigureAwait(false);

			this.transactionId = envelope.Contents.Uuid;

			return envelope;
		}
	}

}