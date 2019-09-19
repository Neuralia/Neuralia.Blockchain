using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Services;

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
		protected TransactionIdExtended transactionId;
		public CreateChangeKeyTransactionWorkflow(CENTRAL_COORDINATOR centralCoordinator, string note, byte changingKeyOrdinal, CorrelationContext correlationContext) : base(centralCoordinator, note, correlationContext) {

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

		protected override void CheckSyncStatus() {

			// for this transaction type, we dont care about the sync status. we launch it when we have to.
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

		protected override void PreProcess() {
			base.PreProcess();

			// first thing, lets mark the key as changing status

			// now we publish our keys
			IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();

			using(IWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, this.keyChangeName)) {

				if(key.Status == Enums.KeyStatus.Changing) {
					throw new EventGenerationException("The key is already in the process of changing. we can not do it again.");
				}

				key.Status = Enums.KeyStatus.Changing;

				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);
			}
		}
		
		protected override void PostProcess() {
			base.PreProcess();

			// first thing, lets mark the key as changing status

			// now we publish our keys
			IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();

			using(IWalletKey key = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, this.keyChangeName)) {

				if(key.Status != Enums.KeyStatus.Changing) {
					throw new EventGenerationException("The key is already in the process of changing. we can not do it again.");
				}

				key.ChangeTransactionId = this.transactionId;

				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);
			}
		}

		protected override ITransactionEnvelope AssembleEvent() {
			var envelope = this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateKeyChangeTransaction(this.changingKeyOrdinal, this.keyChangeName, this.correlationContext);

			this.transactionId = envelope.Contents.Uuid;
			
			return envelope;
		}
	}

}