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
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
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
		Task ProcessBlockImmediateGeneralImpact(BlockId blockId, byte moderatorKeyOrdinal, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		Task ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
		Task ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext);
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

	public static class InterpretationProvider{
		public class AccountCache {
			public ImmutableDictionary<AccountId, IWalletAccount> combinedAccounts;
			public ImmutableList<AccountId> combinedAccountsList;
			public ImmutableDictionary<AccountId, IWalletAccount> dispatchedAccounts;
			public ImmutableList<AccountId> dispatchedAccountsList;
			public ImmutableDictionary<AccountId, IWalletAccount> publishedAccounts;

			/// <summary>
			/// key is published account Id, value is presentation Id
			/// </summary>
			public ImmutableDictionary<AccountId, AccountId> publishedAccountsList;
		}
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

		public Task InterpretNewBlockLocalWallet(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, TaskRoutingContext taskRoutingContext, LockContext lockContext) {

			if(synthesizedBlock.BlockId == 1) {
				return this.InterpretGenesisBlockLocalWallet(synthesizedBlock, taskRoutingContext, lockContext);
			} else {
				return this.InterpretBlockLocalWallet(synthesizedBlock, lastSyncedBlockId, taskRoutingContext, lockContext);
			}
		}

		/// <summary>
		///     Here we take a block a synthesize the transactions that concern our local accounts
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		public async Task<SynthesizedBlock> SynthesizeBlock(IBlock block, LockContext lockContext) {

			InterpretationProvider.AccountCache accountCache = await this.GetAccountCache(lockContext).ConfigureAwait(false);

			// get the transactions that concern us
			Dictionary<TransactionId, ITransaction> blockConfirmedTransactions = block.GetAllConfirmedTransactions();

			return await this.SynthesizeBlock(block, accountCache, blockConfirmedTransactions, lockContext).ConfigureAwait(false);
		}

		public Task ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			return this.ProcessBlockImmediateGeneralImpact(block.BlockId, block.SignatureSet.ModeratorKeyOrdinal, block.GetAllConfirmedTransactions().Values.ToList(), serializationTransactionProcessor, lockContext);
		}

		public Task ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			return this.ProcessBlockImmediateGeneralImpact(new BlockId(synthesizedBlock.BlockId), synthesizedBlock.ModeratorKeyOrdinal, synthesizedBlock.GetAllConfirmedTransactions().Values.ToList(), serializationTransactionProcessor, lockContext);
		}

		/// <summary>
		///     determine any impact the block has on our general caches and files but NOT our personal wallet
		/// </summary>
		/// <param name="block"></param>
		public async Task ProcessBlockImmediateGeneralImpact(BlockId blockId, byte moderatorKeyOrdinal, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor, LockContext lockContext) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// refresh our accounts list
			if(chainStateProvider.DiskBlockHeight < blockId.Value) {
				throw new ApplicationException($"Invalid disk block height value. Should be at least {blockId}.");
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone)) {
				// ok, the interpretation has been fully performed, we don't need to repeat it
				return;
			}

			List<IIndexedTransaction> confirmedIndexedTransactions = transactions.OfType<IIndexedTransaction>().ToList();

			List<TransactionId> indexedTransactionIds = confirmedIndexedTransactions.Select(t => t.TransactionId).ToList();
			List<ITransaction> confirmedTransactions = transactions.Where(t => !indexedTransactionIds.Contains(t.TransactionId)).ToList();

			// first thing, lets process any transaction that might affect our wallet directly
			foreach(IIndexedTransaction trx in confirmedIndexedTransactions) {
				await this.HandleConfirmedIndexedGeneralTransaction(blockId, moderatorKeyOrdinal, trx, lockContext).ConfigureAwait(false);
			}

			foreach(ITransaction trx in confirmedTransactions) {
				await this.HandleConfirmedGeneralTransaction(blockId, moderatorKeyOrdinal, trx, lockContext).ConfigureAwait(false);
			}

			this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus |= ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone;

		}

		/// <summary>
		///     determine any impact the block has on our general wallet
		/// </summary>
		/// <param name="block"></param>
		public async Task ProcessBlockImmediateAccountsImpact(SynthesizedBlock synthesizedBlock, long lastSyncedBlockId, LockContext lockContext) {

			Dictionary<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock)> walletActionSets = new Dictionary<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock)>();
			List<Func<LockContext, Task>> serializationActions = new List<Func<LockContext, Task>>();

			InterpretationProvider.AccountCache accountCache = null;

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(async (provider, lc) => {
				accountCache = await this.GetIncompleteAccountCache(provider, synthesizedBlock.BlockId, lastSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed, lc).ConfigureAwait(false);

				if((accountCache == null) || !accountCache.combinedAccounts.Any()) {
					// ok, the interpretation has been fully performed, we don't need to repeat it
					return;
				}

				foreach(AccountId account in synthesizedBlock.Accounts) {

					List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions = new List<Func<LockContext, Task<List<Func<LockContext, Task>>>>>();
					SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock = synthesizedBlock.AccountScoped[account];

					List<IIndexedTransaction> confirmedIndexedTransactions = scopedSynthesizedBlock.ConfirmedLocalTransactions.Select(t => t.Value).OfType<IIndexedTransaction>().ToList();
					confirmedIndexedTransactions.AddRange(scopedSynthesizedBlock.ConfirmedExternalsTransactions.Select(t => t.Value).OfType<IIndexedTransaction>());

					List<TransactionId> indexedTransactionIds = confirmedIndexedTransactions.Select(t => t.TransactionId).ToList();
					List<ITransaction> confirmedTransactions = scopedSynthesizedBlock.ConfirmedLocalTransactions.Values.Where(t => !indexedTransactionIds.Contains(t.TransactionId)).ToList();
					confirmedTransactions.AddRange(scopedSynthesizedBlock.ConfirmedExternalsTransactions.Values.Where(t => !indexedTransactionIds.Contains(t.TransactionId)));

					List<RejectedTransaction> rejectedTransactions = scopedSynthesizedBlock.RejectedTransactions;

					// first, we check any election results
					foreach (SynthesizedBlock.SynthesizedElectionResult finalElectionResult in synthesizedBlock.FinalElectionResults) {

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

					foreach(IIndexedTransaction trx in confirmedIndexedTransactions) {
						
						this.HandleConfirmedIndexedTransaction(synthesizedBlock.BlockId, synthesizedBlock.ModeratorKeyOrdinal, trx, synthesizedBlock.ConfirmedIndexedTransactions[trx.TransactionId].Index, accountCache, walletActions, serializationActions, lc);
					}

					foreach(ITransaction trx in confirmedTransactions) {
						await this.HandleConfirmedTransaction(synthesizedBlock.BlockId, synthesizedBlock.ModeratorKeyOrdinal, trx, accountCache, walletActions, lc).ConfigureAwait(false);
					}

					foreach(RejectedTransaction trx in rejectedTransactions) {
						this.HandleRejectedTransaction(synthesizedBlock.BlockId, trx, accountCache, walletActions, lc);
					}

					if(walletActions.Any()) {
						walletActionSets.Add(account, (walletActions, scopedSynthesizedBlock));
					}
				}
			}, lockContext).ConfigureAwait(false);

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction(async (provider, token, lc) => {

				await this.SetNewAccountsFlag(provider, synthesizedBlock.BlockId, lastSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed, lc).ConfigureAwait(false);

				List<Func<LockContext, Task>> transactionalSuccessActions = new List<Func<LockContext, Task>>();

				List<Func<LockContext, Task>> tasks = new List<Func<LockContext, Task>>();

				foreach(KeyValuePair<AccountId, (List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock)> accountEntry in walletActionSets) {

					tasks.Add(async lc2 => {
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
					});
				}
				tasks.Add(async lc2 => {

					foreach(AccountId account in synthesizedBlock.Accounts) {
						SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock = synthesizedBlock.AccountScoped[account];

						// if there are any impacting transactions, let's add them now
						if(scopedSynthesizedBlock.ConfirmedExternalsTransactions.Any()) {

							ImmutableList<KeyValuePair<TransactionId, ITransaction>> impactedTransactions = scopedSynthesizedBlock.ConfirmedExternalsTransactions.ToImmutableList();

							foreach(KeyValuePair<TransactionId, ITransaction> entry in impactedTransactions) {
								await provider.InsertTransactionHistoryEntry(entry.Value, false, null, synthesizedBlock.BlockId, WalletTransactionHistory.TransactionStatuses.Confirmed, lc).ConfigureAwait(false);
							}
						}
					}
				});

				await IndependentActionRunner.RunAsync(lc, tasks).ConfigureAwait(false);

				// now mark the others that had no transactions

				foreach(KeyValuePair<AccountId, IWalletAccount> account in accountCache.combinedAccounts) {

					IWalletAccount walletAccount = await provider.GetWalletAccount(account.Key, lc).ConfigureAwait(false);
					IAccountFileInfo accountEntry = await provider.GetAccountFileInfo(walletAccount.AccountCode, lc).ConfigureAwait(false);

					WalletAccountChainState chainState = await accountEntry.WalletChainStatesInfo.ChainState(lc).ConfigureAwait(false);

					if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed)) {
						chainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed;
					}
				}

				await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AddTransactionSuccessActions(transactionalSuccessActions, lc).ConfigureAwait(false);
			}, lockContext).ConfigureAwait(false);
			
			await Repeater.RepeatAsync(async () => {
				if(serializationActions.Any()) {

					try {
						//TODO: should we use serialization transactions here too?
						await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, null).ConfigureAwait(false);

					} catch(Exception ex) {
						//TODO: what do we do here?
						this.CentralCoordinator.Log.Error(ex, "Failed to serialize");

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

				List<AccountId> standardAccounts = accountId.Where(a => a.IsStandard).ToList();
				List<IWalletAccount> selectedAccounts = accountsList.Where(a => standardAccounts.Contains(a.GetAccountId())).ToList();

				Dictionary<AccountId, STANDARD_WALLET_ACCOUNT_SNAPSHOT> accountSnapshots = new Dictionary<AccountId, STANDARD_WALLET_ACCOUNT_SNAPSHOT>();

				foreach(IWalletAccount account in selectedAccounts) {

					if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountCode, lc).ConfigureAwait(false) is STANDARD_WALLET_ACCOUNT_SNAPSHOT entry) {
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
					if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountCode, lc).ConfigureAwait(false) is JOINT_WALLET_ACCOUNT_SNAPSHOT entry) {
						accountSnapshots.Add(account.GetAccountId(), entry);
					}
				}

				return accountSnapshots;
			};

			transactionInterpretationProcessor.RequestCreateNewStandardAccountSnapshot += async lc => (STANDARD_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry(lc).ConfigureAwait(false);
			transactionInterpretationProcessor.RequestCreateNewJointAccountSnapshot += async lc => (JOINT_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshotEntry(lc).ConfigureAwait(false);

			transactionInterpretationProcessor.EnableLocalMode(true);

			transactionInterpretationProcessor.AccountInfluencingTransactionFound += (isOwn, impactedLocalPublishedAccounts, impactedLocalDispatchedAccounts, transaction, blockId, lc) => {
				// alert when a transaction concerns us

				List<string> impactedLocalPublishedAccountCodes = accountsList.Where(a => impactedLocalPublishedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountCode).ToList();
				List<string> impactedLocalDispatchedAccountCodes = accountsList.Where(a => impactedLocalDispatchedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountCode).ToList();

				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionReceived(impactedLocalPublishedAccounts, impactedLocalPublishedAccountCodes, impactedLocalDispatchedAccounts, impactedLocalDispatchedAccountCodes, transaction.TransactionId));
				
				return Task.CompletedTask;
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

			// if we use Key dictionary, we track all keys. otherwise, whatever account we track
			transactionInterpretationProcessor.IsAnyAccountKeysTracked = (ids, accounts) => {

				BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				return this.AccountSnapshotsProvider.AnyAccountTracked(accounts);
			};

			// we track them all
			transactionInterpretationProcessor.IsAnyAccreditationCertificateTracked = async ids => true;
			transactionInterpretationProcessor.IsAnyChainOptionTracked = async ids => true;

			transactionInterpretationProcessor.RequestStandardAccountSnapshots += async (accountIds, lc) => {

				List<AccountId> standardAccounts = accountIds.Where(a => a.IsStandard).ToList();
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
					if(configuration.EnableKeyDictionaryIndex || await this.AccountSnapshotsProvider.IsAccountTracked(key.AccountId.ToAccountId()).ConfigureAwait(false)) {
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

		protected virtual async Task<SynthesizedBlock> SynthesizeBlock(IBlock block, InterpretationProvider.AccountCache accountCache, Dictionary<TransactionId, ITransaction> blockConfirmedTransactions, LockContext lockContext) {
			SynthesizedBlock synthesizedBlock = this.CreateSynthesizedBlock();

			synthesizedBlock.BlockId = block.BlockId.Value;
			synthesizedBlock.ModeratorKeyOrdinal = block.SignatureSet.ModeratorKeyOrdinal;

			ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> localTransactionInterpretationProcessor = await this.CreateLocalTransactionInterpretationProcessor(accountCache.combinedAccounts.Values.ToList(), lockContext).ConfigureAwait(false);

			localTransactionInterpretationProcessor.IsAnyAccountTracked = accountIds => Task.FromResult(accountCache.combinedAccountsList.Any(accountIds.Contains));
			localTransactionInterpretationProcessor.GetTrackedAccounts = accountIds => Task.FromResult(accountCache.combinedAccountsList.Where(accountIds.Contains).ToList());
			localTransactionInterpretationProcessor.SetLocalAccounts(accountCache.publishedAccountsList, accountCache.dispatchedAccountsList);

			List<ITransaction> confirmedLocalTransactions = null;
			List<(ITransaction transaction, AccountId targetAccount)> confirmedExternalsTransactions = null;
			Dictionary<AccountId, List<TransactionId>> accountsTransactions = null;

			(confirmedLocalTransactions, confirmedExternalsTransactions, accountsTransactions) = await localTransactionInterpretationProcessor.GetImpactingTransactionsList(blockConfirmedTransactions.Values.ToList(), accountCache, lockContext).ConfigureAwait(false);

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

				synthesizedBlock.AccountScoped.Add(account, synthesizedBlockAccountSet);
			}

			var confirmedIndexTransactions = block.GetAllIndexedTransactions();
			var accountScopedTransactions = synthesizedBlock.AccountScoped.SelectMany(a => a.Value.ConfirmedTransactions).Distinct();
			synthesizedBlock.ConfirmedTransactions = accountScopedTransactions.Where(e => !(e.Value is IIndexedTransaction)).ToDictionary();
			synthesizedBlock.ConfirmedIndexedTransactions = accountScopedTransactions.Where(e => e.Value is IIndexedTransaction).ToDictionary(e => e.Key, e => new SynthesizedBlock.IndexedTransaction(e.Value, confirmedIndexTransactions.Single(t => t.TransactionId == e.Key).index));

			synthesizedBlock.RejectedTransactions = synthesizedBlock.AccountScoped.SelectMany(a => a.Value.RejectedTransactions).Distinct().ToList();

			synthesizedBlock.Accounts = accountCache.combinedAccountsList.Distinct().ToList();

			List<TransactionId> allTransactions = synthesizedBlock.ConfirmedTransactions.Keys.ToList();
			allTransactions.AddRange(synthesizedBlock.RejectedTransactions.Select(t => t.TransactionId));

			// let's add the election results that may concern us
			foreach(IFinalElectionResults result in block.FinalElectionResults.Where(r => r.ElectedCandidates.Any(c => accountCache.combinedAccounts.ContainsKey(c.Key)) || r.DelegateAccounts.Any(c => accountCache.combinedAccounts.ContainsKey(c.Key)))) {

				synthesizedBlock.FinalElectionResults.Add(this.SynthesizeElectionResult(synthesizedBlock, result, block, accountCache, blockConfirmedTransactions));
			}

			return synthesizedBlock;
		}

		protected virtual SynthesizedBlock.SynthesizedElectionResult SynthesizeElectionResult(SynthesizedBlock synthesizedBlock, IFinalElectionResults result, IBlock block, InterpretationProvider.AccountCache accountCache, Dictionary<TransactionId, ITransaction> blockConfirmedTransactions) {

			SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult = synthesizedBlock.CreateSynthesizedElectionResult();

			synthesizedElectionResult.BlockId = block.BlockId.Value - result.BlockOffset;
			synthesizedElectionResult.Timestamp = block.FullTimestamp;

			synthesizedElectionResult.DelegateAccounts = result.DelegateAccounts.Where(r => accountCache.combinedAccounts.ContainsKey(r.Key)).Select(a => a.Key).ToList();
			synthesizedElectionResult.ElectedAccounts = result.ElectedCandidates.Where(c => accountCache.combinedAccounts.ContainsKey(c.Key)).ToDictionary(e => e.Key, e => (e.Key, e.Value.DelegateAccountId, electedTier: e.Value.ElectedTier, string.Join(",", e.Value.Transactions.Select(t => t.ToString()))));

			return synthesizedElectionResult;
		}

		protected Task<List<IWalletAccount>> GetIncompleteAccountList(IWalletProvider walletProvider, long blockSyncHeight, long latestSyncedBlockId, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {
			return walletProvider.GetWalletSyncableAccounts(blockSyncHeight, latestSyncedBlockId, lockContext);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<InterpretationProvider.AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {

			List<IWalletAccount> accountsList = await this.GetIncompleteAccountList(walletProvider, blockSyncHeight, previousSyncedBlockId, flagFilter, lockContext).ConfigureAwait(false);

			return await this.GetIncompleteAccountCache(walletProvider, blockSyncHeight, accountsList, flagFilter, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<InterpretationProvider.AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, long previousSyncedBlockId, WalletAccountChainState.BlockSyncStatuses flagFilter, IWalletAccount filterAccount, LockContext lockContext) {

			List<IWalletAccount> accounts = await this.GetIncompleteAccountList(walletProvider, blockSyncHeight, previousSyncedBlockId, flagFilter, lockContext).ConfigureAwait(false);
			List<IWalletAccount> accountsList = accounts.Where(a => a.AccountCode == filterAccount.AccountCode).ToList();

			return await this.GetIncompleteAccountCache(walletProvider, blockSyncHeight, accountsList, flagFilter, lockContext).ConfigureAwait(false);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected async Task<InterpretationProvider.AccountCache> GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, List<IWalletAccount> accountsList, WalletAccountChainState.BlockSyncStatuses flagFilter, LockContext lockContext) {

			List<IWalletAccount> tempList = new List<IWalletAccount>();

			foreach(IWalletAccount a in accountsList) {
				IAccountFileInfo accountFileInfo = await walletProvider.GetAccountFileInfo(a.AccountCode, lockContext).ConfigureAwait(false);
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
		protected async Task<InterpretationProvider.AccountCache> GetAccountCache(LockContext lockContext, long? blockSyncHeight = null, long? latestSyncedBlockId = null) {
			if ((blockSyncHeight.HasValue && !latestSyncedBlockId.HasValue)
				|| (!blockSyncHeight.HasValue && latestSyncedBlockId.HasValue))
				throw new ArgumentException($"Both optional parameters ({nameof(blockSyncHeight)} and {nameof(latestSyncedBlockId)}) must have a value if they are used. One cannot be null if the other is not.");

			List<IWalletAccount> accountsList = blockSyncHeight.HasValue && latestSyncedBlockId.HasValue ? await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(blockSyncHeight.Value, latestSyncedBlockId.Value, lockContext).ConfigureAwait(false) : await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts(lockContext).ConfigureAwait(false);

			return this.PrepareAccountCache(accountsList, lockContext);
		}

		/// <summary>
		///     Set a status flag to all New accounts
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="latestSyncedBlockId"></param>
		/// <param name="statusFlag"></param>
		protected async Task<bool> SetNewAccountsFlag(IWalletProvider provider, long blockId, long latestSyncedBlockId, WalletAccountChainState.BlockSyncStatuses statusFlag, LockContext lockContext) {
			bool changed = false;

			List<IWalletAccount> syncableAccounts = await provider.GetWalletSyncableAccounts(blockId, latestSyncedBlockId, lockContext).ConfigureAwait(false);

			foreach(IWalletAccount account in syncableAccounts.Where(a => a.Status == Enums.PublicationStatus.New || a.ConfirmationBlockId == 0)) {

				IAccountFileInfo accountFileInfo = await provider.GetAccountFileInfo(account.AccountCode, lockContext).ConfigureAwait(false);
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

				if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(statusFlag)) {

					chainState.BlockSyncStatus |= (int) statusFlag;
					changed = true;
				}
			}

			return changed;
		}

		protected InterpretationProvider.AccountCache PrepareAccountCache(List<IWalletAccount> accountsList, LockContext lockContext) {
			InterpretationProvider.AccountCache accountCache = new InterpretationProvider.AccountCache();
			accountCache.publishedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Published).ToImmutableDictionary(a => a.PublicAccountId, a => a);
			accountCache.dispatchedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Dispatched).ToImmutableDictionary(a => a.PresentationId, a => a);

			Dictionary<AccountId, IWalletAccount> combinedTemp = accountCache.publishedAccounts.ToDictionary();

			foreach(KeyValuePair<AccountId, IWalletAccount> e in accountCache.dispatchedAccounts) {
				combinedTemp.Add(e.Key, e.Value);
			}

			accountCache.combinedAccounts = combinedTemp.ToImmutableDictionary(e => e.Key, e => e.Value);

			accountCache.publishedAccountsList = accountCache.publishedAccounts.ToImmutableDictionary(a => a.Key, a => a.Value.PresentationId);
			accountCache.dispatchedAccountsList = accountCache.dispatchedAccounts.Keys.ToImmutableList();

			List<AccountId> combinedAccountsList = accountCache.publishedAccountsList.Keys.ToList();
			combinedAccountsList.AddRange(accountCache.publishedAccountsList.Values);
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
			await snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedIndexedTransactions.Cast<ITransaction>().ToList(), block.BlockId.Value, lockContext).ConfigureAwait(false);

			await this.ProcessConfirmedIndexedTransactions(block, block.ConfirmedIndexedTransactions).ConfigureAwait(false);
			
			// now just the published accounts
			await snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedTransactions, block.BlockId.Value, lockContext).ConfigureAwait(false);

			await this.ProcessConfirmedTransactions(block, block.ConfirmedTransactions).ConfigureAwait(false);
			await this.ProcessRejectedTransactions(block, block.RejectedTransactions).ConfigureAwait(false);
			
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
				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyDictionaryIndex) {
					Dictionary<(AccountId accountId, byte ordinal), byte[]> keyDictionary = snapshotsTransactionInterpretationProcessor.GetImpactedKeyDictionary();

					if(keyDictionary?.Any() ?? false) {
						serializationActions.AddRange(this.AccountSnapshotsProvider.PrepareKeysSerializationTasks(keyDictionary));
					}
				}

				if(serializationActions.Any()) {

					try {
						if(serializationActions.Any()) {
							await this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, serializationTransactionProcessor).ConfigureAwait(false);
						}
					} catch(Exception ex) {
						//TODO: what do we do here?
						this.CentralCoordinator.Log.Error(ex, "Failed to perform serializations during block interpretation");

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

						serializationTransactionProcessor?.LoadUndoOperations(this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase);
					}
				} catch(Exception ex) {
					//TODO: what do we do here?
					this.CentralCoordinator.Log.Error(ex, "Failed to serialize");

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
				this.CentralCoordinator.Log.Information("Wallet is not loaded. cannot interpret block.");

				
				return;
			}

			List<IWalletAccount> incompleteAccounts;
			List<(InterpretationProvider.AccountCache accountCache, IWalletAccount account)> incompleteAccountCaches = new List<(InterpretationProvider.AccountCache accountCache, IWalletAccount account)>();

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleWrite(async (prov, lc) => {

				// new accounts can be updated systematically, they dont interpret anything
				await this.SetNewAccountsFlag(prov, synthesizedBlock.BlockId, lastSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, lc).ConfigureAwait(false);
			}, lockContext).ConfigureAwait(false);

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(async (provider, lc) => {

				incompleteAccounts = await this.GetIncompleteAccountList(provider, synthesizedBlock.BlockId, lastSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, lc).ConfigureAwait(false);

				foreach(IWalletAccount account in incompleteAccounts) {
					InterpretationProvider.AccountCache accountCache = await this.GetIncompleteAccountCache(provider, synthesizedBlock.BlockId, lastSyncedBlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, account, lc).ConfigureAwait(false);

					if(!accountCache.combinedAccountsList.Any()) {
						continue;
					}

					incompleteAccountCaches.Add((accountCache, account));

				}

			}, lockContext).ConfigureAwait(false);

			// lets perform interpretation before we create a transaction
			List<Func<LockContext, Task>> accountActions = new List<Func<LockContext, Task>>();

			Dictionary<AccountId, (ISnapshotHistoryStackSet snapshots, IWalletAccount account, InterpretationProvider.AccountCache accountCache, Dictionary<AccountId, List<Func<LockContext, Task>>> changedLocalAccounts)> modificationHistoryStacks = new Dictionary<AccountId, (ISnapshotHistoryStackSet snapshots, IWalletAccount account, InterpretationProvider.AccountCache accountCache, Dictionary<AccountId, List<Func<LockContext, Task>>> changedLocalAccounts)>();

			foreach((InterpretationProvider.AccountCache accountCache, IWalletAccount account) in incompleteAccountCaches) {

				ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> localTransactionInterpretationProcessor = await this.CreateLocalTransactionInterpretationProcessor(accountCache.combinedAccounts.Values.ToList(), lockContext).ConfigureAwait(false);
				AccountId currentAccountId = account.GetAccountId();

				IAccountFileInfo accountFileInfo = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountCode, lockContext).ConfigureAwait(false);
				WalletAccountChainState chainState = await accountFileInfo.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);
				
				if(!((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

					// all our accounts are tracked here
					localTransactionInterpretationProcessor.IsAnyAccountTracked = async accountIds => accountIds.Contains(currentAccountId);
					localTransactionInterpretationProcessor.GetTrackedAccounts = async accountIds => new[] {currentAccountId}.ToList();

					ImmutableDictionary<AccountId, AccountId> publishedAccountsList = accountCache.publishedAccountsList.Where(a => a.Key == currentAccountId).ToImmutableDictionary();
					ImmutableList<AccountId> dispatchedAccountsList = accountCache.dispatchedAccountsList.Where(a => a == currentAccountId).ToImmutableList();

					Dictionary<AccountId, List<Func<Task>>> changedLocalAccounts = new Dictionary<AccountId, List<Func<Task>>>();

					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList, dispatchedAccountsList);

					List<ITransaction> indexedTransactions = synthesizedBlock.ConfirmedTransactions.Values.OfType<IIndexedTransaction>().Cast<ITransaction>().ToList();
					await localTransactionInterpretationProcessor.InterpretTransactions(indexedTransactions, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false);

					// now just the published accounts
					localTransactionInterpretationProcessor.IsAnyAccountTracked = async accountIds => publishedAccountsList.Keys.Any(accountIds.Contains);
					localTransactionInterpretationProcessor.GetTrackedAccounts = async accountIds => publishedAccountsList.Keys.Where(accountIds.Contains).ToList();
					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList);

					List<ITransaction> confirmedRegularTransactions = synthesizedBlock.ConfirmedTransactions.Values.Where(t => !(t is IIndexedTransaction)).ToList();

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

								Dictionary<AccountId, List<Func<DbContext, LockContext, Task>>> operations = localModificationHistoryStack.CompileStandardAccountHistorySets<DbContext>(async (db, accountId, temporaryHashId, entry, lc3) => {
									// it may have been created in the local wallet transactions
									if(await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetStandardAccountSnapshot(accountId, lc3).ConfigureAwait(false) == null) {
										IWalletStandardAccountSnapshot accountSnapshot = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry(lc3).ConfigureAwait(false);
										this.CardUtils.Copy(entry, accountSnapshot);
										await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(accountCache.combinedAccounts[temporaryHashId], accountSnapshot, lc3).ConfigureAwait(false);
									}

									return null;
								}, async (db, accountId, entry, lc3) => {

									this.LocalAccountSnapshotEntryChanged(changedLocalAccounts, entry, await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountSnapshot(accountId, lc3).ConfigureAwait(false), lc3);

									await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountCache.combinedAccounts[accountId].AccountCode, lc3).ConfigureAwait(false);

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

									await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountCache.combinedAccounts[accountId].AccountCode, lc3).ConfigureAwait(false);

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
								IAccountFileInfo accountFileInfo1 = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountCode, lc2).ConfigureAwait(false);
								WalletAccountChainState chainState1 = await accountFileInfo1.WalletChainStatesInfo.ChainState(lc2).ConfigureAwait(false);
								chainState1.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
							}
						}, async lc2 => {

							IAccountFileInfo accountFileInfo1 = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(account.AccountCode, lc2).ConfigureAwait(false);
							WalletAccountChainState chainState1 = await accountFileInfo1.WalletChainStatesInfo.ChainState(lc2).ConfigureAwait(false);

							if(!((WalletAccountChainState.BlockSyncStatuses) chainState1.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

								if(localModificationHistoryStack?.Any() ?? false) {
									await this.AccountSnapshotsProvider.ProcessSnapshotImpacts(localModificationHistoryStack).ConfigureAwait(false);
								}

								// now, alert the world of this new block!
								chainState1.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
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

		protected async Task HandleConfirmedIndexedGeneralTransaction(BlockId blockId, byte moderatorKeyOrdinal, IIndexedTransaction transaction, LockContext lockContext) {
			if(transaction is IModerationIndexedTransaction moderationIndexedTransaction) {
				await this.HandleModerationIndexedGeneralImpactTransaction(blockId, moderatorKeyOrdinal, moderationIndexedTransaction, lockContext).ConfigureAwait(false);
			}
		}

		protected void HandleConfirmedIndexedTransaction(BlockId blockId, byte moderatorKeyOrdinal, IIndexedTransaction transaction, int indexedTransactionIndex, InterpretationProvider.AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, List<Func<LockContext, Task>> serializationActions, LockContext lockContext) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {
				if(transaction is IStandardPresentationTransaction presentationTransaction) {

					this.AddConfirmedTransaction(transaction, blockId, walletActions, lockContext);

					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedStandardPresentationTransaction(blockId, presentationTransaction, indexedTransactionIndex, dispatchedAccount, walletActions, lockContext);
				} else if(transaction is IJointPresentationTransaction jointPresentationTransaction) {
					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedJointPresentationTransaction(blockId, jointPresentationTransaction, dispatchedAccount, walletActions, lockContext);
				} else {
					//TODO: what to do?
					throw new ApplicationException("A dispatched transaction can only be a presentation one!");
				}
			}

			if(transaction is IModerationIndexedTransaction moderationIndexedTransaction) {
				this.HandleModerationIndexedLocalImpactTransaction(blockId, moderatorKeyOrdinal, moderationIndexedTransaction, accountCache, walletActions, serializationActions, lockContext);
			} else {
				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction, blockId, walletActions, lockContext);

					if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
						this.ProcessOwnConfirmedKeyChangeTransaction(blockId, keyChangeTransaction, indexedTransactionIndex, publishedAccount, walletActions, lockContext);
					}
				}
			}
		}

		private async Task HandleModerationIndexedGeneralImpactTransaction(BlockId blockId, byte moderatorKeyOrdinal, IModerationIndexedTransaction moderationIndexedTransaction, LockContext lockContext) {

			if(moderationIndexedTransaction is IGenesisModeratorAccountPresentationTransaction genesisModeratorAccountPresentationTransaction) {
				await this.HandleGenesisModeratorAccountTransaction(genesisModeratorAccountPresentationTransaction, lockContext).ConfigureAwait(false);
			} else if(moderationIndexedTransaction is IModeratorKeyChangeTransaction moderatorKeyChangeTransaction) {
				await this.HandleModeratorKeyChangeTransaction(blockId, moderatorKeyOrdinal, moderatorKeyChangeTransaction, lockContext).ConfigureAwait(false);
			}
		}

		private void HandleModerationIndexedLocalImpactTransaction(BlockId blockId, byte moderatorKeyOrdinal, IModerationIndexedTransaction moderationIndexedTransaction, InterpretationProvider.AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, List<Func<LockContext, Task>> serializationActions, LockContext lockContext) {

			if(moderationIndexedTransaction is IAccountResetTransaction accountResetTransaction) {

				// check if it concerns us
				if(accountCache.publishedAccounts.ContainsKey(accountResetTransaction.Account)) {

					// ok, thats us!
					this.ProcessLocalConfirmedAccountResetTransaction(blockId, moderatorKeyOrdinal, accountResetTransaction, accountCache.publishedAccounts[accountResetTransaction.Account], walletActions, lockContext);
				}
			}
		}

		protected async Task HandleConfirmedGeneralTransaction(BlockId blockId, byte moderatorKeyOrdinal, ITransaction transaction, LockContext lockContext) {
			if(transaction is IModerationTransaction moderationTransaction) {
				await this.HandleModerationGeneralImpactTransaction(blockId, moderationTransaction, lockContext).ConfigureAwait(false);
			}
		}

		protected async Task HandleConfirmedTransaction(BlockId blockId, byte moderatorKeyOrdinal, ITransaction transaction, InterpretationProvider.AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if(dispatchedAccount != null) {

				this.AddConfirmedTransaction(transaction, blockId, walletActions, lockContext);

			}

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);

			if(transaction is IModerationTransaction moderationTransaction) {
				await this.HandleModerationLocalImpactTransaction(blockId, moderationTransaction, accountCache, walletActions, lockContext).ConfigureAwait(false);
			} else {

				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction, blockId, walletActions, lockContext);

					if(transaction is ISetAccountRecoveryTransaction setAccountRecoveryTransaction) {
						this.ProcessLocalConfirmedSetAccountRecoveryTransaction(blockId, moderatorKeyOrdinal, setAccountRecoveryTransaction, publishedAccount, walletActions, lockContext);
					}

					if(transaction is ISetAccountCorrelationTransaction setAccountCorrelationIdTransaction) {
						this.ProcessLocalConfirmedSetAccountCorrelationIdTransaction(blockId, moderatorKeyOrdinal, setAccountCorrelationIdTransaction, publishedAccount, walletActions, lockContext);
					}
				}
			}
		}

		private async Task HandleModerationGeneralImpactTransaction(BlockId blockId, IModerationTransaction moderationTransaction, LockContext lockContext) {
			if(moderationTransaction is IChainOperatingRulesTransaction chainOperatingRulesTransaction) {
				await this.HandleChainOperatingRulesTransaction(blockId, chainOperatingRulesTransaction, lockContext).ConfigureAwait(false);
			}
		}

		private async Task HandleModerationLocalImpactTransaction(BlockId blockId, IModerationTransaction moderationTransaction, InterpretationProvider.AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			if(moderationTransaction is IAccountResetWarningTransaction accountResetWarningTransaction) {
				if(accountCache.publishedAccounts.ContainsKey(accountResetWarningTransaction.Account)) {

					// ok, thats us!
					//TODO: we ned to raise an alert about this!!!  we are about to be reset
				}
			}

			if(moderationTransaction is IReclaimAccountsTransaction reclaimAccountsTransaction) {

				// check if it concerns us
				ImmutableList<AccountId> resetAccounts = reclaimAccountsTransaction.Accounts.Select(a => a.Account).ToImmutableList();

				ImmutableList<AccountId> ourResetAccounts = accountCache.publishedAccountsList.Keys.Where(a => resetAccounts.Contains(a)).ToImmutableList();

				if(ourResetAccounts.Any()) {

					// ok, thats us, we are begin reset!!
					//TODO: what do we do here??
				}
			}

			if(moderationTransaction is IAssignAccountCorrelationsTransaction assignAccountCorrelationsTransaction) {

				// check if it concerns us

				ImmutableList<AccountId> ourEnableAccounts = accountCache.publishedAccountsList.Keys.Where(a => assignAccountCorrelationsTransaction.EnableAccounts.Contains(a)).ToImmutableList();
				ImmutableList<AccountId> ourDisableAccounts = accountCache.publishedAccountsList.Keys.Where(a => assignAccountCorrelationsTransaction.DisableAccounts.Contains(a)).ToImmutableList();

				if(ourEnableAccounts.Any() || ourDisableAccounts.Any()) {

					//ok, we can correlate our account
					await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ChangeAccountsCorrelation(ourEnableAccounts, ourDisableAccounts, lockContext).ConfigureAwait(false);
				}
			}
		}

		protected void HandleRejectedTransaction(BlockId blockId, RejectedTransaction trx, InterpretationProvider.AccountCache accountCache, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, trx.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, trx.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {

				// handle our failed publication
				this.ProcessLocalRejectedPresentationTransaction(blockId, trx, dispatchedAccount, walletActions, lockContext);

				this.AddRejectedTransaction(trx.TransactionId, walletActions, lockContext);
			} else if(publishedAccount != null) {

				this.AddRejectedTransaction(trx.TransactionId, walletActions, lockContext);
			}
		}

		protected void AddRejectedTransaction(TransactionId transactionId, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionRefused(transactionId));

			walletActions.Add(async lc => {
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteGenerationCacheEntry(transactionId, lc)).ConfigureAwait(false);
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(null, transactionId, WalletTransactionHistory.TransactionStatuses.Rejected, 0, lc)).ConfigureAwait(false);

				return null;
			});
		}

		protected void AddConfirmedTransaction(ITransaction transaction, BlockId blockId, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionConfirmed(transaction.TransactionId));

			walletActions.Add(async lc => {

				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.DeleteGenerationCacheEntry(transaction.TransactionId, lc)).ConfigureAwait(false);
				await Repeater.RepeatAsync(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transaction, transaction.TransactionId, WalletTransactionHistory.TransactionStatuses.Confirmed, blockId, lc)).ConfigureAwait(false);

				return null;
			});

		}

		protected abstract ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateInterpretationProcessor();

		protected abstract ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateWalletInterpretationProcessor();
		
		#region Special Processing

			protected virtual async Task ProcessConfirmedIndexedTransactions(IBlock block, List<IIndexedTransaction> confirmedIndexedTransactions) {
				
				List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates = new List<(AccountId AccountId, IdKeyUseIndexSet keyGate)>();
				foreach(var transaction in confirmedIndexedTransactions) {

					if(transaction is IPresentationTransaction) {
						
					} 
					else if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
					
						// ok, a key change, we have to store it in the key gates
						keyGates.Add((keyChangeTransaction.TransactionId.Account, keyChangeTransaction.NewCryptographicKey.KeyIndex));
					}
					else {
						// everything else has a keygate
						this.AccrueKeyGate(keyGates, transaction);
					}
				}
				
				await this.SetKeyGates(keyGates).ConfigureAwait(false);
			}

		
			protected virtual async Task ProcessConfirmedTransactions(IBlock block, List<ITransaction> confirmedTransactions) {

				List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates = new List<(AccountId AccountId, IdKeyUseIndexSet keyGate)>();

				List<AccountId> reclaimAccounts = new List<AccountId>();
				foreach(var transaction in confirmedTransactions) {
					if(transaction is IReclaimAccountsTransaction reclaimAccountsTransaction) {
						reclaimAccounts.AddRange(reclaimAccountsTransaction.Accounts.Select(e => e.Account));
					} else {
						
					}
					
					this.AccrueKeyGate(keyGates, transaction);
				}
				
				await this.SetKeyGates(keyGates).ConfigureAwait(false);
				
				try {

					if(reclaimAccounts.Any() && this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyGates) {
						await this.CentralCoordinator.ChainComponentProvider.ChainValidationProviderBase.ClearKeyGates(reclaimAccounts).ConfigureAwait(false);
					}
				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, $"Failed to reclaim accounts");
				}
			}
			
			protected virtual Task ProcessRejectedTransactions(IBlock block, List<RejectedTransaction> rejectedTransactions) {
				return Task.CompletedTask;
			}
		#endregion

			protected void AccrueKeyGate(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates, ITransaction transaction) {
				if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyGates) {
					if(transaction.TransactionMeta.KeyUseLock != null) {
						keyGates.Add((transaction.TransactionId.Account, transaction.TransactionMeta.KeyUseLock));
					}

					if(transaction.TransactionMeta != null) {
						foreach(var sig in transaction.TransactionMeta.MultiSigKeyUseIndices.Where(e => e.Value.keyIndexLock != null)) {
							keyGates.Add((sig.Key, sig.Value.keyIndexLock));
						}
					}
				}
			}

			protected async Task SetKeyGates(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates) {
				if(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyGates) {

					await this.CentralCoordinator.ChainComponentProvider.ChainValidationProviderBase.SetKeyGates(keyGates).ConfigureAwait(false);

				}
			}

			
	#region Handle Local Transactions

		protected virtual void ProcessLocalConfirmedStandardPresentationTransaction<T>(BlockId blockId, T trx, int indexedTransactionIndex, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IStandardPresentationTransaction {

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				this.CentralCoordinator.Log.Verbose($"We just received confirmation that our presentation for simple account {account.AccountCode} with temporary hash {account.PresentationId} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;

				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				account.ConfirmationBlockId = blockId;

				if(account.SMSDetails != null) {
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + account.SMSDetails.VerificationSpan;
					account.VerificationLevel = Enums.AccountVerificationTypes.SMS;
				}
				else if(account.AccountAppointment != null) {
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + account.AccountAppointment.AppointmentVerificationSpan;
					account.VerificationLevel = Enums.AccountVerificationTypes.Appointment;
				}
				
				// reset all these if set, we are done
				account.SMSDetails = null;
				AppointmentUtils.ResetAppointment(account);

				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(lc2 => {
					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountCode, true, account.PublicAccountId.ToString()}, new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, Array.Empty<object>(), new CorrelationContext());

					this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.None;
					return Task.CompletedTask;
				});

				//this gives us the transaction's offsets for the keyaddress
				foreach(KeyValuePair<byte, ICryptographicKey> confirmedKey in trx.Keyset.Keys) {

					using IWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountCode, confirmedKey.Key, lc).ConfigureAwait(false);

					key.Status = Enums.KeyStatus.Ready;

					// store the address of our key inside the block

					key.KeyAddress.IndexedTransactionIndex = indexedTransactionIndex;

					key.KeyAddress.AccountId = trx.AssignedAccountId;
					key.KeyAddress.AnnouncementBlockId = blockId;
					key.AnnouncementBlockId = blockId;

					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);

				}

				// anything to do with the keys here?

				foreach(KeyInfo keyInfo in account.Keys) {

				}

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountCode, blockId, WalletAccountChainState.BlockSyncStatuses.FullySynced, lc).ConfigureAwait(false);

				// now we create our account snap shot, we will need it forward on.
				IWalletStandardAccountSnapshot newSnapshot = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(account, lc).ConfigureAwait(false);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = blockId;
				newSnapshot.Correlated = trx.CorrelationId.HasValue;

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot, lc).ConfigureAwait(false);
				
				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessLocalConfirmedJointPresentationTransaction<T>(BlockId blockId, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IJointPresentationTransaction {

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				this.CentralCoordinator.Log.Verbose($"We just received confirmation that our presentation for joint account {account.AccountCode} with temporary hash {account.PresentationId} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;

				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				if(account.SMSDetails != null) {
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + account.SMSDetails.VerificationSpan;
					account.VerificationLevel = Enums.AccountVerificationTypes.SMS;
				}
				else if(account.AccountAppointment != null) {
					account.VerificationExpirationDate = DateTimeEx.CurrentTime + account.AccountAppointment.AppointmentVerificationSpan;
					account.VerificationLevel = Enums.AccountVerificationTypes.Appointment;
				}
				
				// reset all these if set, we are done
				account.SMSDetails = null;
				AppointmentUtils.ResetAppointment(account);

				//TODO: presentation
				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(async lc2 => {
					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, new CorrelationContext());

					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountCode, true, account.PublicAccountId.ToString()}, new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, Array.Empty<object>(), new CorrelationContext());
					this.CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.OperatingMode = Enums.OperationStatus.None;
				});

				// now we create our account snap shot, we will need it forward on.
				JOINT_WALLET_ACCOUNT_SNAPSHOT newSnapshot = (JOINT_WALLET_ACCOUNT_SNAPSHOT) await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshot(account, lc).ConfigureAwait(false);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = blockId;
				newSnapshot.Correlated = trx.CorrelationId.HasValue;

				foreach(ITransactionJointAccountMember entry in trx.MemberAccounts) {
					JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT newAccount = new JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT();

					this.CardUtils.Copy(entry, newAccount);

					newSnapshot.MemberAccounts.Add(newAccount);
				}

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot, lc).ConfigureAwait(false);

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountCode, blockId, WalletAccountChainState.BlockSyncStatuses.FullySynced, lc).ConfigureAwait(false);

				this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, new CorrelationContext());
				
				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessOwnConfirmedKeyChangeTransaction<T>(BlockId blockId, T keyChangeTrx, int indexedTransactionIndex, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IStandardAccountKeyChangeTransaction {

			// its our own
			if(account.Status != Enums.PublicationStatus.Published) {
				throw new ApplicationException($"We can only confirm transactions for an account that has been published. current account status '{account.Status}' is invalid.");
			}

			string keyName = account.Keys.Single(k => k.Ordinal == keyChangeTrx.NewCryptographicKey.Ordinal).Name;

			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				// swap the changed key
				using(IWalletKey nextKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountCode, keyName, lc).ConfigureAwait(false)) {

					nextKey.Status = Enums.KeyStatus.Ready;

					// store the address of our key inside the block
					nextKey.KeyAddress.IndexedTransactionIndex = indexedTransactionIndex;
					nextKey.KeyAddress.AccountId = account.GetAccountId();
					nextKey.KeyAddress.AnnouncementBlockId = blockId;
					nextKey.AnnouncementBlockId = blockId;

					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetNextKey(nextKey, lc).ConfigureAwait(false);
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SwapNextKey(nextKey, lc).ConfigureAwait(false);

					this.CentralCoordinator.Log.Information($"Key named {nextKey.Name} is confirmed as changed.");
				}

				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(lc2 => {

					// alert important change
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, Array.Empty<object>(), new CorrelationContext());
					
					return Task.CompletedTask;
				});

				return successCalls;
			}

			walletActions.Add(Operation);

		}

		protected virtual void ProcessLocalConfirmedAccountResetTransaction<T>(BlockId blockId, byte moderatorKeyOrdinal, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : IAccountResetTransaction {
			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountRecoveryTransaction<T>(BlockId blockId, byte moderatorKeyOrdinal, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : ISetAccountRecoveryTransaction {

			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountCorrelationIdTransaction<T>(BlockId blockId, byte moderatorKeyOrdinal, T trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext)
			where T : ISetAccountCorrelationTransaction {

			walletActions.Add(async lc => {

				account.VerificationLevel = Enums.AccountVerificationTypes.KYC;

				return null;
			});
		}

		protected virtual void ProcessLocalRejectedPresentationTransaction(BlockId blockId, RejectedTransaction trx, IWalletAccount account, List<Func<LockContext, Task<List<Func<LockContext, Task>>>>> walletActions, LockContext lockContext) {
			// thats it, this account is now rejected.
			
			if(account.Status != Enums.PublicationStatus.Dispatched) {
				throw new ApplicationException($"current account status '{account.Status}' is invalid.");
			}
			async Task<List<Func<LockContext, Task>>> Operation(LockContext lc) {

				account.Status = Enums.PublicationStatus.Rejected;
				
				List<Func<LockContext, Task>> successCalls = new List<Func<LockContext, Task>>();

				successCalls.Add(async lc2 => {

					// alert important change
					this.centralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountStatusUpdated, new CorrelationContext());
				});

				return successCalls;
			}

			walletActions.Add(Operation);

		}

	#endregion

	#region handle moderator Transactions

		protected virtual async Task HandleGenesisModeratorAccountTransaction<T>(T genesisModeratorAccountPresentationTransaction, LockContext lockContext)
			where T : IGenesisModeratorAccountPresentationTransaction {
			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = genesisModeratorAccountPresentationTransaction.CommunicationsCryptographicKey;

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);
			using SafeArrayHandle communicationsCryptographicKey = dehydrator.ToArray();
			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, communicationsCryptographicKey).ConfigureAwait(false);

			
			cryptographicKey = genesisModeratorAccountPresentationTransaction.ValidatorSecretsCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle validatorSecretsCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, validatorSecretsCryptographicKey).ConfigureAwait(false);
			
			cryptographicKey = genesisModeratorAccountPresentationTransaction.BlocksXmssCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle blocksXmssMTCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, blocksXmssMTCryptographicKey).ConfigureAwait(false);

			// we dont do anything for the qtesla (secret) blocks key, it is provided by the block signature at every new block
			//chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, GlobalsService.MODERATOR_BLOCKS_KEY_QTESLA_ID, null);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.BlocksChangeCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle blocksChangeCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, blocksChangeCryptographicKey).ConfigureAwait(false);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.DigestBlocksCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle digestBlocksCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, digestBlocksCryptographicKey).ConfigureAwait(false);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.DigestBlocksChangeCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle digestBlocksChangeCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, digestBlocksChangeCryptographicKey).ConfigureAwait(false);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.GossipCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle gossipCryptographicKey = dehydrator.ToArray();
			
			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, gossipCryptographicKey).ConfigureAwait(false);
			
			cryptographicKey = genesisModeratorAccountPresentationTransaction.BinaryCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle binaryCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, binaryCryptographicKey).ConfigureAwait(false);
			
			cryptographicKey = genesisModeratorAccountPresentationTransaction.MessageCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle messageCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, messageCryptographicKey).ConfigureAwait(false);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.SuperChangeCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle superChangeCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, superChangeCryptographicKey).ConfigureAwait(false);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.PtahCryptographicKey;
			dehydrator?.Dispose();
			dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			using SafeArrayHandle ptahCryptographicKey = dehydrator.ToArray();

			await chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Ordinal, ptahCryptographicKey).ConfigureAwait(false);
			dehydrator?.Dispose();
		}

		protected virtual async Task HandleGenesisAccountPresentationTransaction<T>(T genesisAccountPresentationTransaction, LockContext lockContext)
			where T : IGenesisAccountPresentationTransaction {

			// do nothing
		}
		
		protected virtual async Task HandleModeratorKeyChangeTransaction<T>(BlockId blockId, byte moderatorKeyOrdinal, T moderatorKeyChangeTransaction, LockContext lockContext)
			where T : IModeratorKeyChangeTransaction {
			
			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = moderatorKeyChangeTransaction.NewCryptographicKey;

			if(GlobalsService.ValidateBlockKeyTree(moderatorKeyOrdinal, cryptographicKey.Ordinal)) {

				await Repeater.RepeatAsync(async () => {
					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
					cryptographicKey.Dehydrate(dehydrator);

					SafeArrayHandle modifiedCryptographicKey = dehydrator.ToArray();

					await chainStateProvider.UpdateModeratorKey(moderatorKeyChangeTransaction.TransactionId, cryptographicKey.Ordinal, modifiedCryptographicKey, true).ConfigureAwait(false);

					if(cryptographicKey is IXmssCryptographicKey) {
						await chainStateProvider.UpdateModeratorExpectedNextKeyIndex(cryptographicKey.Ordinal, cryptographicKey.KeyIndex.KeyUseSequenceId.Value, cryptographicKey.KeyIndex.KeyUseIndex).ConfigureAwait(false);
					}
				}).ConfigureAwait(false);
			}
		}

		protected virtual async Task HandleChainOperatingRulesTransaction(BlockId blockId, IChainOperatingRulesTransaction chainOperatingRulesTransaction, LockContext lockContext) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			//TODO: if multi chain, we need to change this
			chainStateProvider.MaximumVersionAllowed = chainOperatingRulesTransaction.MaximumVersionAllowed.ToString();
			chainStateProvider.MinimumWarningVersionAllowed = chainOperatingRulesTransaction.MinimumWarningVersionAllowed.ToString();
			chainStateProvider.MinimumVersionAllowed = chainOperatingRulesTransaction.MaximumVersionAllowed.ToString();
			chainStateProvider.MaxBlockInterval = chainOperatingRulesTransaction.MaxBlockInterval;
			chainStateProvider.AllowGossipPresentations = chainOperatingRulesTransaction.AllowGossipPresentations;

			if(new SoftwareVersion(chainStateProvider.MinimumVersionAllowed) > GlobalSettings.BlockchainCompatibilityVersion) {
				throw new UnrecognizedElementException(this.CentralCoordinator.ChainId, this.CentralCoordinator.ChainName);
			}
		}

	#endregion

	}
}