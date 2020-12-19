using System.Threading;
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

	public interface ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewSignedTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public abstract class CreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : GenerateNewSignedTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>{

		protected readonly byte changingKeyOrdinal;
		protected readonly string keyChangeName;

		protected const string CHANGE_WALLET_TASK_NAME = "change";
		protected const string CONFIRM_CHANGE_WALLET_TASK_NAME = "confirm_change";

		
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

		#region workflow tasks
		
		#endregion
		protected override Task CheckSyncStatus(LockContext lockContext) {

			// for this transaction type, we dont care about the sync status. we launch it when we have to.
			return Task.CompletedTask;
		}

		protected override async Task PreValidateContents(LockContext lockContext) {
			await base.PreValidateContents(lockContext).ConfigureAwait(false);
			

			if(envelope.Contents.Uuid.Scope != 0) {
				new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ONLY_ONE_TRANSACTION_PER_SCOPE).GenerateException();
			}
		}

		protected override async Task PreProcess(LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);

			IWalletAccount account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountCode, this.keyChangeName, lockContext).ConfigureAwait(false);

			// we can not update again, if it is still happening
			if(key.Status == Enums.KeyStatus.Changing) {
				throw new EventGenerationException("The key is already in the process of changing. we can not do it again.");
			}
		}
		
		protected override void AddTaskDispatch() {
			
			this.AddWalletTransactionTask(CHANGE_WALLET_TASK_NAME,this.SetChangingProcess);

			base.AddTaskDispatch();
			
			this.AddWalletTransactionTask(CONFIRM_CHANGE_WALLET_TASK_NAME,this.ConfirmChange);
		}

		protected async Task SetChangingProcess(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {
	
		}

		protected async Task ConfirmChange(IWalletProvider walletProvider, CancellationToken token, LockContext lockContext) {
			await base.PreProcess(lockContext).ConfigureAwait(false);

			// first thing, lets mark the key as changing status

			// now we publish our keys
			IWalletAccount account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			using IWalletKey key = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountCode, this.keyChangeName, lockContext).ConfigureAwait(false);

			key.Status = Enums.KeyStatus.Changing;
			key.KeyChangeTimeout = this.GetTransactionExpiration();
			key.ChangeTransactionId = envelope.Contents.Uuid;

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lockContext).ConfigureAwait(false);
		}

		protected override async Task<ITransactionEnvelope> AssembleEvent(LockContext lockContext) {
			//TODO; this here must be refined! about the super key change
			bool changeSuperKey = false;
			return await this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateKeyChangeTransaction(this.changingKeyOrdinal, this.keyChangeName, changeSuperKey, this.correlationContext, lockContext).ConfigureAwait(false);
		}
	}

}