using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Results.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Widgets;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.TransactionInterpretation.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IInterpretationProvider: IChainProvider {
	}

	public interface IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IInterpretationProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		void InterpretNewBlockSnapshots(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor);
		void InterpretNewBlockLocalWallet(SynthesizedBlock synthesizedBlock, TaskRoutingContext taskRoutingContext);

		void InterpretGenesisBlockSnapshots(IGenesisBlock genesisBlock);
		void InterpretGenesisBlockLocalWallet(SynthesizedBlock synthesizedBlockk, TaskRoutingContext taskRoutingContext);

		SynthesizedBlock SynthesizeBlock(IBlock block);
		void ProcessBlockImmediateGeneralImpact(BlockId blockId, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor);
		void ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor);
		void ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor);
		void ProcessBlockImmediateAccountsImpact(SynthesizedBlock synthesizedBlock);

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
	/// <CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
	///     "></typeparam>
	///     <typeparam name="WALLET_PROVIDER"></typeparam>
	///     <typeparam name="USERWALLET"></typeparam>
	///     <typeparam name="WALLET_IDENTITY"></typeparam>
	///     <typeparam name="WALLET_KEY"></typeparam>
	///     <typeparam name="WALLET_KEY_HISTORY"></typeparam>
	public abstract class InterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_CONTEXT, BLOCK, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT> : IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, STANDARD_ACCOUNT_SNAPSHOT_CONTEXT, JOINT_ACCOUNT_SNAPSHOT_CONTEXT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT_CONTEXT, STANDARD_ACCOUNT_KEYS_SNAPSHOT_CONTEXT, CHAIN_OPTIONS_SNAPSHOT_CONTEXT, TRACKED_ACCOUNTS_CONTEXT, BLOCK, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT>
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

		public virtual void InterpretGenesisBlockSnapshots(IGenesisBlock genesisBlock) {

			// first thing, lets add the moderator keys to our chainState. these are pretty important

			this.InterpretBlockSnapshots((BLOCK) genesisBlock, null);
		}

		public virtual void InterpretGenesisBlockLocalWallet(SynthesizedBlock synthesizedBlock, TaskRoutingContext taskRoutingContext) {

			// first thing, lets add the moderator keys to our chainState. these are pretty important

			this.InterpretBlockLocalWallet(synthesizedBlock, taskRoutingContext);
		}

		public void InterpretNewBlockSnapshots(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor) {

			if((block.BlockId.Value == 1) && block is IGenesisBlock genesisBlock) {
				this.InterpretGenesisBlockSnapshots(genesisBlock);
			} else {
				this.InterpretBlockSnapshots((BLOCK) block, serializationTransactionProcessor);
			}
		}

		public void InterpretNewBlockLocalWallet(SynthesizedBlock synthesizedBlock, TaskRoutingContext taskRoutingContext) {

			if(synthesizedBlock.BlockId == 1) {
				this.InterpretGenesisBlockLocalWallet(synthesizedBlock, taskRoutingContext);
			} else {
				this.InterpretBlockLocalWallet(synthesizedBlock, taskRoutingContext);
			}
		}

		/// <summary>
		///     Here we take a block a synthesize the transactions that concern our local accounts
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		public SynthesizedBlock SynthesizeBlock(IBlock block) {

			AccountCache accountCache = this.GetAccountCache();

			// get the transactions that concern us
			var blockConfirmedTransactions = block.GetAllConfirmedTransactions();

			return this.SynthesizeBlock(block, accountCache, blockConfirmedTransactions);
		}

		public void ProcessBlockImmediateGeneralImpact(IBlock block, SerializationTransactionProcessor serializationTransactionProcessor) {

			this.ProcessBlockImmediateGeneralImpact(block.BlockId, block.GetAllConfirmedTransactions().Values.ToList(), serializationTransactionProcessor);
		}

		public void ProcessBlockImmediateGeneralImpact(SynthesizedBlock synthesizedBlock, SerializationTransactionProcessor serializationTransactionProcessor) {

			this.ProcessBlockImmediateGeneralImpact(new BlockId(synthesizedBlock.BlockId), synthesizedBlock.ConfirmedGeneralTransactions.Values.ToList(), serializationTransactionProcessor);
		}

		/// <summary>
		///     determine any impact the block has on our general caches and files but NOT our personal wallet
		/// </summary>
		/// <param name="block"></param>
		public void ProcessBlockImmediateGeneralImpact(BlockId blockId, List<ITransaction> transactions, SerializationTransactionProcessor serializationTransactionProcessor) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// refresh our accounts list
			if(chainStateProvider.DiskBlockHeight < blockId.Value) {
				throw new ApplicationException($"Invalid disk block height value. Should be at least {blockId}.");
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone)) {
				// ok, the interpretation has been fully performed, we don't need to repeat it
				return;
			}
			

			var confirmedMasterTransactions = transactions.OfType<IMasterTransaction>().ToList();

			var keyedTransactionIds = confirmedMasterTransactions.Select(t => t.TransactionId).ToList();
			var confirmedTransactions = transactions.Where(t => !keyedTransactionIds.Contains(t.TransactionId)).ToList();
			

			// first thing, lets process any transaction that might affect our wallet directly
			foreach(IMasterTransaction trx in confirmedMasterTransactions) {
				this.HandleConfirmedMasterGeneralTransaction(trx);
			}

			foreach(ITransaction trx in confirmedTransactions) {
				this.HandleConfirmedGeneralTransaction(trx);
			}

			this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockInterpretationStatus |= ChainStateEntryFields.BlockInterpretationStatuses.ImmediateImpactDone;

		}

		/// <summary>
		///     determine any impact the block has on our general wallet
		/// </summary>
		/// <param name="block"></param>
		public void ProcessBlockImmediateAccountsImpact(SynthesizedBlock synthesizedBlock) {

			var walletActionSets = new Dictionary<AccountId, (List<Func<List<Action>>> walletActions, SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock)>();
			var serializationActions = new List<Action>();

			AccountCache accountCache = null;

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(provider => {
				accountCache = this.GetIncompleteAccountCache(provider, synthesizedBlock.BlockId, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed);

				if((accountCache == null) || !accountCache.combinedAccounts.Any()) {
					// ok, the interpretation has been fully performed, we don't need to repeat it
					return;
				}

				foreach(AccountId account in synthesizedBlock.Accounts) {

					var walletActions = new List<Func<List<Action>>>();
					SynthesizedBlock.SynthesizedBlockAccountSet scoppedSynthesizedBlock = synthesizedBlock.AccountScopped[account];

					var confirmedMasterTransactions = scoppedSynthesizedBlock.ConfirmedLocalTransactions.Select(t => t.Value).OfType<IMasterTransaction>().ToList();
					confirmedMasterTransactions.AddRange(scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Select(t => t.Value).OfType<IMasterTransaction>());

					var keyedTransactionIds = confirmedMasterTransactions.Select(t => t.TransactionId).ToList();
					var confirmedTransactions = scoppedSynthesizedBlock.ConfirmedLocalTransactions.Values.Where(t => !keyedTransactionIds.Contains(t.TransactionId)).ToList();
					confirmedTransactions.AddRange(scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Values.Where(t => !keyedTransactionIds.Contains(t.TransactionId)));

					// first, we check any election results
					foreach(SynthesizedBlock.SynthesizedElectionResult finalElectionResult in synthesizedBlock.FinalElectionResults) {

						if(finalElectionResult.ElectedAccounts.ContainsKey(account)) {
							walletActions.Add(() => {
								AccountId currentAccount = account;
								SynthesizedBlock.SynthesizedElectionResult synthesizedElectionResult = finalElectionResult;
								provider.InsertElectionsHistoryEntry(synthesizedElectionResult, currentAccount);

								return null;
							});
						}
					}

					// next thing, lets process any transaction that might affect our wallet directly
					int index = 0;

					foreach(IMasterTransaction trx in confirmedMasterTransactions) {
						this.HandleConfirmedMasterTransaction(synthesizedBlock.BlockId, trx, index, accountCache, walletActions, serializationActions);
						index++;
					}

					foreach(ITransaction trx in confirmedTransactions) {
						this.HandleConfirmedTransaction(synthesizedBlock.BlockId, trx, accountCache, walletActions);
					}

					foreach(RejectedTransaction trx in scoppedSynthesizedBlock.RejectedTransactions) {
						this.HandleRejectedTransaction(synthesizedBlock.BlockId, trx, accountCache, walletActions);
					}

					if(walletActions.Any()) {
						walletActionSets.Add(account, (walletActions, scoppedSynthesizedBlock));
					}
				}
			});

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {

				if(this.SetNewAccountsFlag(provider, synthesizedBlock.BlockId, WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed)) {
					provider.SaveWallet();
				}

				List<Action> transactionalSuccessActions = new List<Action>();

				foreach(var accountEntry in walletActionSets) {

					IndependentActionRunner.Run(() => {
						var walletActions = accountEntry.Value.walletActions;

						if(walletActions.Any()) {

							// run any wallet tasks we may have
							foreach(var action in walletActions.Where(a => a != null)) {
								
								var result = action();

								if(result != null) {
									transactionalSuccessActions.AddRange(result);
								}
							}

							provider.SaveWallet();
						}
					}, () => {

						// if there are any impacting transactions, let's add them now
						if(accountEntry.Value.scoppedSynthesizedBlock.ConfirmedExternalsTransactions.Any()) {

							var impactedTransactions = accountEntry.Value.scoppedSynthesizedBlock.ConfirmedExternalsTransactions.ToImmutableList();

							foreach(var entry in impactedTransactions) {
								provider.InsertTransactionHistoryEntry(entry.Value, null);
							}
						}

					});

				}

				// now mark the others that had no transactions

				foreach(var account in accountCache.combinedAccounts) {

					IAccountFileInfo accountEntry = provider.GetAccountFileInfo(provider.GetWalletAccount(account.Key).AccountUuid);

					if(!((WalletAccountChainState.BlockSyncStatuses) accountEntry.WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed)) {
						accountEntry.WalletChainStatesInfo.ChainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.WalletImmediateImpactPerformed;
					}
				}
				
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.AddTransactionSuccessActions(transactionalSuccessActions);
			});


			Repeater.Repeat(() => {
				if(serializationActions.Any()) {

					try {
						//TODO: should we use serialization transactions here too?
						this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, null);
						
					}catch(Exception ex) {
						//TODO: what do we do here?
						Log.Error(ex, "Failed to serialize");
					}
				}
			});
		}

		public abstract SynthesizedBlock CreateSynthesizedBlock();

		protected virtual ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_SNAPSHOT, STANDARD_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_SNAPSHOT, JOINT_WALLET_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateLocalTransactionInterpretationProcessor(List<IWalletAccount> accountsList) {
			// create two interpreters. one for our own transactions and one for the general snapshots
			var transactionInterpretationProcessor = this.CreateWalletInterpretationProcessor();

			transactionInterpretationProcessor.RequestStandardAccountSnapshots += accountId => {

				var standardAccounts = accountId.Where(a => a.AccountType == Enums.AccountTypes.Standard).ToList();
				var selectedAccounts = accountsList.Where(a => standardAccounts.Contains(a.GetAccountId())).ToList();

				var accountSnapshots = new Dictionary<AccountId, STANDARD_WALLET_ACCOUNT_SNAPSHOT>();

				foreach(IWalletAccount account in selectedAccounts) {

					if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountUuid) is STANDARD_WALLET_ACCOUNT_SNAPSHOT entry) {
						accountSnapshots.Add(account.GetAccountId(), entry);
					}
				}

				return accountSnapshots;
			};

			transactionInterpretationProcessor.RequestJointAccountSnapshots += accountId => {

				var jointAccounts = accountId.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList();
				var selectedAccounts = accountsList.Where(a => jointAccounts.Contains(a.GetAccountId())).ToList();

				var accountSnapshots = new Dictionary<AccountId, JOINT_WALLET_ACCOUNT_SNAPSHOT>();

				foreach(IWalletAccount account in selectedAccounts) {
					if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletFileInfoAccountSnapshot(account.AccountUuid) is JOINT_WALLET_ACCOUNT_SNAPSHOT entry) {
						accountSnapshots.Add(account.GetAccountId(), entry);
					}
				}

				return accountSnapshots;
			};

			transactionInterpretationProcessor.RequestCreateNewStandardAccountSnapshot += () => (STANDARD_WALLET_ACCOUNT_SNAPSHOT) this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry();
			transactionInterpretationProcessor.RequestCreateNewJointAccountSnapshot += () => (JOINT_WALLET_ACCOUNT_SNAPSHOT) this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshotEntry();

			transactionInterpretationProcessor.EnableLocalMode(true);

			transactionInterpretationProcessor.AccountInfluencingTransactionFound += (isOwn, impactedLocalPublishedAccounts, impactedLocalDispatchedAccounts, transaction) => {
				// alert when a transaction concerns us

				var impactedLocalPublishedAccountsUuids = accountsList.Where(a => impactedLocalPublishedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountUuid).ToList();
				var impactedLocalDispatchedAccountsUuids = accountsList.Where(a => impactedLocalDispatchedAccounts.Contains(a.GetAccountId())).Select(a => a.AccountUuid).ToList();

				if(!isOwn) {
					// this is a foreign transaction that is targetting us. let's add it to our wallet
					this.centralCoordinator.ChainComponentProvider.WalletProviderBase.InsertTransactionHistoryEntry(transaction, "");
				}

				this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionReceived(impactedLocalPublishedAccounts, impactedLocalPublishedAccountsUuids, impactedLocalDispatchedAccounts, impactedLocalDispatchedAccountsUuids, transaction.TransactionId));
			};

			// we dont store keys in our wallet snapshots as we already have them in the wallet itself.
			transactionInterpretationProcessor.IsAnyAccountKeysTracked = (ids, accounts) => false;

			return transactionInterpretationProcessor;
		}

		protected virtual ITransactionInterpretationProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> CreateSnapshotsTransactionInterpretationProcessor() {
			// create two interpreters. one for our own transactions and one for the general snapshots
			var transactionInterpretationProcessor = this.CreateInterpretationProcessor();

			transactionInterpretationProcessor.IsAnyAccountTracked = accountIds => this.AccountSnapshotsProvider.AnyAccountTracked(accountIds);
			transactionInterpretationProcessor.GetTrackedAccounts = accountIds => this.AccountSnapshotsProvider.AccountsTracked(accountIds);

			// if we use fast keys, we track all keys. otherwise, whatever account we track
			transactionInterpretationProcessor.IsAnyAccountKeysTracked = (ids, accounts) => {

				BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				return this.AccountSnapshotsProvider.AnyAccountTracked(accounts);
			};

			// we track them all
			transactionInterpretationProcessor.IsAnyAccreditationCertificateTracked = ids => true;
			transactionInterpretationProcessor.IsAnyChainOptionTracked = ids => true;

			transactionInterpretationProcessor.RequestStandardAccountSnapshots += accountIds => {

				var standardAccounts = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Standard).ToList();
				var trackedAccounts = new List<AccountId>();

				foreach(AccountId accountId in standardAccounts) {
					if(this.AccountSnapshotsProvider.IsAccountTracked(accountId)) {
						trackedAccounts.Add(accountId);
					}
				}

				return this.AccountSnapshotsProvider.LoadAccountSnapshots(trackedAccounts).ToDictionary(a => a.AccountId.ToAccountId(), a => (STANDARD_ACCOUNT_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestJointAccountSnapshots += accountIds => {

				var jointAccounts = accountIds.Where(a => a.AccountType == Enums.AccountTypes.Joint).ToList();
				var trackedAccounts = new List<AccountId>();

				foreach(AccountId accountId in jointAccounts) {
					if(this.AccountSnapshotsProvider.IsAccountTracked(accountId)) {
						trackedAccounts.Add(accountId);
					}
				}

				return this.AccountSnapshotsProvider.LoadAccountSnapshots(trackedAccounts).ToDictionary(a => a.AccountId.ToAccountId(), a => (JOINT_ACCOUNT_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestStandardAccountKeySnapshots += keys => {
				var trackedKeys = new List<(long accountId, byte ordinal)>();

				BlockChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

				foreach((long AccountId, byte OrdinalId) key in keys) {
					if(configuration.EnableFastKeyIndex || this.AccountSnapshotsProvider.IsAccountTracked(new AccountId(key.AccountId, Enums.AccountTypes.Standard))) {
						trackedKeys.Add(key);
					}
				}

				return this.AccountSnapshotsProvider.LoadStandardAccountKeysSnapshots(trackedKeys).ToDictionary(a => (a.AccountId, a.OrdinalId), a => (STANDARD_ACCOUNT_KEY_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestAccreditationCertificateSnapshots += certificateIds => {

				return this.AccountSnapshotsProvider.LoadAccreditationCertificatesSnapshots(certificateIds).ToDictionary(a => a.CertificateId, a => (ACCREDITATION_CERTIFICATE_SNAPSHOT) a);
			};

			transactionInterpretationProcessor.RequestChainOptionSnapshots += ids => {
				return this.AccountSnapshotsProvider.LoadChainOptionsSnapshots(ids).ToDictionary(a => a.Id, a => (CHAIN_OPTIONS_SNAPSHOT) a);

			};

			transactionInterpretationProcessor.RequestCreateNewStandardAccountSnapshot += () => {

				return (STANDARD_ACCOUNT_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewStandardAccountSnapshots();
			};

			transactionInterpretationProcessor.RequestCreateNewJointAccountSnapshot += () => {

				return (JOINT_ACCOUNT_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewJointAccountSnapshots();
			};

			transactionInterpretationProcessor.RequestCreateNewAccountKeySnapshot += () => {
				return (STANDARD_ACCOUNT_KEY_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewAccountKeySnapshots();

			};

			transactionInterpretationProcessor.RequestCreateNewAccreditationCertificateSnapshot += () => {

				return (ACCREDITATION_CERTIFICATE_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewAccreditationCertificateSnapshots();
			};

			transactionInterpretationProcessor.RequestCreateNewChainOptionSnapshot += () => {

				return (CHAIN_OPTIONS_SNAPSHOT) this.AccountSnapshotsProvider.CreateNewChainOptionsSnapshots();
			};

			return transactionInterpretationProcessor;
		}

		protected void InsertChangedLocalAccountsEvent(Dictionary<AccountId, List<Action>> changedLocalAccounts, AccountId accountId, Action operation) {
			if(!changedLocalAccounts.ContainsKey(accountId)) {
				changedLocalAccounts.Add(accountId, new List<Action>());
			}

			changedLocalAccounts[accountId].Add(operation);
		}

		protected IWalletAccount IsLocalAccount(ImmutableDictionary<AccountId, IWalletAccount> account, AccountId accountId) {
			return account.ContainsKey(accountId) ? account[accountId] : null;
		}

		protected virtual SynthesizedBlock SynthesizeBlock(IBlock block, AccountCache accountCache, Dictionary<TransactionId, ITransaction> blockConfirmedTransactions) {
			SynthesizedBlock synthesizedBlock = this.CreateSynthesizedBlock();

			synthesizedBlock.BlockId = block.BlockId.Value;

			var localTransactionInterpretationProcessor = this.CreateLocalTransactionInterpretationProcessor(accountCache.combinedAccounts.Values.ToList());

			localTransactionInterpretationProcessor.IsAnyAccountTracked = accountIds => accountCache.combinedAccountsList.Any(accountIds.Contains);
			localTransactionInterpretationProcessor.GetTrackedAccounts = accountIds => accountCache.combinedAccountsList.Where(accountIds.Contains).ToList();
			localTransactionInterpretationProcessor.SetLocalAccounts(accountCache.publishedAccountsList, accountCache.dispatchedAccountsList);

			List<ITransaction> confirmedLocalTransactions = null;
			List<(ITransaction transaction, AccountId targetAccount)> confirmedExternalsTransactions = null;
			Dictionary<AccountId, List<TransactionId>> accountsTransactions = null;
			(confirmedLocalTransactions, confirmedExternalsTransactions, accountsTransactions) = localTransactionInterpretationProcessor.GetImpactingTransactionsList(blockConfirmedTransactions.Values.ToList());

			foreach(AccountId account in accountCache.combinedAccountsList) {

				SynthesizedBlock.SynthesizedBlockAccountSet synthesizedBlockAccountSet = new SynthesizedBlock.SynthesizedBlockAccountSet();

				synthesizedBlockAccountSet.ConfirmedLocalTransactions = confirmedLocalTransactions.Where(t => t.TransactionId.Account == account).ToDictionary(e => e.TransactionId, e => e);
				synthesizedBlockAccountSet.ConfirmedExternalsTransactions = confirmedExternalsTransactions.Where(t => t.targetAccount == account).ToDictionary(e => e.transaction.TransactionId, e => e.transaction);

				foreach(var transaction in synthesizedBlockAccountSet.ConfirmedLocalTransactions) {
					if(!synthesizedBlockAccountSet.ConfirmedTransactions.ContainsKey(transaction.Key)) {
						synthesizedBlockAccountSet.ConfirmedTransactions.Add(transaction.Key, transaction.Value);
					}
				}

				foreach(var transaction in synthesizedBlockAccountSet.ConfirmedExternalsTransactions) {
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

			var allTransactions = synthesizedBlock.ConfirmedTransactions.Keys.ToList();
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

		protected List<IWalletAccount> GetIncompleteAccountList(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter) {
			return walletProvider.GetWalletSyncableAccounts(blockSyncHeight);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected AccountCache GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter) {

			var accountsList = this.GetIncompleteAccountList(walletProvider, blockSyncHeight, flagFilter);

			return this.GetIncompleteAccountCache(walletProvider, accountsList, flagFilter);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected AccountCache GetIncompleteAccountCache(IWalletProvider walletProvider, long blockSyncHeight, WalletAccountChainState.BlockSyncStatuses flagFilter, IWalletAccount filterAccount) {

			var accountsList = this.GetIncompleteAccountList(walletProvider, blockSyncHeight, flagFilter).Where(a => a.AccountUuid == filterAccount.AccountUuid).ToList();

			return this.GetIncompleteAccountCache(walletProvider, accountsList, flagFilter);
		}

		/// <summary>
		///     Pick all the accounts for a given block height which do not have a certain flag set
		/// </summary>
		/// <param name="blockSyncHeight"></param>
		/// <param name="flagFilter"></param>
		/// <returns></returns>
		protected AccountCache GetIncompleteAccountCache(IWalletProvider walletProvider, List<IWalletAccount> accountsList, WalletAccountChainState.BlockSyncStatuses flagFilter) {

			accountsList = accountsList.Where(a => {

				return !((WalletAccountChainState.BlockSyncStatuses) walletProvider.GetAccountFileInfo(a.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(flagFilter);
			}).ToList();

			return this.PrepareAccountCache(accountsList);
		}

		/// <summary>
		///     this method will return our local accounts in different forms and combinations
		/// </summary>
		/// <returns></returns>
		protected AccountCache GetAccountCache(long? blockSyncHeight = null) {

			var accountsList = blockSyncHeight.HasValue ? this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletSyncableAccounts(blockSyncHeight.Value) : this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccounts();

			return this.PrepareAccountCache(accountsList);
		}

		/// <summary>
		///     Set a status flag to all New accounts
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="statusFlag"></param>
		protected bool SetNewAccountsFlag(IWalletProvider provider, long blockId, WalletAccountChainState.BlockSyncStatuses statusFlag) {
			bool changed = false;

			foreach(IWalletAccount account in provider.GetWalletSyncableAccounts(blockId).Where(a => a.Status == Enums.PublicationStatus.New)) {
				if(!((WalletAccountChainState.BlockSyncStatuses) provider.GetAccountFileInfo(account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(statusFlag)) {
					provider.GetAccountFileInfo(account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus |= (int) statusFlag;
					changed = true;
				}
			}

			return changed;
		}

		protected AccountCache PrepareAccountCache(List<IWalletAccount> accountsList) {
			AccountCache accountCache = new AccountCache();
			accountCache.publishedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Published).ToImmutableDictionary(a => a.PublicAccountId, a => a);
			accountCache.dispatchedAccounts = accountsList.Where(a => a.Status == Enums.PublicationStatus.Dispatched).ToImmutableDictionary(a => a.AccountUuidHash, a => a);

			var combinedTemp = accountCache.publishedAccounts.ToDictionary();

			foreach(var e in accountCache.dispatchedAccounts) {
				combinedTemp.Add(e.Key, e.Value);
			}

			accountCache.combinedAccounts = combinedTemp.ToImmutableDictionary(e => e.Key, e => e.Value);

			accountCache.publishedAccountsList = accountCache.publishedAccounts.Keys.ToImmutableList();
			accountCache.dispatchedAccountsList = accountCache.dispatchedAccounts.Keys.ToImmutableList();

			var combinedAccountsList = accountCache.publishedAccountsList.ToList();
			combinedAccountsList.AddRange(accountCache.dispatchedAccountsList);
			accountCache.combinedAccountsList = combinedAccountsList.ToImmutableList();

			return accountCache;
		}

		/// <summary>
		///     Receive and post a newly accepted transaction for processing
		/// </summary>
		/// <param name="transaction"></param>
		protected virtual void InterpretBlockSnapshots(BLOCK block, SerializationTransactionProcessor serializationTransactionProcessor) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			// refresh our accounts list
			if(chainStateProvider.DiskBlockHeight < block.BlockId.Value) {
				throw new ApplicationException($"Invalid disk block height value. Should be at least {block.BlockId}.");
			}

			if(chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.FullSnapshotInterpretationCompleted)) {
				// ok, the interpretation has been fully performed, we don't need to repeat it

				return;
			}

			var serializationActions = new List<Action>();

			var snapshotsTransactionInterpretationProcessor = this.CreateSnapshotsTransactionInterpretationProcessor();

			// now lets process the snapshots
			snapshotsTransactionInterpretationProcessor.Reset();

			var confirmedTransactions = block.GetAllConfirmedTransactions();

			// now the transactions
			
			snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedMasterTransactions.Cast<ITransaction>().ToList(), block.BlockId.Value);

			// now just the published accounts
			snapshotsTransactionInterpretationProcessor.InterpretTransactions(block.ConfirmedTransactions, block.BlockId.Value);

			// and finally, the elections
			snapshotsTransactionInterpretationProcessor.ApplyBlockElectionsInfluence(block.FinalElectionResults, confirmedTransactions);

			SnapshotHistoryStackSet<STANDARD_ACCOUNT_SNAPSHOT, STANDARD_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_SNAPSHOT, JOINT_ACCOUNT_ATTRIBUTE_SNAPSHOT, JOINT_ACCOUNT_MEMBERS_SNAPSHOT, STANDARD_ACCOUNT_KEY_SNAPSHOT, ACCREDITATION_CERTIFICATE_SNAPSHOT, ACCREDITATION_CERTIFICATE_ACCOUNT_SNAPSHOT, CHAIN_OPTIONS_SNAPSHOT> snapshotsModificationHistoryStack = null;

			bool interpretationSerializationDone = chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.InterpretationSerializationDone);
			bool snapshotInterpretationDone = chainStateProvider.BlockInterpretationStatus.HasFlag(ChainStateEntryFields.BlockInterpretationStatuses.SnapshotInterpretationDone);

			if(!interpretationSerializationDone || !snapshotInterpretationDone) {

				snapshotsModificationHistoryStack = snapshotsTransactionInterpretationProcessor.GetEntriesModificationStack();
			}

			// ok, commit the impacts of this interpretation

			if(!interpretationSerializationDone) {

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {
					var fastKeys = snapshotsTransactionInterpretationProcessor.GetImpactedFastKeys();

					if(fastKeys?.Any() ?? false) {
						serializationActions.AddRange(this.AccountSnapshotsProvider.PrepareKeysSerializationTasks(fastKeys));
					}
				}

				if(serializationActions.Any()) {

					try {
						if(serializationActions.Any()) {

							//throw new OutOfMemoryException();
							this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.RunTransactionalActions(serializationActions, serializationTransactionProcessor);
						}
					} catch(Exception ex) {
						//TODO: what do we do here?
						Log.Error(ex, "Failed to perform serializations during block interpretation");
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
					Log.Error(ex, "Failed to serialize");
				}

			}

			if(!snapshotInterpretationDone) {

				// this is the very last step in block insertion. succeed here, and we are good to go
				if(snapshotsModificationHistoryStack?.Any() ?? false) {
					this.AccountSnapshotsProvider.ProcessSnapshotImpacts(snapshotsModificationHistoryStack);
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
		protected virtual void InterpretBlockLocalWallet(SynthesizedBlock synthesizedBlock, TaskRoutingContext taskRoutingContext) {

			if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsWalletLoaded) {
				// ok, the interpretation has been fully performed, we don't need to repeat it
				Log.Information("Wallet is not loaded. cannot interpret block.");

				return;
			}

			List<IWalletAccount> incompleteAccounts;
			var incompleteAccountCaches = new List<(AccountCache accountCache, IWalletAccount account)>();

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleWrite(prov => {

				// new accounts can be updated systematically, they dont interpret anything
				if(this.SetNewAccountsFlag(prov, synthesizedBlock.BlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted)) {
					prov.SaveWallet();
				}
			});

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleRead(provider => {

				incompleteAccounts = this.GetIncompleteAccountList(provider, synthesizedBlock.BlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted);

				foreach(IWalletAccount account in incompleteAccounts) {
					AccountCache accountCache = this.GetIncompleteAccountCache(provider, synthesizedBlock.BlockId, WalletAccountChainState.BlockSyncStatuses.InterpretationCompleted, account);

					if(!accountCache.combinedAccountsList.Any()) {
						continue;
					}

					incompleteAccountCaches.Add((accountCache, account));
				}
			});

			// lets perform interpretation before we create a transaction
			var accountActions = new List<Action>();

			var modificationHistoryStacks = new Dictionary<AccountId, (ISnapshotHistoryStackSet snapshots, IWalletAccount account, AccountCache accountCache, Dictionary<AccountId, List<Action>> changedLocalAccounts)>();

			foreach((AccountCache accountCache, IWalletAccount account) accountEntry in incompleteAccountCaches) {

				var localTransactionInterpretationProcessor = this.CreateLocalTransactionInterpretationProcessor(accountEntry.accountCache.combinedAccounts.Values.ToList());
				AccountId currentAccountId = accountEntry.account.GetAccountId();

				if(!((WalletAccountChainState.BlockSyncStatuses) this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(accountEntry.account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

					// all our accounts are tracked here
					localTransactionInterpretationProcessor.IsAnyAccountTracked = accountIds => accountIds.Contains(currentAccountId);
					localTransactionInterpretationProcessor.GetTrackedAccounts = accountIds => new[] {currentAccountId}.ToList();

					var publishedAccountsList = accountEntry.accountCache.publishedAccountsList.Where(a => a == currentAccountId).ToImmutableList();
					var dispatchedAccountsList = accountEntry.accountCache.dispatchedAccountsList.Where(a => a == currentAccountId).ToImmutableList();

					var changedLocalAccounts = new Dictionary<AccountId, List<Action>>();

					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList, dispatchedAccountsList);

					var keyedTransactions = synthesizedBlock.ConfirmedTransactions.Values.OfType<IMasterTransaction>().Cast<ITransaction>().ToList();
					localTransactionInterpretationProcessor.InterpretTransactions(keyedTransactions, synthesizedBlock.BlockId);

					// now just the published accounts
					localTransactionInterpretationProcessor.IsAnyAccountTracked = accountIds => publishedAccountsList.Any(accountIds.Contains);
					localTransactionInterpretationProcessor.GetTrackedAccounts = accountIds => publishedAccountsList.Where(accountIds.Contains).ToList();
					localTransactionInterpretationProcessor.SetLocalAccounts(publishedAccountsList);

					var confirmedRegularTransactions = synthesizedBlock.ConfirmedTransactions.Values.Where(t => !(t is IMasterTransaction)).ToList();

					localTransactionInterpretationProcessor.InterpretTransactions(confirmedRegularTransactions, synthesizedBlock.BlockId);

					// and finally the elections
					localTransactionInterpretationProcessor.ApplyBlockElectionsInfluence(synthesizedBlock.FinalElectionResults, confirmedRegularTransactions.ToDictionary(t => t.TransactionId, t => t));

					var localModificationHistoryStack = localTransactionInterpretationProcessor.GetEntriesModificationStack();

					accountActions.Add(() => {

						// ok, commit the impacts of this interpretation
						IndependentActionRunner.Run(() => {

							if(localModificationHistoryStack.Any()) {
								// now lets process the results. first, anything impacting our wallet

								// here we update our wallet snapshots

								var operations = localModificationHistoryStack.CompileStandardAccountHistorySets<DbContext>((db, accountId, temporaryHashId, entry) => {
									// it may have been created in the local wallet transactions
									if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetStandardAccountSnapshot(accountId) == null) {
										IWalletStandardAccountSnapshot accountSnapshot = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshotEntry();
										this.CardUtils.Copy(entry, accountSnapshot);
										this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(accountEntry.accountCache.combinedAccounts[temporaryHashId], accountSnapshot);
									}

									return null;
								}, (db, accountId, entry) => {

									this.LocalAccountSnapshotEntryChanged(changedLocalAccounts, entry, this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountSnapshot(accountId));

									this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountEntry.accountCache.combinedAccounts[accountId].AccountUuid);

									return null;
								}, (db, accountId) => {
									//do we do anything here?  we dont really delete accounts
									//TODO: delete an account in the wallet?
									return null;
								});

								// run the operations
								foreach(var operation in operations.SelectMany(e => e.Value)) {
									operation(null);
								}

								//-------------------------------------------------------------------------------------------
								operations = localModificationHistoryStack.CompileJointAccountHistorySets<DbContext>((db, accountId, temporaryHashId, entry) => {
									// it may have been created in the local wallet transactions
									if(this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetJointAccountSnapshot(accountId) == null) {
										IWalletJointAccountSnapshot accountSnapshot = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshotEntry();
										this.CardUtils.Copy(entry, accountSnapshot);
										this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshot(accountEntry.accountCache.combinedAccounts[temporaryHashId], accountSnapshot);
									}

									return null;
								}, (db, accountId, entry) => {

									this.LocalAccountSnapshotEntryChanged(changedLocalAccounts, entry, this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountSnapshot(accountId));

									this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(entry, accountEntry.accountCache.combinedAccounts[accountId].AccountUuid);

									return null;
								}, (db, accountId) => {
									//do we do anything here?  we dont really delete accounts
									//TODO: delete an account in the wallet?
									return null;
								});

								// run the operations
								foreach(var operation in operations.SelectMany(e => e.Value)) {
									operation(null);
								}

								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet();

								// fire any extra events
								foreach(var entry in changedLocalAccounts) {
									foreach(Action operation in entry.Value) {
										operation?.Invoke();
									}
								}

								// now, alert the world of this new block!
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(accountEntry.account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
							}
						}, () => {

							if(!((WalletAccountChainState.BlockSyncStatuses) this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(accountEntry.account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone)) {

								if(localModificationHistoryStack?.Any() ?? false) {
									this.AccountSnapshotsProvider.ProcessSnapshotImpacts(localModificationHistoryStack);
								}

								// now, alert the world of this new block!
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountFileInfo(accountEntry.account.AccountUuid).WalletChainStatesInfo.ChainState.BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.SnapshotInterpretationDone;
							}
						});

					});

					localTransactionInterpretationProcessor.ClearLocalAccounts();
				}
			}

			if(accountActions.Any()) {
				this.centralCoordinator.ChainComponentProvider.WalletProviderBase.ScheduleTransaction((provider, token) => {

					// run for all accounts, try to get as much done as possible before we break for exceptions
					IndependentActionRunner.Run(accountActions.ToArray());
				});
			}

		}

		protected virtual void LocalAccountSnapshotEntryChanged(Dictionary<AccountId, List<Action>> changedLocalAccounts, IAccountSnapshot newEntry, IWalletAccountSnapshot original) {

		}

		protected void HandleConfirmedMasterGeneralTransaction(IMasterTransaction transaction) {
			if(transaction is IModerationMasterTransaction moderationMasterTransaction) {
				this.HandleModerationMasterGeneralImpactTransaction(moderationMasterTransaction);
			}
		}

		protected void HandleConfirmedMasterTransaction(long BlockId, IMasterTransaction transaction, int keyedTransactionIndex, AccountCache accountCache, List<Func<List<Action>>> walletActions, List<Action> serializationActions) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {
				if(transaction is IStandardPresentationTransaction presentationTransaction) {

					this.AddConfirmedTransaction(transaction.TransactionId, walletActions);

					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedStandardPresentationTransaction(BlockId, presentationTransaction, keyedTransactionIndex, dispatchedAccount, walletActions);
				} else if(transaction is IJointPresentationTransaction jointPresentationTransaction) {
					// ok, this is a very special case, its our presentation confirmation :D
					this.ProcessLocalConfirmedJointPresentationTransaction(BlockId, jointPresentationTransaction, dispatchedAccount, walletActions);
				} else {
					//TODO: what to do?
					throw new ApplicationException("A dispatched transaction can only be a presentation one!");
				}
			}

			if(transaction is IModerationMasterTransaction moderationMasterTransaction) {
				this.HandleModerationMasterLocalImpactTransaction(BlockId, moderationMasterTransaction, accountCache, walletActions, serializationActions);
			} else {
				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction.TransactionId, walletActions);

					if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
						this.ProcessOwnConfirmedKeyChangeTransaction(BlockId, keyChangeTransaction, keyedTransactionIndex, publishedAccount, walletActions);
					}
				}
			}
		}

		private void HandleModerationMasterGeneralImpactTransaction(IModerationMasterTransaction moderationMasterTransaction) {

			if(moderationMasterTransaction is IGenesisModeratorAccountPresentationTransaction genesisModeratorAccountPresentationTransaction) {
				this.HandleGenesisModeratorAccountTransaction(genesisModeratorAccountPresentationTransaction);
			} else if(moderationMasterTransaction is IModeratorKeyChangeTransaction moderatorKeyChangeTransaction) {
				this.HandleModeratorKeyChangeTransaction(moderatorKeyChangeTransaction);
			}
		}

		private void HandleModerationMasterLocalImpactTransaction(long BlockId, IModerationMasterTransaction moderationMasterTransaction, AccountCache accountCache, List<Func<List<Action>>> walletActions, List<Action> serializationActions) {

			if(moderationMasterTransaction is IAccountResetTransaction accountResetTransaction) {

				// check if it concerns us
				if(accountCache.publishedAccounts.ContainsKey(accountResetTransaction.Account)) {

					// ok, thats us!
					this.ProcessLocalConfirmedAccountResetTransaction(BlockId, accountResetTransaction, accountCache.publishedAccounts[accountResetTransaction.Account], walletActions);
				}
			}
		}

		protected void HandleConfirmedGeneralTransaction(ITransaction transaction) {
			if(transaction is IModerationTransaction moderationTransaction) {
				this.HandleModerationGeneralImpactTransaction(moderationTransaction);
			}
		}

		protected void HandleConfirmedTransaction(long BlockId, ITransaction transaction, AccountCache accountCache, List<Func<List<Action>>> walletActions) {

			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, transaction.TransactionId.Account);

			if(dispatchedAccount != null) {

				this.AddConfirmedTransaction(transaction.TransactionId, walletActions);

				
			}

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, transaction.TransactionId.Account);

			if(transaction is IModerationTransaction moderationTransaction) {
				this.HandleModerationLocalImpactTransaction(BlockId, moderationTransaction, accountCache, walletActions);
			} else {

				if(publishedAccount != null) {

					this.AddConfirmedTransaction(transaction.TransactionId, walletActions);

					if(transaction is ISetAccountRecoveryTransaction setAccountRecoveryTransaction) {
						this.ProcessLocalConfirmedSetAccountRecoveryTransaction(BlockId, setAccountRecoveryTransaction, publishedAccount, walletActions);
					}

					if(transaction is ISetAccountCorrelationIdTransaction setAccountCorrelationIdTransaction) {
						this.ProcessLocalConfirmedSetAccountCorrelationIdTransaction(BlockId, setAccountCorrelationIdTransaction, publishedAccount, walletActions);
					}
				}
			}
		}

		private void HandleModerationGeneralImpactTransaction(IModerationTransaction moderationTransaction) {
			if(moderationTransaction is IChainOperatingRulesTransaction chainOperatingRulesTransaction) {
				this.HandleChainOperatingRulesTransaction(chainOperatingRulesTransaction);
			}
		}

		private void HandleModerationLocalImpactTransaction(long BlockId, IModerationTransaction moderationTransaction, AccountCache accountCache, List<Func<List<Action>>> walletActions) {

			if(moderationTransaction is IAccountResetWarningTransaction accountResetWarningTransaction) {
				if(accountCache.publishedAccounts.ContainsKey(accountResetWarningTransaction.Account)) {

					// ok, thats us!
					//TODO: we ned to raise an alert about this!!!  we are about to be reset
				}
			}

			if(moderationTransaction is IReclaimAccountsTransaction reclaimAccountsTransaction) {

				// check if it concerns us
				var resetAccounts = reclaimAccountsTransaction.Accounts.Select(a => a.Account).ToImmutableList();

				var ourResetAccounts = accountCache.publishedAccountsList.Where(a => resetAccounts.Contains(a)).ToImmutableList();

				if(ourResetAccounts.Any()) {

					// ok, thats us, we are begin reset!!
					//TODO: what do we do here??
				}
			}
		}

		protected void HandleRejectedTransaction(long BlockId, RejectedTransaction trx, AccountCache accountCache, List<Func<List<Action>>> walletActions) {

			IWalletAccount publishedAccount = this.IsLocalAccount(accountCache.publishedAccounts, trx.TransactionId.Account);
			IWalletAccount dispatchedAccount = this.IsLocalAccount(accountCache.dispatchedAccounts, trx.TransactionId.Account);

			if((publishedAccount != null) && (dispatchedAccount != null)) {
				//TODO: what to do?
				throw new ApplicationException("This should never happen!");
			}

			if(dispatchedAccount != null) {

				// handle our failed publication
				this.ProcessLocalRejectedPresentationTransaction(BlockId, trx, dispatchedAccount, walletActions);

				this.AddRejectedTransaction(trx.TransactionId, walletActions);
			} else if(publishedAccount != null) {

				this.AddRejectedTransaction(trx.TransactionId, walletActions);
			}
		}

		protected void AddRejectedTransaction(TransactionId transactionId, List<Func<List<Action>>> walletActions) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionRefused(transactionId));

			walletActions.Add(() => {
				Repeater.Repeat(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemoveLocalTransactionCacheEntry(transactionId));
				Repeater.Repeat(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transactionId, WalletTransactionHistory.TransactionStatuses.Rejected));
				
				return null;
			});
		}

		protected void AddConfirmedTransaction(TransactionId transactionId, List<Func<List<Action>>> walletActions) {
			this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.TransactionConfirmed(transactionId));

			walletActions.Add(() => {

				Repeater.Repeat(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.RemoveLocalTransactionCacheEntry(transactionId));
				Repeater.Repeat(() => this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalTransactionHistoryEntry(transactionId, WalletTransactionHistory.TransactionStatuses.Confirmed));
				
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

		protected virtual void ProcessLocalConfirmedStandardPresentationTransaction<T>(long BlockId, T trx, int keyedTransactionIndex, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : IStandardPresentationTransaction {

			List<Action> Operation() {

				Log.Verbose($"We just received confirmation that our presentation for simple account {account.AccountUuid} with temporary hash {account.AccountUuidHash} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;

				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				account.ConfirmationBlockId = BlockId;

				List<Action> successCalls = new List<Action>();
				successCalls.Add(() => {
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountUuid, true, account.PublicAccountId.ToString()},  new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] {},  new CorrelationContext());

				});

				//this gives us the transaction's offsets for the keyaddress
				foreach(var confirmedKey in trx.Keyset.Keys) {

					using(IWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountUuid, confirmedKey.Key)) {

						key.Status = Enums.KeyStatus.Ok;

						// store the address of our key inside the block

						key.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;

						key.KeyAddress.AccountId = trx.AssignedAccountId;
						key.KeyAddress.AnnouncementBlockId = BlockId;
						key.AnnouncementBlockId = BlockId;

						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);
					}
				}

				// anything to do with the keys here?

				foreach(KeyInfo keyInfo in account.Keys) {

				}

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountUuid, BlockId, WalletAccountChainState.BlockSyncStatuses.FullySynced);

				// now we create our account snap shot, we will need it forward on.
				IWalletStandardAccountSnapshot newSnapshot = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletStandardAccountSnapshot(account);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = BlockId;
				newSnapshot.CorrelationId = trx.CorrelationId;

				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot);

				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessLocalConfirmedJointPresentationTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : IJointPresentationTransaction {
			
			List<Action> Operation() {
				
				Log.Verbose($"We just received confirmation that our presentation for joint account {account.AccountUuid} with temporary hash {account.AccountUuidHash} has been accepted. Our new encoded public account Id is '{trx.AssignedAccountId}'");

				// thats it, this account is now valid. lets take the required information :)
				account.Status = Enums.PublicationStatus.Published;
				account.PresentationTransactionTimeout = null;
				
				// we got our new publicly recognized account id. lets set it
				account.PublicAccountId = new AccountId(trx.AssignedAccountId);

				
				
				//TODO: presentation
				List<Action> successCalls = new List<Action>();
				successCalls.Add(() => {
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.AccountPublicationEnded, new object[] {account.AccountUuid, true, account.PublicAccountId.ToString()},  new CorrelationContext());
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] {},  new CorrelationContext());

				});

				// now we create our account snap shot, we will need it forward on.
				JOINT_WALLET_ACCOUNT_SNAPSHOT newSnapshot = (JOINT_WALLET_ACCOUNT_SNAPSHOT) this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateNewWalletJointAccountSnapshot(account);

				newSnapshot.AccountId = trx.AssignedAccountId.ToLongRepresentation();
				newSnapshot.InceptionBlockId = BlockId;
				newSnapshot.CorrelationId = trx.CorrelationId;

				foreach(ITransactionJointAccountMember entry in trx.MemberAccounts) {
					JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT newAccount = new JOINT_WALLET_ACCOUNT_MEMBERS_SNAPSHOT();

					this.CardUtils.Copy(entry, newAccount);

					newSnapshot.MemberAccounts.Add(newAccount);
				}

				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletSnapshot(newSnapshot);

				// now lets mark our new account as fully synced up to this point, since it just comes into existance
				this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateWalletChainStateSyncStatus(account.AccountUuid, BlockId, WalletAccountChainState.BlockSyncStatuses.FullySynced);

				return successCalls;
			}

			walletActions.Add(Operation);
		}

		protected virtual void ProcessOwnConfirmedKeyChangeTransaction<T>(long BlockId, T keyChangeTrx, int keyedTransactionIndex, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : IStandardAccountKeyChangeTransaction {

			// its our own
			if(account.Status != Enums.PublicationStatus.Published) {
				throw new ApplicationException($"We can only confirm transactions for an account that has been published. current account status '{account.Status}' is invalid.");
			}

			string keyName = account.Keys.Single(k => k.Ordinal == keyChangeTrx.NewCryptographicKey.Id).Name;

			List<Action> Operation() {

				// swap the changed key
				using(IWalletKey nextKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountUuid, keyName)) {

					nextKey.Status = Enums.KeyStatus.Ok;

					// store the address of our key inside the block
					nextKey.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;
					nextKey.KeyAddress.AccountId = account.GetAccountId();
					nextKey.KeyAddress.AnnouncementBlockId = BlockId;
					nextKey.AnnouncementBlockId = BlockId;

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(nextKey);
					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SwapNextKey(nextKey);

					Log.Information($"Key named {nextKey.Name} is confirmed as changed.");
				}

				if(keyChangeTrx.IsChangingChangeKey) {
					// we must also swap the super key
					using(IWalletKey nextKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountUuid, GlobalsService.SUPER_KEY_NAME)) {

						nextKey.Status = Enums.KeyStatus.Ok;

						// store the address of our key inside the block
						nextKey.KeyAddress.MasterTransactionIndex = keyedTransactionIndex;
						nextKey.KeyAddress.AccountId = account.GetAccountId();
						nextKey.KeyAddress.AnnouncementBlockId = BlockId;
						nextKey.AnnouncementBlockId = BlockId;

						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(nextKey);
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SwapNextKey(nextKey);

						Log.Information("Super Key is also confirmed as changed.");
					}
				}
				
				List<Action> successCalls = new List<Action>();
				successCalls.Add(() => {

					// alert important change
					this.CentralCoordinator.PostSystemEvent(BlockchainSystemEventTypes.Instance.ImportantWalletUpdate, new object[] {},  new CorrelationContext());
				});

				return successCalls;
			}

			walletActions.Add(Operation);

		}

		protected virtual void ProcessLocalConfirmedAccountResetTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : IAccountResetTransaction {
			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountRecoveryTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : ISetAccountRecoveryTransaction {

			//TODO: set this
		}

		protected virtual void ProcessLocalConfirmedSetAccountCorrelationIdTransaction<T>(long BlockId, T trx, IWalletAccount account, List<Func<List<Action>>> walletActions)
			where T : ISetAccountCorrelationIdTransaction {
			
			walletActions.Add(() => {
				
				account.CorrelationId = trx.CorrelationId;
				
				return null;
			});
		}

		protected virtual void ProcessLocalRejectedPresentationTransaction(long BlockId, RejectedTransaction trx, IWalletAccount account, List<Func<List<Action>>> walletActions) {
			// thats it, this account is now rejected.
			account.Status = Enums.PublicationStatus.Rejected;
		}

	#endregion

	#region handle moderator Transactions

		protected virtual void HandleGenesisModeratorAccountTransaction<T>(T genesisModeratorAccountPresentationTransaction)
			where T : IGenesisModeratorAccountPresentationTransaction {
			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = genesisModeratorAccountPresentationTransaction.CommunicationsCryptographicKey;
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle communicationsCryptographicKey = dehydrator.ToArray();

			chainStateProvider.InsertModeratorKey(genesisModeratorAccountPresentationTransaction.TransactionId, cryptographicKey.Id, communicationsCryptographicKey);

			cryptographicKey = genesisModeratorAccountPresentationTransaction.BlocksXmssMTCryptographicKey;
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

		protected virtual void HandleGenesisAccountPresentationTransaction<T>(T genesisAccountPresentationTransaction)
			where T : IGenesisAccountPresentationTransaction {

			// do nothing
		}

		protected virtual void HandleModeratorKeyChangeTransaction<T>(T moderatorKeyChangeTransaction)
			where T : IModeratorKeyChangeTransaction {

			// add the moderator keys
			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

			ICryptographicKey cryptographicKey = moderatorKeyChangeTransaction.NewCryptographicKey;
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			cryptographicKey.Dehydrate(dehydrator);

			SafeArrayHandle modifiedCryptographicKey = dehydrator.ToArray();

			chainStateProvider.UpdateModeratorKey(moderatorKeyChangeTransaction.TransactionId, cryptographicKey.Id, modifiedCryptographicKey);
		}

		protected virtual void HandleChainOperatingRulesTransaction(IChainOperatingRulesTransaction chainOperatingRulesTransaction) {

			IChainStateProvider chainStateProvider = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase;

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