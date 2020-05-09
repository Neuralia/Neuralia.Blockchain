using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoreLinq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Storage.Bases;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.ChainState;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IInterpretationProvider : IChainProvider {
	}

	public interface IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IInterpretationProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		Task InterpretNewBlockSnapshots(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		Task InterpretNewBlockLocalWallet(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, TaskRoutingContext taskRoutingContext, LockContext lockContext);

		Task InterpretGenesisBlockSnapshots(IGenesisBlock genesisBlock, LockContext lockContext);
		Task InterpretGenesisBlockLocalWallet(SynthesizedBlock synthesizedBlockk, TaskRoutingContext taskRoutingContext, LockContext lockContext);

		Task<SynthesizedBlock> SynthesizeBlock(IBlock block, LockContext lockContext);
		void ProcessBlockImmediateGeneralImpact(BlockId blockId, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		void ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		void ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		Task ProcessBlockImmediateAccountsImpact(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, LockContext lockContext);

		SynthesizedBlock CreateSynthesizedBlock();
	}

	public interface IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_CONTEXT, BLOCK, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> : IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : class, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : class, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : class, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : class, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_CONTEXT : class, ITrackedAccountsContext
		where BLOCK : IBlock
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new() {
	}

	/// <summary>
	///     A special service to handle validate and accepted transactions into our chain. Here, we process the contents of the
	///     chain
	/// </summary>
	/// <typeparam name="TRANSACTION_BLOCK_FACTORY"></typeparam>
	/// <typeparam name="BLOCKASSEMBLYPROVIDER"></typeparam>
	/// <typeparam name="IWalletManager
	/// 
	/// 
	/// 
	/// 
	/// 
	/// <CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
	///     "></typeparam>
	///     <typeparam name="WALLET_PROVIDER"></typeparam>
	///     <typeparam name="USERWALLET"></typeparam>
	///     <typeparam name="WALLET_IDENTITY"></typeparam>
	///     <typeparam name="WALLET_KEY"></typeparam>
	///     <typeparam name="WALLET_KEY_HISTORY"></typeparam>
	public abstract class InterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_CONTEXT, BLOCK, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT> : ChainProvider, IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_CONTEXT, BLOCK, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where STANDARD_ACCOUNT_SNAPSHOT_CONTEXT : IStandardAccountSnapshotContext<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>
		where JOINT_ACCOUNT_SNAPSHOT_CONTEXT : class, IJointAccountSnapshotContext<JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT : class, IAccreditationCertificatesSnapshotContext<ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>
		where STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT : class, IAccountKeysSnapshotContext<STANDARD_ACCOUNT_KEY_SNAPSHOT>
		where CHAIN_OPTIONS_SNAPSHOT_CONTEXT : class, IChainOptionsSnapshotContext<CHAIN_OPTIONS_SNAPSHOT>
		where TRACKED_ACCOUNTS_CONTEXT : class, ITrackedAccountsContext
		where BLOCK : IBlock
		where ACCOUNT_SNAPSHOT : IAccountSnapshot
		where STANDARD_ACCOUNT_SNAPSHOT : class, IStandardAccountSnapshotEntry<STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_SNAPSHOT : class, IJointAccountSnapshotEntry<JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttributeEntry, new()
		where JOINT_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccountEntry, new()
		where STANDARD_ACCOUNT_KEY_SNAPSHOT : class, IStandardAccountKeysSnapshotEntry, new()
		where ACCREDITATION_CERTIFICATE_SNAPSHOT : class, IAccreditationCertificateSnapshotEntry<ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT>, new()
		where ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT : class, IAccreditationCertificateSnapshotAccountEntry, new()
		where CHAIN_OPTIONS_SNAPSHOT : class, IChainOptionsSnapshotEntry, new()
		where STANDARD_WALLET_ACCOUNT_SNAPSHOT : class, IWalletStandardAccountSnapshot<STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_WALLET_ACCOUNT_SNAPSHOT : class, IWalletJointAccountSnapshot<JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT>, ACCOUNT_SNAPSHOT, new()
		where JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT : class, IAccountAttribute, new()
		where JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT : class, IJointMemberAccount, new() {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly IGuidService guidService;

		private readonly ITimeService timeService;

		public InterpretationProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
			this.centralCoordinator = centralCoordinator;
		}

		protected CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		private IAccountSnapshotsProvider AccountSnapshotsProvider => this.CentralCoordinator.ChainComponentProvider.AccountSnapshotsProviderBase;

		protected abstract ICardUtils CardUtils { get; }

		public virtual Task InterpretGenesisBlockSnapshots(IGenesisBlock genesisBlock, LockContext lockContext) {

			// first thing, lets add the moderator keys to our chainState. these are pretty important

			return this.InterpretBlockSnapshots((BLOCK) genesisBlock, null, lockContext);
		}

		public virtual Task InterpretGenesisBlockLocalWallet(SynthesizedBlock synthesizedBlock, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			// first thing, lets add the moderator keys to our chainState. these are pretty important

			return this.InterpretBlockLocalWallet(synthesizedBlock, synthesizedBlock.BlockId - 1, taskRoutingContext, lockContext);
		}

		public async Task InterpretNewBlockSnapshots(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			if((block.BlockId.Value == 1) && block is IGenesisBlock genesisBlock) {
				await this.InterpretGenesisBlockSnapshots(genesisBlock, lockContext).ConfigureAwait(false);
			} else {
				await this.InterpretBlockSnapshots((BLOCK) block, serializationTransactionProcessor, lockContext).ConfigureAwait(false);
			}
		}

		public async Task InterpretNewBlockLocalWallet(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			if(synthesizedBlock.BlockId == 1) {
				await this.InterpretGenesisBlockLocalWallet(synthesizedBlock, taskRoutingContext, lockContext).ConfigureAwait(false);
			} else {
				await this.InterpretBlockLocalWallet(synthesizedBlock, lastSyncedBlockId, taskRoutingContext, lockContext).ConfigureAwait(false);
			}
		}

		/// <summary>
		///     Here we take a block a synthesize the transactions that concern our local accounts
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		public async Task<SynthesizedBlock> SynthesizeBlock(IBlock block, LockContext lockContext) {

			AccountCache accountCache = await this.GetAccountCache(lockContext).ConfigureAwait(false);

			// get the transactions that concern us
			Dictionary<TransactionId, ITransaction> blockConfirmedTransactions = block.GetAllConfirmedTransactions();

			return await this.SynthesizeBlock(block, accountCache, blockConfirmedTransactions, lockContext).ConfigureAwait(false);
		}

		public void ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			this.ProcessBlockImmediateGeneralImpact(block.BlockId, block.GetAllConfirmedTransactions().Values.ToList(), serializationTransactionProcessor, lockContext);
		}

		public void ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			this.ProcessBlockImmediateGeneralImpact(new BlockId(synthesizedBlock.BlockId), synthesizedBlock.ConfirmedGeneralTransactions.Values.ToList(), serializationTransactionProcessor, lockContext);
		}

		/// <summary>
		///     determine any impact the block has on our general caches and files but NOT our personal wallet
		/// </summary>
		/// <param name="block"></param>
		public void ProcessBlockImmediateGeneralImpact(BlockId blockId, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// refresh our accounts list
			if(chainStateProvider.DiskBlockHeight < blockId.Value) {
				throw new ApplicationException($"Invalid disk block height value. Should be at least {blockId}.");
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone)) {
				// ok, the interpretation has been fully performed, we don't need to repeat it
				return;
			}

			List<IMasterTransaction> confirmedMasterTransactions = transactions.OfType<IMasterTransaction>().ToList();

			List<TransactionId> keyedTransactionIds = confirmedMasterTransactions.Select(t => t.TransactionId).ToList();
			List<ITransaction> confirmedTransactions = transactions.Where(t => !keyedTransactionIds.Contains(t.TransactionId)).ToList();

			// first thing, lets process any transaction that might affect our wallet directly
			foreach(IMasterTransaction trx in confirmedMasterTransactions) {
				this.HandleConfirmedMasterGeneralTransaction(trx, lockContext);
			}

			foreach(ITransaction trx in confirmedTransactions) {
				this.HandleConfirmedGeneralTransaction(trx, lockContext);
			}

			this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus |= ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone;

		}

		/// <summary>
		///     determine any impact the block has on our general wallet
		/// </summary>
		/// <param name="block"></param>
		public async Task ProcessBlockImmediateAccountsImpact(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, LockContext lockContext) {

			Dictionary<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock)> walletActionSets = new Dictionary<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock)>();
			List<Func<LockContext, Task>> serializationActions = new List<Func<LockContext, Task>>();

			AccountCache accountCache = null;

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(async (provider, lc) => {
				accountCache = await this.GetIncompleteAccountCache(provider, lastSyncedBlockId + 1, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed, lc).ConfigureAwait(false);

				if((accountCache == null) || !accountCache.combinedAccounts.Any()) {
					// ok, the interpretation has been fully performed, we don't need to repeat it
					return;
				}

				foreach(AccountId account in synthesizedBlock.Accounts) {

					List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions = new List<Func<LockContext, Task<List<Func<LockContext, Task>>>>>();
					SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock = synthesizedBlock.AccountScopped[account];

					List<IMasterTransaction> confirmedMasterTransactions = scoppedSynthesizedBlock.ConfirmedLocalTransactions.Select(t => t.Value).OfType<IMasterTransaction>().ToList();
					confirmedMasterTransactions.AddRange(scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Select(t => t.Value).OfType<IMasterTransaction>());

					List<TransactionId> keyedTransactionIds = confirmedMasterTransactions.Select(t => t.TransactionId).ToList();
					List<ITransaction> confirmedTransactions = scoppedSynthesizedBlock.ConfirmedLocalTransactions.Values.Where(t => !keyedTransactionIds.Contains(t.TransactionId)).ToList();
					confirmedTransactions.AddRange(scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Values.Where(t => !keyedTransactionIds.Contains(t.TransactionId)));

					// first, we check any election results
					foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in synthesizedBlock.FinalElectionResults) {

						if(finalElectionResult.ElectedAccounts.ContainsKey(account)) {
							walletActions.Add(async lc2 => {
								AccountId currentAccount = account;
								SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult = finalElectionResult;
								await provider.InsertElectionsHistoryEntry(synthesizedElectionResult, synthesizedBlock, currentAccount, lc2).ConfigureAwait(false);

								return null;
							});
						}
					}

					// next thing, lets process any transaction that might affect our wallet directly
					int index = 0;

					foreach(IMasterTransaction trx in confirmedMasterTransactions) {
						this.HandleConfirmedMasterTransaction(synthesizedBlock.BlockId, trx, index, accountCache, walletActions, serializationActions, lc);
						index++;
					}

					foreach(ITransaction trx in confirmedTransactions) {
						await this.HandleConfirmedTransaction(synthesizedBlock.BlockId, trx, accountCache, walletActions, lc).ConfigureAwait(false);
					}

					foreach(RejectedTransaction trx in scoppedSynthesizedBlock.RejectedTransactions) {
						this.HandleRejectedTransaction(synthesizedBlock.BlockId, trx, accountCache, walletActions, lc);
					}

					if(walletActions.Any()) {
						walletActionSets.Add(account, (walletActions, scoppedSynthesizedBlock));
					}
				}
			}, lockContext).ConfigureAwait(false);

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

				await this.SetNewAccountsFlag(provider, lastSyncedBlockId + 1, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed, lc).ConfigureAwait(false);

				List<Func<LockContext, Task>> transactionalSuccessActions = new List<Func<LockContext, Task>>();

				foreach(KeyValuePair<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock)> accountEntry in walletActionSets) {

					await IndependentActionRunner.RunAsync(lc, async lc2 => {
						List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions = accountEntry.Value.walletActions;

						if(walletActions.Any()) {

							// run any wallet tasks we may have
							foreach(Func<LockContext, Task<List<Func<LockContext, Task>>>> action in walletActions.Where(a => a != null)) {

								List<Func<LockContext, Task>> result = await action(lc).ConfigureAwait(false);

								if(result != null) {
									transactionalSuccessActions.AddRange(result);
								}
							}
						}
					}, async lc2 => {

						// if there are any impacting transactions, let's add them now
						if(accountEntry.Value.scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Any()) {

							ImmutableList<KeyValuePair<TransactionId, ITransaction>> impactedTransactions = accountEntry.Value.scoppedSynthesizedBlock.ConfirmedExternalsTransactions.ToImmutableList();

							foreach(KeyValuePair<TransactionId, ITransaction> entry in impactedTransactions) {
								await provider.InsertTransactionHistoryEntry(entry.Value, null, synthesizedBlock.BlockId, lc).ConfigureAwait(false);
							}
						}

					}).ConfigureAwait(false);

				}

				// now mark the others that had no transactions

				foreach(KeyValuePair<AccountId, IWalletAccount> account in accountCache.combinedAccounts) {

					IWalletAccount walletAccount = await provider.GetWalletAccount(account.Key, lc).ConfigureAwait(false);
					IAccountFileInfo accountEntry = await provider.GetAccountFileInfo(walletAccount.AccountUuid, lc).ConfigureAwait(false);

					WalletAccountChainState chainState = await accountEntry.WalletChainStatesInfo.ChainState(lc).ConfigureAwait(false);

					if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed)) {
						chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed;
					}
				}

				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AddTransactionSuccessActions(transactionalSuccessActions, lc).ConfigureAwait(false);
			}, lockContext).ConfigureAwait(false);

			;

			await Repeater.RepeatAsync(async () => {
				if(serializationActions.Any()) {

					try {
						//TODO: should we use serialization transactions here too?
						await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, null).ConfigureAwait(false);

					} catch(Exception ex) {
						//TODO: what do we do here?
						NLog.Default.Error(ex, "Failed to serialize");

						throw;
					}
				}
			}).ConfigureAwait(false);

			;
		}

		public abstract SynthesizedBlock CreateSynthesizedBlock();

		protected virtual async Task<ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>> CreateLocalTransactionInterpretationProcessor(List<IWalletAccount> accountsList, LockContext lockContext) {
			// create two interpreters. one for our own transactions and one for the general snapshots
			ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> transactionInterpretationProcessor = this.CreateWalletInterpretationProcessor();

			transactionInterpretationProcessor.RequestStandardAccountSnapshots += async (accountId, lc) => {

				List<AccountId> standardAccounts = accountId.Where(a => a.AccountType == Enums.AccountTypes.Standard).ToList();
				List<IWalletAccount> selectedAccounts = accountsList.Where(a => standardAccounts.Contains(a.GetAccountId())).ToList();

				Dictionary<AccountId, STANDARD_WALLET_ACCOUNT_SNAPSHOT> accountSnapshots = new Dictionary<AccountId, STANDARD_WALLET_ACCOUNT_SNAPSHOT>();

				foreach(IWalletAccount account in selectedAccounts) {

					if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountUuid, lc).ConfigureAwait(false) is STANDARD_WALLET_ACCOUNT_SNAPSHOT entry) {
						accountSnapshots.Add(account.GetAccountId(), entry);
					}
				}

				return accountSnapshots;
			};

			transactionInterpretationProcessor.RequestJointAccountSnapshots += async (accountId, lc) => {

				List<AccountId> jointAccounts = accountId.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList();
				List<IWalletAccount> selectedAccounts = accountsList.Where(a => jointAccounts.Contains(a.GetAccountId())).ToList();

				Dictionary<AccountId, JOINT_WALLET_ACCOUNT_SNAPSHOT> accountSnapshots = new Dictionary<AccountId, JOINT_WALLET_ACCOUNT_SNAPSHOT>();

				foreach(IWalletAccount account in selectedAccounts) {
					if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountUuid, lc).ConfigureAwait(false) is JOINT_WALLET_ACCOUNT_SNAPSHOT entry) {
						accountSnapshots.Add(account.GetAccountId(), entry);
					}
				}

				return accountSnapshots;
			};

			transactionInterpretationProcessor.RequestCreateNewStandardAccountSnapshot += async lc => (STANDARD_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry(lc).ConfigureAwait(false);
			transactionInterpretationProcessor.RequestCreateNewJointAccountSnapshot += async lc => (JOINT_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshotEntry(lc).ConfigureAwait(false);

			transactionInterpretationProcessor.EnableLocalMode(true);

			transactionInterpretationProcessor.AccountInfluencingTransactionFound += async (isOwn, impactedLocalPublishedAccounts, impactedLocalDispatchedAccounts, transaction, blockId, lc) => {
				// alert when a transaction concerns us

				List<Guid> impactedLocalPublishedAccountsUuids = accountsList.Where(a => impactedLocalPublishedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountUuid).ToList();
				List<Guid> impactedLocalDispatchedAccountsUuids = accountsList.Where(a => impactedLocalDispatchedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountUuid).ToList();

				if(!isOwn) {
					// this is a foreign transaction that is targetting us. let's add it to our wallet
					await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.InsertTransactionHistoryEntry(transaction, "", blockId, lc).ConfigureAwait(false);
				}

				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionReceived(impactedLocalPublishedAccounts, impactedLocalPublishedAccountsUuids, impactedLocalDispatchedAccounts, impactedLocalDispatchedAccountsUuids, transaction.TransactionId));
			};

			// we dont store keys in our wallet snapshots as we already have them in the wallet itself.
			transactionInterpretationProcessor.IsAnyAccountKeysTracked = async (ids, accounts) => false;

			return transactionInterpretationProcessor;
		}

		protected virtual ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateSnapshotsTransactionInterpretationProcessor() {
			// create two interpreters. one for our own transactions and one for the general snapshots
			ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> transactionInterpretationProcessor = this.CreateInterpretationProcessor();

			transactionInterpretationProcessor.IsAnyAccountTracked = accountIds => this.AccountSnapshotsProvider.AnyAccountTracked(accountIds);
			transactionInterpretationProcessor.GetTrackedAccounts = accountIds => this.AccountSnapshotsProvider.AccountsTracked(accountIds);

			// if we use fast keys, we track all keys. otherwise, whatever account we track
			transactionInterpretationProcessor.IsAnyAccountKeysTracked = (ids, accounts) => {

				BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				return this.AccountSnapshotsProvider.AnyAccountTracked(accounts);
			};

			// we track them all
			transactionInterpretationProcessor.IsAnyAccreditationCertificateTracked = async ids => true;
			transactionInterpretationProcessor.IsAnyChainOptionTracked = async ids => true;

			transactionInterpretationProcessor.RequestStandardAccountSnapshots += async (accountIds, lc) => {

				List<AccountId> standardAccounts = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).ToList();
				List<AccountId> trackedAccounts = new List<AccountId>();

				foreach(AccountId accountId in standardAccounts) {
					if(await this.AccountSnapshotsProvider.IsAccountTracked(accountId).ConfigureAwait(false)) {
						trackedAccounts.Add(accountId);
					}
				}

				return (await this.AccountSnapshotsProvider.LoadAccountSnapshots(trackedAccounts).ConfigureAwait(false)).ToDictionary(a => a.AccountId.ToAccountId(), a => (STANDARD_ACCOUNT_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestJointAccountSnapshots += async (accountIds, lc) => {

				List<AccountId> jointAccounts = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList();
				List<AccountId> trackedAccounts = new List<AccountId>();

				foreach(AccountId accountId in jointAccounts) {
					if(await this.AccountSnapshotsProvider.IsAccountTracked(accountId).ConfigureAwait(false)) {
						trackedAccounts.Add(accountId);
					}
				}

				return (await this.AccountSnapshotsProvider.LoadAccountSnapshots(trackedAccounts).ConfigureAwait(false)).ToDictionary(a => a.AccountId.ToAccountId(), a => (JOINT_ACCOUNT_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestStandardAccountKeySnapshots += async (keys, lc) => {
				List<(long accountId, byte ordinal)> trackedKeys = new List<(long accountId, byte ordinal)>();

				BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				foreach((long AccountId, byte OrdinalId) key in keys) {
					if(configuration.EnableFastKeyIndex || await this.AccountSnapshotsProvider.IsAccountTracked(new AccountId(key.AccountId, Enums.AccountTypes.Standard)).ConfigureAwait(false)) {
						trackedKeys.Add(key);
					}
				}

				return (await this.AccountSnapshotsProvider.LoadStandardAccountKeysSnapshots(trackedKeys).ConfigureAwait(false)).ToDictionary(a => (a.AccountId, a.OrdinalId), a => (STANDARD_ACCOUNT_KEY_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestAccreditationCertificateSnapshots += async (certificateIds, lc) => {

				return (await this.AccountSnapshotsProvider.LoadAccreditationCertificatesSnapshots(certificateIds).ConfigureAwait(false)).ToDictionary(a => a.CertificateId, a => (ACCREDITATION_CERTIFICATE_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestChainOptionSnapshots += async (ids, lc) => {
				IChainOptionsSnapshot loadedChainOPtionSnapshots = await this.AccountSnapshotsProvider.LoadChainOptionsSnapshot().ConfigureAwait(false);

				Dictionary<int, CHAIN_OPTIONS_SNAPSHOT> results = new Dictionary<int, CHAIN_OPTIONS_SNAPSHOT>();

				if(loadedChainOPtionSnapshots != null) {
					results.Add(loadedChainOPtionSnapshots.Id, loadedChainOPtionSnapshots as CHAIN_OPTIONS_SNAPSHOT);
				}

				return results;
			};

			transactionInterpretationProcessor.RequestCreateNewStandardAccountSnapshot += lc => {

				return Task.FromResult((STANDARD_ACCOUNT_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewStandardAccountSnapshots());
			};

			transactionInterpretationProcessor.RequestCreateNewJointAccountSnapshot += lc => {

				return Task.FromResult((JOINT_ACCOUNT_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewJointAccountSnapshots());
			};

			transactionInterpretationProcessor.RequestCreateNewAccountKeySnapshot += lc => {
				return Task.FromResult((STANDARD_ACCOUNT_KEY_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewAccountKeySnapshots());

			};

			transactionInterpretationProcessor.RequestCreateNewAccreditationCertificateSnapshot += lc => {

				return Task.FromResult((ACCREDITATION_CERTIFICATE_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewAccreditationCertificateSnapshots());
			};

			transactionInterpretationProcessor.RequestCreateNewChainOptionSnapshot += lc => {

				return Task.FromResult((CHAIN_OPTIONS_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewChainOptionsSnapshots());
			};

			return transactionInterpretationProcessor;
		}

		protected void InsertChangedLocalAccountsEvent(Dictionary<AccountId, List<Func<Task>>> changedLocalAccounts, AccountId accountId, Func<Task> operation) {
			if(!changedLocalAccounts.ContainsKey(accountId)) {
				changedLocalAccounts.Add(accountId, new List<Func<Task>>());
			}

			changedLocalAccounts[accountId].Add(operation);
		}

		protected IWalletAccount IsLocalAccount(ImmutableDictionary<AccountId, IWalletAccount> account, AccountId accountId) {
			return account.ContainsKey(accountId) ? account[accountId] : null;
		}

		protected virtual async Task<SynthesizedBlock> SynthesizeBlock(IBlock block, AccountCache accountCache, Dictionary<TransactionId, ITransaction> blockConfirmedTransactions, LockContext lockContext) {
			SynthesizedBlock synthesizedBlock = this.CreateSynthesizedBlock();

			synthesizedBlock.BlockId = block.BlockId.Value;

			ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> localTransactionInterpretationProcessor = await this.CreateLocalTransactionInterpretationProcessor(accountCache.combinedAccounts.Values.ToList(), lockContext).ConfigureAwait(false);

			localTransactionInterpretationProcessor.IsAnyAccountTracked = async accountIds => accountCache.combinedAccountsList.Any(accountIds.Contains);
			localTransactionInterpretationProcessor.GetTrackedAccounts = async accountIds => accountCache.combinedAccountsList.Where(accountIds.Contains).ToList();
			localTransactionInterpretationProcessor.SetLocalAccounts(accountCache.publishedAccountsList, accountCache.dispatchedAccountsList);

			List<ITransaction> confirmedLocalTransactions = null;
			List<(ITransaction transaction, AccountId targetAccount)> confirmedExternalsTransactions = null;
			Dictionary<AccountId, List<TransactionId>> accountsTransactions = null;

			(confirmedLocalTransactions, confirmedExternalsTransactions, accountsTransactions) = await localTransactionInterpretationProcessor.GetImpactingTransactionsList(blockConfirmedTransactions.Values.ToList(), lockContext).ConfigureAwait(false);

			foreach(AccountId account in accountCache.combinedAccountsList) {

				SynthesizedBlock.SynthesizedBlockAccountSet synthesizedBlockAccountSet = new SynthesizedBlock.SynthesizedBlockAccountSet();

				synthesizedBlockAccountSet.ConfirmedLocalTransactions = confirmedLocalTransactions.Where(t => t.TransactionId.Account == account).ToDictionary(e => e.TransactionId, e => e);
				synthesizedBlockAccountSet.ConfirmedExternalsTransactions = confirmedExternalsTransactions.Where(t => t.targetAccount == account).ToDictionary(e => e.transaction.TransactionId, e => e.transaction);

				foreach(KeyValuePair<TransactionId, ITransaction> transaction in synthesizedBlockAccountSet.ConfirmedLocalTransactions) {
					if(!synthesizedBlockAccountSet.ConfirmedTransactions.ContainsKey(transaction.Key)) {
						synthesizedBlockAccountSet.ConfirmedTransactions.Add(transaction.Key, transaction.Value);
					}
				}

				foreach(KeyValuePair<TransactionId, ITransaction> transaction in synthesizedBlockAccountSet.ConfirmedExternalsTransactions) {
					if(!synthesizedBlockAccountSet.ConfirmedTransactions.ContainsKey(transaction.Key)) {
						synthesizedBlockAccountSet.ConfirmedTransactions.Add(transaction.Key, transaction.Value);
					}
				}

				synthesizedBlockAccountSet.RejectedTransactions = block.RejectedTransactions.Where(t => t.TransactionId.Account == account).ToList();

				synthesizedBlock.AccountScopped.Add(account, synthesizedBlockAccountSet);
			}

			synthesizedBlock.ConfirmedTransactions = synthesizedBlock.AccountScopped.SelectMany(a => a.Value.ConfirmedTransactions).Distinct().ToDictionary();
			synthesizedBlock.RejectedTransactions = synthesizedBlock.AccountScopped.SelectMany(a => a.Value.RejectedTransactions).Distinct().ToList();

			synthesizedBlock.Accounts = accountCache.combinedAccountsList.Distinct().ToList();

			List<TransactionId> allTransactions = synthesizedBlock.ConfirmedTransactions.Keys.ToList();
			allTransactions.AddRange(synthesizedBlock.RejectedTransactions.Select(t => t.TransactionId));

			// let's add the election results that may concern us
			foreach(IFinalElectionResults result in block.FinalElectionResults.Where(r => r.ElectedCandidates.Any(c => accountCache.combinedAccounts.ContainsKey(c.Key)) || r.DelegateAccounts.Any(c => accountCache.combinedAccounts.ContainsKey(c.Key)))) {

				synthesizedBlock.FinalElectionResults.Add(this.SynthesizeElectionResult(synthesizedBlock, result, block, accountCache, blockConfirmedTransactions));
			}

			return synthesizedBlock;
		}

		protected virtual SynthesizedBlock.SynthesizedElectionResult SynthesizeElectionResult(SynthesizedBlock synthesizedBlock, IFinalElectionResults result, IBlock block, AccountCache accountCache, Dictionary<TransactionId, ITransaction> blockConfirmedTransactions) {

			SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult = synthesizedBlock.CreateSynthesizedElectionResult();

			synthesizedElectionResult.BlockId = block.BlockId.Value - result.BlockOffset;
			synthesizedElectionResult.Timestamp = block.FullTimestamp;

			synthesizedElectionResult.DelegateAccounts = result.DelegateAccounts.Where(r => accountCache.combinedAccounts.ContainsKey(r.Key)).Select(a => a.Key).ToList();
			synthesizedElectionResult.ElectedAccounts = result.ElectedCandidates.Where(c => accountCache.combinedAccounts.ContainsKey(c.Key)).ToDictionary(e => e.Key, e => (e.Key, e.Value.DelegateAccountId, electedTier: e.Value.ElectedTier, string.Join(",", e.Value.Transactions.Select(t => t.ToString()))));

			return synthesizedElectionResult;
		}

		protected Task<List<IWalletAccount>> GetIncompleteAccountList(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {
			return walletProvider.GetWalletSyncableAccounts(blockSyncHeight, lockContext);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {

			List<IWalletAccount> accountsList = await this.GetIncompleteAccountList(walletProvider, blockSyncHeight, flagFilter, lockContext).ConfigureAwait(false);

			return await this.GetIncompleteAccountCache(walletProvider, blockSyncHeight, accountsList, flagFilter, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter, IWalletAccount filterAccount, LockContext lockContext) {

			List<IWalletAccount> accounts = await this.GetIncompleteAccountList(walletProvider, blockSyncHeight, flagFilter, lockContext).ConfigureAwait(false);
			List<IWalletAccount> accountsList = accounts.Where(a => a.AccountUuid == filterAccount.AccountUuid).ToList();

			return await this.GetIncompleteAccountCache(walletProvider, blockSyncHeight, accountsList, flagFilter, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, List<IWalletAccount> accountsList, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {

			List<IWalletAccount> tempList = new List<IWalletAccount>();

			foreach(IWalletAccount a in accountsList) {
				IAccountFileInfo accountFileInfo = await walletProvider.GetAccountFileInfo(a.AccountUuid, lockContext).ConfigureAwait(false);
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

				if(chainState.LastBlockSynced == (blockSyncHeight - 1)) {
					WalletAccountChainState.BlockSyncStatuses blockSyncStatus = (WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus;

					if(blockSyncStatus.HasFlag(WalletAccountChainState.BlockSyncStatuses.FullySynced)) {
						tempList.Add(a);
					}
				}
				else if(chainState.LastBlockSynced == blockSyncHeight) {
					WalletAccountChainState.BlockSyncStatuses blockSyncStatus = (WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus;

					if(!blockSyncStatus.HasFlag(flagFilter)) {
						tempList.Add(a);
					}
				}
			}

			return this.PrepareAccountCache(tempList, lockContext);
		}

		/// <summary>
		///     this method will return our local accounts in different forms and combinations
		/// </summary>
		/// <returns></returns>
		protected async Task<AccountCache> GetAccountCache(LockContext lockContext, long? blockSyncHeight = null) {

			List<IWalletAccount> accountsList = blockSyncHeight.HasValue ? await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(blockSyncHeight.Value, lockContext).ConfigureAwait(false) : await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts(lockContext).ConfigureAwait(false);

			return this.PrepareAccountCache(accountsList, lockContext);
		}

		/// <summary>
		///     Set a status flag to all New accounts
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="statusFlag"></param>
		protected async Task<bool> SetNewAccountsFlag(IWalletProvider provider, long blockId, WalletAccountChainState.BlockSyncStatuses statusFlag, LockContext lockContext) {
			bool changed = false;

			List<IWalletAccount> syncableAccounts = await provider.GetWalletSyncableAccounts(blockId, lockContext).ConfigureAwait(false);

			foreach(IWalletAccount account in syncableAccounts.Where(a => a.Status == Enums.PublicationStatus.New || a.ConfirmationBlockId == 0)) {

				IAccountFileInfo accountFileInfo = await provider.GetAccountFileInfo(account.AccountUuid, lockContext).ConfigureAwait(false);
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

				if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(statusFlag)) {

					chainState.BlockSyncStatus |= (int) statusFlag;
					changed = true;
				}
			}

			return changed;
		}

		protected AccountCache PrepareAccountCache(List<IWalletAccount> accountsList, LockContext lockContext) {
			AccountCache accountCache = new AccountCache();
			accountCache.publishedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Published).ToImmutableDictionary(a => a.PublicAccountId, a => a);
			accountCache.dispatchedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Dispatched).ToImmutableDictionary(a => a.AccountUuidHash, a => a);

			Dictionary<AccountId, IWalletAccount> combinedTemp = accountCache.publishedAccounts.ToDictionary();

			foreach(KeyValuePair<AccountId, IWalletAccount> e in accountCache.dispatchedAccounts) {
				combinedTemp.Add(e.Key, e.Value);
			}

			accountCache.combinedAccounts = combinedTemp.ToImmutableDictionary(e => e.Key, e => e.Value);

			accountCache.publishedAccountsList = accountCache.publishedAccounts.Keys.ToImmutableList();
			accountCache.dispatchedAccountsList = accountCache.dispatchedAccounts.Keys.ToImmutableList();

			List<AccountId> combinedAccountsList = accountCache.publishedAccountsList.ToList();
			combinedAccountsList.AddRange(accountCache.dispatchedAccountsList);
			accountCache.combinedAccountsList = combinedAccountsList.ToImmutableList();

			return accountCache;
		}

		/// <summary>
		///     Receive and post a newly accepted transaction for processing
		/// </summary>
		/// <param name="transaction"></param>
		protected virtual async Task InterpretBlockSnapshots(BLOCK block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// refresh our accounts list
			if(chainStateProvider.DiskBlockHeight < block.BlockId.Value) {
				throw new ApplicationException($"Invalid disk block height value. Should be at least {block.BlockId}.");
			}

			if(chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.FullSnapshotInterpretationCompleted)) {
				// ok, the interpretation has been fully performed, we don't need to repeat it

				return;
			}

			List<Func<LockContext, Task>> serializationActions = new List<Func<LockContext, Task>>();

			ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotsTransactionInterpretationProcessor = this.CreateSnapshotsTransactionInterpretationProcessor();

			// now lets process the snapshots
			snapshotsTransactionInterpretationProcessor.Reset();

			Dictionary<TransactionId, ITransaction> confirmedTransactions = block.GetAllConfirmedTransactions();

			// now the transactions
			await snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedMasterTransactions.Cast<ITransaction>().ToList(), block.BlockId.Value, lockContext).ConfigureAwait(false);

			// now just the published accounts
			await snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedTransactions, block.BlockId.Value, lockContext).ConfigureAwait(false);

			// now, the elections
			await snapshotsTransactionInterpretationProcessor.ApplyBlockElectionsInfluence(block.FinalElectionResults, confirmedTransactions, lockContext).ConfigureAwait(false);

			// finally, anything else
			await this.ApplyBlockImpacts(snapshotsTransactionInterpretationProcessor, block.BlockId, lockContext).ConfigureAwait(false);

			SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotsModificationHistoryStack = null;

			bool interpretationSerializationDone = chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.InterpretationSerializationDone);
			bool snapshotInterpretationDone = chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.SnapshotInterpretationDone);

			if(!interpretationSerializationDone || !snapshotInterpretationDone) {

				snapshotsModificationHistoryStack = snapshotsTransactionInterpretationProcessor.GetEntriesModificationStack();
			}

			// ok, commit the impacts of this interpretation

			if(!interpretationSerializationDone) {
				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {
					Dictionary<(AccountId accountId, byte ordinal), byte[]> fastKeys = snapshotsTransactionInterpretationProcessor.GetImpactedFastKeys();

					if(fastKeys?.Any() ?? false) {
						serializationActions.AddRange(this.AccountSnapshotsProvider.PrepareKeysSerializationTasks(fastKeys));
					}
				}

				if(serializationActions.Any()) {

					try {
						if(serializationActions.Any()) {
							//throw new OutOfMemoryException();
							await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, serializationTransactionProcessor).ConfigureAwait(false);
						}
					} catch(Exception ex) {
						//TODO: what do we do here?
						NLog.Default.Error(ex, "Failed to perform serializations during block interpretation");

						throw;
					}
				}

				// now, alert the world of this new block!
				chainStateProvider.BlockInterpretationStatus |= ChainStateEntryFields.BlockInterpretationStatuses.InterpretationSerializationDone;
				interpretationSerializationDone = true;
			} else {
				// ensure we load the operations that were saved in case we need them since we are not jsut creating them

				try {
					if(serializationActions.Any()) {

						serializationTransactionProcessor.LoadUndoOperations(this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase);
					}
				} catch(Exception ex) {
					//TODO: what do we do here?
					NLog.Default.Error(ex, "Failed to serialize");

					throw;
				}

			}

			if(!snapshotInterpretationDone) {
				// this is the very last step in block insertion. succeed here, and we are good to go
				if(snapshotsModificationHistoryStack?.Any() ?? false) {
					await this.AccountSnapshotsProvider.ProcessSnapshotImpacts(snapshotsModificationHistoryStack).ConfigureAwait(false);
				}

				// now, alert the world of this new block!
				chainStateProvider.BlockInterpretationStatus |= ChainStateEntryFields.BlockInterpretationStatuses.SnapshotInterpretationDone;
				snapshotInterpretationDone = true;
			}

		}

		/// <summary>
		///     run interpretation on the wallet snapshot for our own accounts
		/// </summary>
		/// <param name="transaction"></param>
		protected virtual async Task InterpretBlockLocalWallet(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
				// ok, the interpretation has been fully performed, we don't need to repeat it
				NLog.Default.Information("Wallet is not loaded. cannot interpret block.");

				return;
			}

			List<IWalletAccount> incompleteAccounts;
			List<(AccountCache accountCache, IWalletAccount account)> incompleteAccountCaches = new List<(AccountCache accountCache, IWalletAccount account)>();

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleWrite(async (prov, lc) => {

				// new accounts can be updated systematically, they dont interpret anything
				await this.SetNewAccountsFlag(prov, lastSyncedBlockId + 1, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, lc).ConfigureAwait(false);
			}, lockContext).ConfigureAwait(false);

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(async (provider, lc) => {

				incompleteAccounts = await this.GetIncompleteAccountList(provider, lastSyncedBlockId + 1, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, lc).ConfigureAwait(false);

				foreach(IWalletAccount account in incompleteAccounts) {
					AccountCache accountCache = await this.GetIncompleteAccountCache(provider, lastSyncedBlockId + 1, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, account, lc).ConfigureAwait(false);

					if(!accountCache.combinedAccountsList.Any()) {
						continue;
					}

					incompleteAccountCaches.Add((accountCache, account));

				}

			}, lockContext).ConfigureAwait(false);

			// lets perform interpretation before we create a transaction
			List<Func<LockContext, Task>> accountActions = new List<Func<LockContext, Task>>();

			Dictionary<AccountId, (ISnapshotHistoryStackSet snapshots, IWalletAccount account, AccountCache accountCache, Dictionary<AccountId, List<Func<LockContext, Task>>> changedLocalAccounts)> modificationHistoryStacks = new Dictionary<AccountId, (ISnapshotHistoryStackSet snapshots, IWalletAccount account, AccountCache accountCache, Dictionary<AccountId, List<Func<LockContext, Task>>> changedLocalAccounts)>();

			foreach((AccountCache accountCache, IWalletAccount account) in incompleteAccountCaches) {

				ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> localTransactionInterpretationProcessor = await this.CreateLocalTransactionInterpretationProcessor(accountCache.combinedAccounts.Values.ToList(), lockContext).ConfigureAwait(false);
				AccountId currentAccountId = account.GetAccountId();

				IAccountFileInfo accountFileInfo = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountUuid, lockContext).ConfigureAwait(false);
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
				
				if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

					// all our accounts are tracked here
					localTransactionInterpretationProcessor.IsAnyAccountTracked = async accountIds => accountIds.Contains(currentAccountId);
					localTransactionInterpretationProcessor.GetTrackedAccounts = async accountIds => new[] {currentAccountId}.ToList();

					ImmutableList<AccountId> publishedAccountsList = accountCache.publishedAccountsList.Where(a => a == currentAccountId).ToImmutableList();
					ImmutableList<AccountId> dispatchedAccountsList = accountCache.dispatchedAccountsList.Where(a => a == currentAccountId).ToImmutableList();

					Dictionary<AccountId, List<Func<Task>>> changedLocalAccounts = new Dictionary<AccountId, List<Func<Task>>>();

					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList, dispatchedAccountsList);

					List<ITransaction> masterTransactions = synthesizedBlock.ConfirmedTransactions.Values.OfType<IMasterTransaction>().Cast<ITransaction>().ToList();
					await localTransactionInterpretationProcessor.InterpretTransactions(masterTransactions, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

					// now just the published accounts
					localTransactionInterpretationProcessor.IsAnyAccountTracked = async accountIds => publishedAccountsList.Any(accountIds.Contains);
					localTransactionInterpretationProcessor.GetTrackedAccounts = async accountIds => publishedAccountsList.Where(accountIds.Contains).ToList();
					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList);

					List<ITransaction> confirmedRegularTransactions = synthesizedBlock.ConfirmedTransactions.Values.Where(t => !(t is IMasterTransaction)).ToList();

					await localTransactionInterpretationProcessor.InterpretTransactions(confirmedRegularTransactions, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

					// now, the elections
					await localTransactionInterpretationProcessor.ApplyBlockElectionsInfluence(synthesizedBlock.FinalElectionResults, confirmedRegularTransactions.ToDictionary(t => t.TransactionId, t => t), lockContext).ConfigureAwait(false);

					// finally, anything else
					await this.ApplyBlockImpacts(localTransactionInterpretationProcessor, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

					SnapshotHistoryStackSet<STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> localModificationHistoryStack = localTransactionInterpretationProcessor.GetEntriesModificationStack();

					accountActions.Add(async lc => {

						// ok, commit the impacts of this interpretation
						await IndependentActionRunner.RunAsync(lc, async lc2 => {

							if(localModificationHistoryStack.Any()) {
								// now lets process the results. first, anything impacting our wallet

								// here we update our wallet snapshots

								Dictionary<long, List<Func<DbContext, LockContext, Task>>> operations = localModificationHistoryStack.CompileStandardAccountHistorySets<DbContext>(async (db, accountId, temporaryHashId, entry, lc3) => {
									// it may have been created in the local wallet transactions
									if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetStandardAccountSnapshot(accountId, lc3).ConfigureAwait(false) == null) {
										IWalletStandardAccountSnapshot accountSnapshot = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry(lc3).ConfigureAwait(false);
										this.CardUtils.Copy(entry, accountSnapshot);
										await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(accountCache.combinedAccounts[temporaryHashId], accountSnapshot, lc3).ConfigureAwait(false);
									}

									return null;
								}, async (db, accountId, entry, lc3) => {

									this.LocalAccountSnapshotEntryChanged(changedLocalAccounts, entry, await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountSnapshot(accountId, lc3).ConfigureAwait(false), lc3);

									await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountCache.combinedAccounts[accountId].AccountUuid, lc3).ConfigureAwait(false);

									return null;
								}, async (db, accountId, lc3) => {
									//do we do anything here?  we dont really delete accounts
									//TODO: delete an account in the wallet?
									return null;
								});

								// run the operations
								foreach(Func<DbContext, LockContext, Task> operation in operations.SelectMany(e => e.Value)) {
									await operation(null, lc2).ConfigureAwait(false);
								}

								//-------------------------------------------------------------------------------------------
								operations = localModificationHistoryStack.CompileJointAccountHistorySets<DbContext>(async (db, accountId, temporaryHashId, entry, lc3) => {
									// it may have been created in the local wallet transactions
									if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetJointAccountSnapshot(accountId, lc3).ConfigureAwait(false) == null) {
										IWalletJointAccountSnapshot accountSnapshot = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshotEntry(lc3).ConfigureAwait(false);
										this.CardUtils.Copy(entry, accountSnapshot);
										await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshot(accountCache.combinedAccounts[temporaryHashId], accountSnapshot, lc3).ConfigureAwait(false);
									}

									return null;
								}, async (db, accountId, entry, lc3) => {

									this.LocalAccountSnapshotEntryChanged(changedLocalAccounts, entry, await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountSnapshot(accountId, lc3).ConfigureAwait(false), lc3);

									await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountCache.combinedAccounts[accountId].AccountUuid, lc3).ConfigureAwait(false);

									return null;
								}, async (db, accountId, lc3) => {
									//do we do anything here?  we dont really delete accounts
									//TODO: delete an account in the wallet?
									return null;
								});

								// run the operations
								foreach(Func<DbContext, LockContext, Task> operation in operations.SelectMany(e => e.Value)) {
									await operation(null, lc2).ConfigureAwait(false);
								}

								// fire any extra events
								foreach(KeyValuePair<AccountId, List<Func<Task>>> entry in changedLocalAccounts) {
									foreach(Func<Task> operation in entry.Value.Where(f => f != null)) {
										await operation().ConfigureAwait(false);
									}
								}

								// now, alert the world of this new block!
								IAccountFileInfo accountFileInfo = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountUuid, lc2).ConfigureAwait(false);
								WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lc2).ConfigureAwait(false);
								chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
							}
						}, async lc2 => {

							IAccountFileInfo accountFileInfo = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountUuid, lc2).ConfigureAwait(false);
							WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lc2).ConfigureAwait(false);

							if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

								if(localModificationHistoryStack?.Any() ?? false) {
									await this.AccountSnapshotsProvider.ProcessSnapshotImpacts(localModificationHistoryStack).ConfigureAwait(false);
								}

								// now, alert the world of this new block!
								chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
							}
						}).ConfigureAwait(false);

					});

					localTransactionInterpretationProcessor.ClearLocalAccounts();
				}
			}

			if(accountActions.Any()) {
				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token, lc) => {

					// run for all accounts, try to get as much done as possible before we break for exceptions
					return IndependentActionRunner.RunAsync(lc, accountActions.ToArray());

				}, lockContext).ConfigureAwait(false);
			}

		}

		protected virtual Task ApplyBlockImpacts(ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> snapshotsTransactionInterpretationProcessor, BlockId blockId, LockContext lockContext) {
			return Task.CompletedTask;
		}

		protected virtual void LocalAccountSnapshotEntryChanged(Dictionary<AccountId, List<Func<Task>>> changedLocalAccounts, IAccountSnapshot newEntry, IWalletAccountSnapshot original, LockContext lockContext) {

		}

		protected void HandleConfirmedMasterGeneralTransaction(IMasterTransaction transaction, LockContext lockContext) {
			if(transaction is IModerationMasterTransaction moderationMasterTransaction) {
				this.HandleModerationMasterGeneralImpactTransaction(moderationMasterTransaction, lockContext);
			}
		}

		protected void HandleConfirmedMasterTransaction(long BlockId, IMasterTransaction transaction, int keyedTransactionIndex, AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, List<Func<LockContext, Task>> serializationActions, LockContext lockContext) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {
				if(transaction is IStandardPresentationTransaction presentationTransaction) {

					this.AddConfirmedTransaction(transaction, BlockId, walletActions, lockContext);

					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedStandardPresentationTransaction(BlockId, presentationTransaction, keyedTransactionIndex, dispatchedAccount, walletActions, lockContext);
				} else if(transaction is IJointPresentationTransaction jointPresentationTransaction) {
					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedJointPresentationTransaction(BlockId, jointPresentationTransaction, dispatchedAccount, walletActions, lockContext);
				} else {
					//TODO: what to do?
					throw new ApplicationException("A dispatched transaction can only be a presentation one!");
				}
			}

			if(transaction is IModerationMasterTransaction moderationMasterTransaction) {
				this.HandleModerationMasterLocalImpactTransaction(BlockId, moderationMasterTransaction, accountCache, walletActions, serializationActions, lockContext);
			} else {
				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction, BlockId, walletActions, lockContext);

					if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
						this.ProcessOwnConfirmedKeyChangeTransaction(BlockId, keyChangeTransaction, keyedTransactionIndex, publishedAccount, walletActions, lockContext);
					}
				}
			}
		}

		private void HandleModerationMasterGeneralImpactTransaction(IModerationMasterTransaction moderationMasterTransaction, LockContext lockContext) {

			if(moderationMasterTransaction is IGenesisModeratorAccountPresentationTransaction genesisModeratorAccountPresentationTransaction) {
				this.HandleGenesisModeratorAccountTransaction(genesisModeratorAccountPresentationTransaction, lockContext);
			} else if(moderationMasterTransaction is IModeratorKeyChangeTransaction moderatorKeyChangeTransaction) {
				this.HandleModeratorKeyChangeTransaction(moderatorKeyChangeTransaction, lockContext);
			}
		}

		private void HandleModerationMasterLocalImpactTransaction(long BlockId, IModerationMasterTransaction moderationMasterTransaction, AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, List<Func<LockContext, Task>> serializationActions, LockContext lockContext) {

			if(moderationMasterTransaction is IAccountResetTransaction accountResetTransaction) {

				// check if it concerns us
				if(accountCache.publishedAccounts.ContainsKey(accountResetTransaction.Account)) {

					// ok, thats us!
					this.ProcessLocalConfirmedAccountResetTransaction(BlockId, accountResetTransaction, accountCache.publishedAccounts[accountResetTransaction.Account], walletActions, lockContext);
				}
			}
		}

		protected void HandleConfirmedGeneralTransaction(ITransaction transaction, LockContext lockContext) {
			if(transaction is IModerationTransaction moderationTransaction) {
				this.HandleModerationGeneralImpactTransaction(moderationTransaction, lockContext);
			}
		}

		protected async Task HandleConfirmedTransaction(long BlockId, ITransaction transaction, AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if(dispatchedAccount != null) {

				this.AddConfirmedTransaction(transaction, BlockId, walletActions, lockContext);

			}

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);

			if(transaction is IModerationTransaction moderationTransaction) {
				await this.HandleModerationLocalImpactTransaction(BlockId, moderationTransaction, accountCache, walletActions, lockContext).ConfigureAwait(false);
			} else {

				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction, BlockId, walletActions, lockContext);

					if(transaction is ISetAccountRecoveryTransaction setAccountRecoveryTransaction) {
						this.ProcessLocalConfirmedSetAccountRecoveryTransaction(BlockId, setAccountRecoveryTransaction, publishedAccount, walletActions, lockContext);
					}

					if(transaction is ISetAccountCorrelationTransaction setAccountCorrelationIdTransaction) {
						this.ProcessLocalConfirmedSetAccountCorrelationIdTransaction(BlockId, setAccountCorrelationIdTransaction, publishedAccount, walletActions, lockContext);
					}
				}
			}
		}

		private void HandleModerationGeneralImpactTransaction(IModerationTransaction moderationTransaction, LockContext lockContext) {
			if(moderationTransaction is IChainOperatingRulesTransaction chainOperatingRulesTransaction) {
				this.HandleChainOperatingRulesTransaction(chainOperatingRulesTransaction, lockContext);
			}
		}

		private async Task HandleModerationLocalImpactTransaction(long BlockId, IModerationTransaction moderationTransaction, AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			if(moderationTransaction is IAccountResetWarningTransaction accountResetWarningTransaction) {
				if(accountCache.publishedAccounts.ContainsKey(accountResetWarningTransaction.Account)) {

					// ok, thats us!
					//TODO: we ned to raise an alert about this!!!  we are about to be reset
				}
			}

			if(moderationTransaction is IReclaimAccountsTransaction reclaimAccountsTransaction) {

				// check if it concerns us
				ImmutableList<AccountId> resetAccounts = reclaimAccountsTransaction.Accounts.Select(a => a.Account).ToImmutableList();

				ImmutableList<AccountId> ourResetAccounts = accountCache.publishedAccountsList.Where(a => resetAccounts.Contains(a)).ToImmutableList();

				if(ourResetAccounts.Any()) {

					// ok, thats us, we are begin reset!!
					//TODO: what do we do here??
				}
			}

			if(moderationTransaction is IAssignAccountCorrelationsTransaction assignAccountCorrelationsTransaction) {

				// check if it concerns us

				ImmutableList<AccountId> ourEnableAccounts = accountCache.publishedAccountsList.Where(a => assignAccountCorrelationsTransaction.EnableAccounts.Contains(a)).ToImmutableList();
				ImmutableList<AccountId> ourDisableAccounts = accountCache.publishedAccountsList.Where(a => assignAccountCorrelationsTransaction.DisableAccounts.Contains(a)).ToImmutableList();

				if(ourEnableAccounts.Any() || ourDisableAccounts.Any()) {

					//ok, we can correlate our account
					await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ChangeAccountsCorrelation(ourEnableAccounts, ourDisableAccounts, lockContext).ConfigureAwait(false);
				}
			}
		}

		protected void HandleRejectedTransaction(long BlockId, RejectedTransaction trx, AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, trx.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, trx.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {

				// handle our failed publication
				this.ProcessLocalRejectedPresentationTransaction(BlockId, trx, dispatchedAccount, walletActions, lockContext);

				this.AddRejectedTransaction(trx.TransactionId, walletActions, lockContext);
			} else if(publishedAccount != null) {

				this.AddRejectedTransaction(trx.TransactionId, walletActions, lockContext);
			}
		}

		protected void AddRejectedTransaction(TransactionId transactionId, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionRefused(transactionId));

			walletActions.Add(async lc => {
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemoveLocalTransactionCacheEntry(transactionId, lc)).ConfigureAwait(false);
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(null, transactionId, WalletTransactionHistory.TransactionStatuses.Rejected, 0, lc)).ConfigureAwait(false);

				return null;
			});
		}

		protected void AddConfirmedTransaction(ITransaction transaction, BlockId blockId, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionConfirmed(transaction.TransactionId));

			walletActions.Add(async lc => {

				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemoveLocalTransactionCacheEntry(transaction.TransactionId, lc)).ConfigureAwait(false);
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transaction, transaction.TransactionId, WalletTransactionHistory.TransactionStatuses.Confirmed, blockId, lc)).ConfigureAwait(false);

				return null;
			});

		}

		protected abstract ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateInterpretationProcessor();

		protected abstract ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateWalletInterpretationProcessor();

		public class AccountCache {
			public ImmutableDictionary<AccountId, IWalletAccount> combinedAccounts;
			public ImmutableList<AccountId> combinedAccountsList;
			public ImmutableDictionary<AccountId, IWalletAccount> dispatchedAccounts;
			public ImmutableList<AccountId> dispatchedAccountsList;
			public ImmutableDictionary<AccountId, IWalletAccount> publishedAccounts;

			public ImmutableList<AccountId> publishedAccountsList;
		}

	#region Handle Local Transactions

		protected virtual void ProcessLocalConfirmedStandardPresentationTransaction<T>(long BlockId, T trx, int keyedTransactionIndex, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IStandardPresentationTransaction {

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				NLog.Default.Verbose($"We just received confirmation that our presentation for simple account {account.AccountUuid} with temporary hash {account.AccountUuidHash} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;

				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				account.ConfirmationBlockId = BlockId;

				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(lc2 => {
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountUuid, true, account.PublicAccountId.ToString()}, new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] { }, new CorrelationContext());

					return Task.CompletedTask;
				});

				//this gives us the transaction's offsets for the keyaddress
				foreach(KeyValuePair<byte, ICryptographicKey> confirmedKey in trx.Keyset.Keys) {

					using IWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, confirmedKey.Key, lc).ConfigureAwait(false);

					key.Status = Enums.KeyStatus.Ok;

					// store the address of our key inside the block

					key.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;

					key.KeyAddress.AccountId = trx.AssignedAccountId;
					key.KeyAddress.AnnouncementBlockId = BlockId;
					key.AnnouncementBlockId = BlockId;

					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);

				}

				// anything to do with the keys here?

				foreach(KeyInfo keyInfo in account.Keys) {

				}

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountUuid, BlockId, WalletAccountChainState.BlockSyncStatuses.FullySynced, lc).ConfigureAwait(false);

				// now we create our account snap shot, we will need it forward on.
				IWalletStandardAccountSnapshot newSnapshot = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(account, lc).ConfigureAwait(false);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = BlockId;
				newSnapshot.Correlated = trx.CorrelationId.HasValue;

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot, lc).ConfigureAwait(false);

				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessLocalConfirmedJointPresentationTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IJointPresentationTransaction {

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				NLog.Default.Verbose($"We just received confirmation that our presentation for joint account {account.AccountUuid} with temporary hash {account.AccountUuidHash} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;

				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				//TODO: presentation
				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(async lc2 => {
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountUuid, true, account.PublicAccountId.ToString()}, new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] { }, new CorrelationContext());

				});

				// now we create our account snap shot, we will need it forward on.
				JOINT_WALLET_ACCOUNT_SNAPSHOT newSnapshot = (JOINT_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshot(account, lc).ConfigureAwait(false);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = BlockId;
				newSnapshot.Correlated = trx.CorrelationId.HasValue;

				foreach(ITransactionJointAccountMember entry in trx.MemberAccounts) {
					JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT newAccount = new JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT();

					this.CardUtils.Copy(entry, newAccount);

					newSnapshot.MemberAccounts.Add(newAccount);
				}

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot, lc).ConfigureAwait(false);

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountUuid, BlockId, WalletAccountChainState.BlockSyncStatuses.FullySynced, lc).ConfigureAwait(false);

				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessOwnConfirmedKeyChangeTransaction<T>(long BlockId, T keyChangeTrx, int keyedTransactionIndex, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IStandardAccountKeyChangeTransaction {

			// its our own
			if(account.Status != Enums.PublicationStatus.Published) {
				throw new ApplicationException($"We can only confirm transactions for an account that has been published. current account status '{account.Status}' is invalid.");
			}

			string keyName = account.Keys.Single(k => k.Ordinal == keyChangeTrx.NewCryptographicKey.Id).Name;

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				// swap the changed key
				using(IWalletKey nextKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountUuid, keyName, lc).ConfigureAwait(false)) {

					nextKey.Status = Enums.KeyStatus.Ok;

					// store the address of our key inside the block
					nextKey.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;
					nextKey.KeyAddress.AccountId = account.GetAccountId();
					nextKey.KeyAddress.AnnouncementBlockId = BlockId;
					nextKey.AnnouncementBlockId = BlockId;

					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(nextKey, lc).ConfigureAwait(false);
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SwapNextKey(nextKey, lc).ConfigureAwait(false);

					NLog.Default.Information($"Key named {nextKey.Name} is confirmed as changed.");
				}

				if(keyChangeTrx.IsChangingChangeKey) {
					// we must also swap the super key
					using IWalletKey nextKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountUuid, GlobalsService.SUPER_KEY_NAME, lc).ConfigureAwait(false);

					nextKey.Status = Enums.KeyStatus.Ok;

					// store the address of our key inside the block
					nextKey.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;
					nextKey.KeyAddress.AccountId = account.GetAccountId();
					nextKey.KeyAddress.AnnouncementBlockId = BlockId;
					nextKey.AnnouncementBlockId = BlockId;

					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(nextKey, lc).ConfigureAwait(false);
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SwapNextKey(nextKey, lc).ConfigureAwait(false);

					NLog.Default.Information("Super Key is also confirmed as changed.");

				}

				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(async lc2 => {

					// alert important change
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] { }, new CorrelationContext());
				});

				return successCalls;
			}

			walletActions.Add(Operation);

		}

		protected virtual void ProcessLocalConfirmedAccountResetTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IAccountResetTransaction {
			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountRecoveryTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : ISetAccountRecoveryTransaction {

			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountCorrelationIdTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : ISetAccountCorrelationTransaction {

			walletActions.Add(async lc => {

				account.Correlated = true;

				return null;
			});
		}

		protected virtual void ProcessLocalRejectedPresentationTransaction(long BlockId, RejectedTransaction trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			// thats it, this account is now rejected.
			account.Status = Enums.PublicationStatus.Rejected;
		}

	#endregion

	#region handle moderator Transactions

		protected virtual void HandleGenesisModeratorAccountTransaction<T>(T genesisModeratorAccountPresentationTransaction, LockContext lockContext)
			where T : IGenesisModeratorAccountPresentationTransaction {
			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = genesisModeratorAccountPresentationTransaction.CommunicationsCryptographicKey;
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle communicationsCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, communicationsCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.BlocksXmssCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle blocksXmssMTCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, blocksXmssMTCryptographicKey);

			// we dont do anything for the qtesla (secret) blocks key, it is provided by the block signature at every new block
			//chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, GlobalsService.MODERATOR_BLOCKS_KEY_QTESLA_ID, null);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.BlocksChangeCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle blocksChangeCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, blocksChangeCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.DigestBlocksCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle digestBlocksCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, digestBlocksCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.DigestBlocksChangeCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle digestBlocksChangeCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, digestBlocksChangeCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.BinaryCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle binaryCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, binaryCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.SuperChangeCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle superChangeCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, superChangeCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.PtahCryptographicKey;
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle ptahCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, ptahCryptographicKey);
		}

		protected virtual void HandleGenesisAccountPresentationTransaction<T>(T genesisAccountPresentationTransaction, LockContext lockContext)
			where T : IGenesisAccountPresentationTransaction {

			// do nothing
		}

		protected virtual void HandleModeratorKeyChangeTransaction<T>(T moderatorKeyChangeTransaction, LockContext lockContext)
			where T : IModeratorKeyChangeTransaction {

			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = moderatorKeyChangeTransaction.NewCryptographicKey;
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle modifiedCryptographicKey = dehydrator.ToArray();

			chainStateProvider.UpdateModeratorKey(moderatorKeyChangeTransaction.TransactionId, cryptographicKey.Id, modifiedCryptographicKey);
		}

		protected virtual void HandleChainOperatingRulesTransaction(IChainOperatingRulesTransaction chainOperatingRulesTransaction, LockContext lockContext) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			//TODO: if multi chain, we need to change this
			chainStateProvider.MaximumVersionAllowed = chainOperatingRulesTransaction.MaximumVersionAllowed.ToString();
			chainStateProvider.MinimumWarningVersionAllowed = chainOperatingRulesTransaction.MinimumWarningVersionAllowed.ToString();
			chainStateProvider.MinimumVersionAllowed = chainOperatingRulesTransaction.MaximumVersionAllowed.ToString();
			chainStateProvider.MaxBlockInterval = chainOperatingRulesTransaction.MaxBlockInterval;
			chainStateProvider.AllowGossipPresentations = chainOperatingRulesTransaction.AllowGossipPresentations;

			if(new SoftwareVersion(chainStateProvider.MinimumVersionAllowed) > GlobalSettings.SoftwareVersion) {
				throw new UnrecognizedElementException(this.CentralCoordinator.ChainId, this.CentralCoordinator.ChainName);
			}
		}

	#endregion

	}
}