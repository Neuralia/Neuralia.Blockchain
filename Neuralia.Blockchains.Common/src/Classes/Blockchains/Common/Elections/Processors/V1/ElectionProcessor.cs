using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using RestSharp;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors.V1 {

	/// <summary>
	///     The main class that will perform the mining processing
	/// </summary>
	public abstract class ElectionProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IElectionProcessor
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		protected ICentralCoordinator CentralCoordinator => this.centralCoordinator;

		protected readonly IEventPoolProvider chainEventPoolProvider;

		public ElectionProcessor(CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider) {
			this.centralCoordinator = centralCoordinator;
			this.chainEventPoolProvider = chainEventPoolProvider;
		}

		/// <summary>
		/// Any extra hashing that we want to add as a layer can be provided here
		/// </summary>
		public Func<(SafeArrayHandle hash, long difficulty)> ExtraHashLayer { get; set; }

		/// <summary>
		///     Perform all the operations to determine if we get to be elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <param name="miningAccount"></param>
		/// <param name="dispatcher"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual ElectedCandidateResultDistillate PerformActiveElection(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount, Enums.MiningTiers miningTier) {

			if(matureBlockElectionDistillate.ElectionContext.ElectionMode != ElectionModes.Active) {
				return null;
			}

			// we only work if this is an active election
			if((miningAccount == default(AccountId)) || miningAccount.Equals(new AccountId())) {
				throw new ApplicationException("Impossible to mine with a null accountID");
			}

			this.CentralCoordinator.Log.Information("We are beginning an election...");
			SafeArrayHandle electionBallot = this.DetermineIfElected(matureBlockElectionDistillate, currentBlockElectionDistillate, miningAccount, miningTier);

			if(electionBallot == null) {
				this.CentralCoordinator.Log.Information("We are not elected.");

				return null; // thats it, we are not elected
			}

			// apply the optional extra hash layer in 3rd tier
			if(this.ExtraHashLayer != null && MiningTierUtils.IsThirdTier(miningTier)) {
				(SafeArrayHandle hash, long difficulty) = this.ExtraHashLayer();

				if(hash != null && !hash.IsZero) {
					BigInteger hashTarget = HashDifficultyUtils.GetHash512TargetByIncrementalDifficulty(difficulty);
					BigInteger currentHash = HashDifficultyUtils.GetBigInteger(hash);

					NLog.Default.Verbose($"Comparing {currentHash} in the {miningTier} tier with difficulty {difficulty} to election target {hashTarget}");

					if(currentHash >= hashTarget) {
						return null; // return null if we are not elected
					}
				}
			}

			this.CentralCoordinator.Log.Information($"We are elected in the {MiningTierUtils.GetOrdinalName(miningTier)} tier!.");

			// well, we were elected!  wow. lets go ahead and prepare our ticket so we can select transactions and move forward
			ElectedCandidateResultDistillate electedCandidateResultDistillate = this.CreateElectedCandidateResult();

			electedCandidateResultDistillate.ElectionMode = ElectionModes.Active;
			electedCandidateResultDistillate.BlockId = matureBlockElectionDistillate.electionBockId;
			electedCandidateResultDistillate.MaturityBlockId = matureBlockElectionDistillate.electionBockId + matureBlockElectionDistillate.ElectionContext.Maturity;

			if(electedCandidateResultDistillate.MaturityBlockId != currentBlockElectionDistillate.electionBockId) {
				throw new ApplicationException($"Invalid maturity block Id. expected block Id {electedCandidateResultDistillate.MaturityBlockId} but found current block Id {currentBlockElectionDistillate.electionBockId}");
			}

			electedCandidateResultDistillate.MaturityBlockHash = currentBlockElectionDistillate.blockxxHash;
			electedCandidateResultDistillate.MiningTier = miningTier;

			return electedCandidateResultDistillate;

		}

		public Dictionary<string, object> PrepareActiveElectionWebConfirmation(BlockElectionDistillate matureBlockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, Guid password) {
			if(matureBlockElectionDistillate.ElectionContext is IActiveElectionContext activeElectionContext) {

				Dictionary<string, object> parameters = new Dictionary<string, object>();

				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply

				parameters.Add("matureBlockId", electedCandidateResultDistillate.BlockId);
				parameters.Add("miningAccountId", matureBlockElectionDistillate.MiningAccountId.ToLongRepresentation());
				parameters.Add("maturityBlockHash", electedCandidateResultDistillate.MaturityBlockHash);
				parameters.Add("miningTier", (int) electedCandidateResultDistillate.MiningTier);
				parameters.Add("password", password);

				if(MiningTierUtils.IsFirstOrSecondTier(electedCandidateResultDistillate.MiningTier) && electedCandidateResultDistillate.secondTierAnswer.HasValue) {
					parameters.Add("secondTierAnswer", electedCandidateResultDistillate.secondTierAnswer.Value);
				}

				if(MiningTierUtils.IsFirstOrSecondTier(electedCandidateResultDistillate.MiningTier) && electedCandidateResultDistillate.digestAnswer.HasValue) {
					parameters.Add("digestAnswer", electedCandidateResultDistillate.digestAnswer.Value);
				}

				if(MiningTierUtils.IsFirstTier(electedCandidateResultDistillate.MiningTier) && electedCandidateResultDistillate.firstTierAnswer.HasValue) {
					parameters.Add("firstTierAnswer", electedCandidateResultDistillate.firstTierAnswer.Value);
				}

				if(electedCandidateResultDistillate.SelectedTransactionIds?.Any() ?? false) {

					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

					List<TransactionId> transactionsIds = electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)).ToList();

					dehydrator.Write(transactionsIds);

					using SafeArrayHandle data = dehydrator.ToArray();

					parameters.Add("selectedTransactions", data.Entry.ToBase64());

				}

				// now make sure that we apply correctly to any representative selection process.
				List<IActiveRepresentativeBallotingApplication> ballotingApplications = this.PrepareRepresentativesApplication(activeElectionContext.RepresentativeBallotingRules);

				if(ballotingApplications.Any()) {

					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

					dehydrator.Write(ballotingApplications);

					using SafeArrayHandle data = dehydrator.ToArray();

					parameters.Add("representativeBallotingApplications", data.Entry.ToBase64());

				}

				return parameters;
			}

			throw new ApplicationException("Must be an active election!");
		}

		public Dictionary<string, object> PreparePassiveElectionWebConfirmation(BlockElectionDistillate matureBlockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, Guid password) {
			if(matureBlockElectionDistillate.ElectionContext is IPassiveElectionContext passiveElectionContext) {
				this.CentralCoordinator.Log.Information("We are elected in a passive election!...");

				Dictionary<string, object> parameters = new Dictionary<string, object>();

				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply

				parameters.Add("matureBlockId", electedCandidateResultDistillate.BlockId);
				parameters.Add("miningAccountId", matureBlockElectionDistillate.MiningAccountId.ToLongRepresentation());
				parameters.Add("maturityBlockHash", electedCandidateResultDistillate.MaturityBlockHash);
				parameters.Add("password", password);

				if(electedCandidateResultDistillate.secondTierAnswer.HasValue && (electedCandidateResultDistillate.secondTierAnswer != 0)) {
					parameters.Add("secondTierAnswer", electedCandidateResultDistillate.secondTierAnswer);
				}

				if(electedCandidateResultDistillate.digestAnswer.HasValue && (electedCandidateResultDistillate.digestAnswer != 0)) {
					parameters.Add("digestAnswer", electedCandidateResultDistillate.digestAnswer);
				}

				if(electedCandidateResultDistillate.firstTierAnswer.HasValue && (electedCandidateResultDistillate.firstTierAnswer != 0)) {
					parameters.Add("firstTierAnswer", electedCandidateResultDistillate.firstTierAnswer);
				}

				// note:  we send a message even if we have no transactions. we may not get transaction fees, but the bounty is still applicable for being present and elected.
				if(electedCandidateResultDistillate.SelectedTransactionIds?.Any() ?? false) {

					using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

					List<TransactionId> transactionsIds = electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)).ToList();

					dehydrator.Write(transactionsIds);

					using SafeArrayHandle data = dehydrator.ToArray();

					parameters.Add("selectedTransactions", data.Entry.ToBase64());

				}

				return parameters;
			}

			throw new ApplicationException("Must be a passive election!");
		}

		public virtual IElectionCandidacyMessage PrepareActiveElectionConfirmationMessage(BlockElectionDistillate matureBlockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate) {

			if(matureBlockElectionDistillate.ElectionContext is IActiveElectionContext activeElectionContext) {
				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply
				ActiveElectionCandidacyMessage message = new ActiveElectionCandidacyMessage();

				message.BlockId = electedCandidateResultDistillate.BlockId;
				message.AccountId = matureBlockElectionDistillate.MiningAccountId;
				message.MaturityBlockHash = electedCandidateResultDistillate.MaturityBlockHash;
				message.MiningTier = electedCandidateResultDistillate.MiningTier;

				message.SecondTierAnswer = electedCandidateResultDistillate.secondTierAnswer;
				message.DigestAnswer = electedCandidateResultDistillate.digestAnswer;
				message.FirstTierAnswer = electedCandidateResultDistillate.firstTierAnswer;

				if(electedCandidateResultDistillate.SelectedTransactionIds?.Any() ?? false) {
					message.SelectedTransactions.AddRange(electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)));
				}

				// now make sure that we apply correctly to any representative selection process.
				message.RepresentativeBallotingApplications.AddRange(this.PrepareRepresentativesApplication(activeElectionContext.RepresentativeBallotingRules));

				return message;
			}

			throw new ApplicationException("Must be an active election!");
		}

		/// <summary>
		///     Perform all the operations if we are participating in a passive election
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <param name="miningAccount"></param>
		/// <param name="dispatcher"></param>
		public IElectionCandidacyMessage PreparePassiveElectionConfirmationMessage(BlockElectionDistillate matureBlockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate) {

			if(matureBlockElectionDistillate.ElectionContext is IPassiveElectionContext passiveElectionContext) {
				this.CentralCoordinator.Log.Information("We are elected in a passive election!...");

				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply

				PassiveElectionCandidacyMessage message = new PassiveElectionCandidacyMessage();

				message.BlockId = electedCandidateResultDistillate.BlockId;
				message.AccountId = matureBlockElectionDistillate.MiningAccountId;
				message.MaturityBlockHash = electedCandidateResultDistillate.MaturityBlockHash;
				message.MiningTier = electedCandidateResultDistillate.MiningTier;

				message.SecondTierAnswer = electedCandidateResultDistillate.secondTierAnswer;
				message.FirstTierAnswer = electedCandidateResultDistillate.firstTierAnswer;

				// note:  we send a message even if we have no transactions. we may not get transaction fees, but the bounty is still applicable for being present and elected.
				message.SelectedTransactions.AddRange(electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)));

				return message;
			}

			throw new ApplicationException("Must be a passive election!");
		}

		/// <summary>
		///     Determine if we are elected, or an election candidate on this turn
		/// </summary>
		/// <returns></returns>
		public virtual SafeArrayHandle DetermineIfElected(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount, Enums.MiningTiers miningTier) {

			return this.DetermineIfElected(matureBlockElectionDistillate, currentBlockElectionDistillate.blockHash, miningAccount, miningTier);
		}

		public virtual SafeArrayHandle DetermineIfElected(BlockElectionDistillate matureBlockElectionDistillate, SafeArrayHandle currentBlockHash, AccountId miningAccount, Enums.MiningTiers miningTier) {

			// initially, lets get our candidacy
			SafeArrayHandle candidacyHash = this.DetermineCandidacy(matureBlockElectionDistillate, currentBlockHash, miningAccount);

			// first step, lets run for the primaries and see if we qualify in the election
			return this.RunForPrimaries(candidacyHash, matureBlockElectionDistillate, miningAccount, miningTier);
		}

		/// <summary>
		///     This is where we will select our transactions based on the proposed selection algorithm
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="blockElectionDistillate"></param>
		/// <param name="lockContext"></param>
		/// <param name="originalContext"></param>
		/// <returns></returns>
		public async Task<List<TransactionId>> SelectTransactions(long blockId, BlockElectionDistillate blockElectionDistillate, LockContext lockContext) {

			ITransactionSelectionMethodFactory transactionSelectionMethodFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.CreateBlockComponentsRehydrationFactory().CreateTransactionSelectionMethodFactory();

			// select transaction based on the bounty allocation method to maximise our profit
			//TODO: consider bounty allocation method in our selection in the future
			ITransactionSelectionMethod transactionSelectionMethod = transactionSelectionMethodFactory.CreateTransactionSelectionMethod(this.GetTransactionSelectionMethodType(), blockId, this.centralCoordinator.ChainComponentProvider.ChainMiningProviderBase, blockElectionDistillate, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase, this.centralCoordinator.ChainComponentProvider.WalletProviderBase, this.centralCoordinator.BlockchainServiceSet);

			// get the transactions we already selected in previous mining, so we dont send them again
			IWalletAccount account = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
			List<TransactionId> existingTransactions = await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetElectionCacheTransactions(account, lockContext).ConfigureAwait(false);

			List<WebTransactionPoolResult> webTransactions = new List<WebTransactionPoolResult>();
			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			if(chainConfiguration.UseWebTransactionPool) {
				
				webTransactions = await centralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.QueryWebTransactionPool(lockContext).ConfigureAwait(false);
			}

			List<TransactionId> selectedTransactions = await transactionSelectionMethod.PerformTransactionSelection(this.chainEventPoolProvider, existingTransactions, webTransactions).ConfigureAwait(false);

			await this.centralCoordinator.ChainComponentProvider.WalletProviderBase.InsertElectionCacheTransactions(selectedTransactions, blockId, account, lockContext).ConfigureAwait(false);

			// ok, lets use them
			return selectedTransactions;
		}

		protected abstract ElectedCandidateResultDistillate CreateElectedCandidateResult();

		/// <summary>
		///     Fill in the required form to apply to become the elected representative, if applicable
		/// </summary>
		/// <param name="representativeBallotingRules"></param>
		/// <returns></returns>
		protected List<IActiveRepresentativeBallotingApplication> PrepareRepresentativesApplication(List<IRepresentativeBallotingRules> representativeBallotingRules) {

			List<IActiveRepresentativeBallotingApplication> results = new List<IActiveRepresentativeBallotingApplication>();

			foreach(IRepresentativeBallotingRules entry in representativeBallotingRules) {
				if(entry is IActiveRepresentativeBallotingRules activeRepresentativeBallotingRules) {
					IActiveRepresentativeBallotingApplication application = RepresentativeBallotingSelectorFactory.GetActiveRepresentativeSelector(activeRepresentativeBallotingRules).PrepareRepresentativeBallotingApplication(activeRepresentativeBallotingRules);
					results.Add(application);
				} else {
					results.Add(null);
				}
			}

			return results;
		}

		protected virtual TransactionSelectionMethodType GetTransactionSelectionMethodType() {

			switch(this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.TransactionSelectionStrategy) {
				case BlockChainConfigurations.TransactionSelectionStrategies.CreationTime:
					return TransactionSelectionMethodTypes.Instance.CreationTime;

				case BlockChainConfigurations.TransactionSelectionStrategies.TransactionType:
					return TransactionSelectionMethodTypes.Instance.TransationTypes;

				case BlockChainConfigurations.TransactionSelectionStrategies.Size:
					return TransactionSelectionMethodTypes.Instance.Size;

				case BlockChainConfigurations.TransactionSelectionStrategies.Random:
					return TransactionSelectionMethodTypes.Instance.Random;

				default:
					return TransactionSelectionMethodTypes.Instance.Automatic;
			}

		}

		/// <summary>
		///     run the proper candidacy selector and determine if we could be elected on this turn
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <param name="miningAccount"></param>
		/// <returns></returns>
		public SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount) {

			return this.DetermineCandidacy(matureBlockElectionDistillate, currentBlockElectionDistillate.blockHash, miningAccount); // we are simply not a candidate
		}

		public SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, SafeArrayHandle currentBlockHash, AccountId miningAccount) {

			if(matureBlockElectionDistillate.ElectionContext.CandidatureMethod.Version.Type == CandidatureMethodTypes.Instance.SimpleHash) {
				if(matureBlockElectionDistillate.ElectionContext.CandidatureMethod.Version == (1, 0)) {
					return ((SimpleHashCandidatureMethod) matureBlockElectionDistillate.ElectionContext.CandidatureMethod).DetermineCandidacy(matureBlockElectionDistillate, currentBlockHash, miningAccount);
				}
			}

			return null; // we are simply not a candidate
		}

		/// <summary>
		///     run the proper candidacy selector and determine if we could be elected on this turn
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <param name="miningAccount"></param>
		/// <returns></returns>
		protected SafeArrayHandle RunForPrimaries(SafeArrayHandle candidacy, BlockElectionDistillate blockElectionDistillate, AccountId miningAccount, Enums.MiningTiers miningTier) {

			SafeArrayHandle resultingBallot = candidacy;

			resultingBallot = blockElectionDistillate.ElectionContext.PrimariesBallotingMethod.PerformBallot(resultingBallot, blockElectionDistillate, miningTier, miningAccount);

			return resultingBallot;
		}
	}
}