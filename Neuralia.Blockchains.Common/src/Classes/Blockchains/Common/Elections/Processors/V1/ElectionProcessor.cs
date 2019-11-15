using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods.Active;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Elections.Processors.V1 {

	/// <summary>
	///     The main class that will perform the mining processing
	/// </summary>
	public abstract class ElectionProcessor<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IElectionProcessor
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		protected readonly IEventPoolProvider chainEventPoolProvider;

		public ElectionProcessor(CENTRAL_COORDINATOR centralCoordinator, IEventPoolProvider chainEventPoolProvider) {
			this.centralCoordinator = centralCoordinator;
			this.chainEventPoolProvider = chainEventPoolProvider;
		}

		/// <summary>
		///     Perform all the operations to determine if we get to be elected
		/// </summary>
		/// <param name="electionBlock"></param>
		/// <param name="miningAccount"></param>
		/// <param name="dispatcher"></param>
		/// <exception cref="ApplicationException"></exception>
		public virtual ElectedCandidateResultDistillate PerformActiveElection(int maturityBlockHash, BlockElectionDistillate blockElectionDistillate, AccountId miningAccount) {

			if(blockElectionDistillate.ElectionContext.ElectionMode != ElectionModes.Active) {
				return null;
			}

			// we only work if this is an active election
			if((miningAccount == null) || miningAccount.Equals(new AccountId())) {
				throw new ApplicationException("Impossible to mine with a null accountID");
			}

			Log.Information("We are beginning an election...");
			SafeArrayHandle electionBallot = this.DetermineIfElected(blockElectionDistillate, miningAccount);

			if(electionBallot == null) {
				Log.Information("We are not elected.");

				return null; // thats it, we are not elected
			}

			Log.Information("We are elected!.");

			// well, we were elected!  wow. lets go ahead and prepare our ticket so we can select transactions and move forward
			ElectedCandidateResultDistillate electedCandidateResultDistillate = this.CreateElectedCandidateResult();

			electedCandidateResultDistillate.ElectionMode = ElectionModes.Active;
			electedCandidateResultDistillate.BlockId = blockElectionDistillate.currentBlockId;
			electedCandidateResultDistillate.MaturityBlockId = blockElectionDistillate.currentBlockId + blockElectionDistillate.ElectionContext.Maturity;
			electedCandidateResultDistillate.MaturityBlockHash = maturityBlockHash;

			return electedCandidateResultDistillate;

		}

		public Dictionary<string, object> PrepareActiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, long password) {
			if(blockElectionDistillate.ElectionContext is IActiveElectionContext activeElectionContext) {
				
				Dictionary<string, object> parameters = new Dictionary<string, object>();
				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply

				parameters.Add("matureBlockId", electedCandidateResultDistillate.BlockId);
				parameters.Add("miningAccountId", blockElectionDistillate.MiningAccountId.ToLongRepresentation());
				parameters.Add("maturityBlockHash", electedCandidateResultDistillate.MaturityBlockHash);
				parameters.Add("password", password);
				
				if(electedCandidateResultDistillate.simpleAnswer.HasValue && electedCandidateResultDistillate.simpleAnswer != 0) {
					parameters.Add("simpleAnswer", electedCandidateResultDistillate.simpleAnswer);
				}

				if(electedCandidateResultDistillate.hardAnswer.HasValue && electedCandidateResultDistillate.hardAnswer != 0) {
					parameters.Add("hardAnswer", electedCandidateResultDistillate.hardAnswer);
				}

				if(electedCandidateResultDistillate.SelectedTransactionIds?.Any() ?? false) {
					
					var dehydrator = DataSerializationFactory.CreateDehydrator();

					var transactionsIds = electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)).ToList();

					dehydrator.Write(transactionsIds);

					using(SafeArrayHandle data = dehydrator.ToArray()) {

						parameters.Add("selectedTransactions", data.Entry.ToBase64());
					}
				}

				// now make sure that we apply correctly to any representative selection process.
				var ballotingApplications = this.PrepareRepresentativesApplication(activeElectionContext.RepresentativeBallotingRules);

				if(ballotingApplications.Any()) {
					
					var dehydrator = DataSerializationFactory.CreateDehydrator();
					
					dehydrator.Write(ballotingApplications);

					using(SafeArrayHandle data = dehydrator.ToArray()) {

						parameters.Add("representativeBallotingApplications", data.Entry.ToBase64());
					}
				}
				
				return parameters;
			}

			throw new ApplicationException("Must be an active election!");
		}

		public Dictionary<string, object> PreparePassiveElectionWebConfirmation(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate, long password) {
			if(blockElectionDistillate.ElectionContext is IPassiveElectionContext passiveElectionContext) {
				Log.Information("We are elected in a passive election!...");

				Dictionary<string, object> parameters = new Dictionary<string, object>();
				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply


				parameters.Add("matureBlockId", electedCandidateResultDistillate.BlockId);
				parameters.Add("miningAccountId", blockElectionDistillate.MiningAccountId.ToLongRepresentation());
				parameters.Add("maturityBlockHash", electedCandidateResultDistillate.MaturityBlockHash);
				parameters.Add("password", password);

				if(electedCandidateResultDistillate.simpleAnswer.HasValue && electedCandidateResultDistillate.simpleAnswer != 0) {
					parameters.Add("simpleAnswer", electedCandidateResultDistillate.simpleAnswer);
				}

				if(electedCandidateResultDistillate.hardAnswer.HasValue && electedCandidateResultDistillate.hardAnswer != 0) {
					parameters.Add("hardAnswer", electedCandidateResultDistillate.hardAnswer);
				}
				
				// note:  we send a message even if we have no transactions. we may not get transaction fees, but the bounty is still applicable for being present and elected.
				if(electedCandidateResultDistillate.SelectedTransactionIds?.Any() ?? false) {
					
					var dehydrator = DataSerializationFactory.CreateDehydrator();

					var transactionsIds = electedCandidateResultDistillate.SelectedTransactionIds.Select(t => new TransactionId(t)).ToList();

					dehydrator.Write(transactionsIds);

					using(SafeArrayHandle data = dehydrator.ToArray()) {

						parameters.Add("selectedTransactions", data.Entry.ToBase64());
					}
				}

				return parameters;
			}

			throw new ApplicationException("Must be a passive election!");
		}

		
		public virtual IElectionCandidacyMessage PrepareActiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate) {

			if(blockElectionDistillate.ElectionContext is IActiveElectionContext activeElectionContext) {
				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply
				ActiveElectionCandidacyMessage message = new ActiveElectionCandidacyMessage();

				message.BlockId = electedCandidateResultDistillate.BlockId;
				message.AccountId = blockElectionDistillate.MiningAccountId;
				message.MaturityBlockHash = electedCandidateResultDistillate.MaturityBlockHash;

				message.SimpleAnswer = electedCandidateResultDistillate.simpleAnswer;
				message.HardAnswer = electedCandidateResultDistillate.hardAnswer;
				
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
		public IElectionCandidacyMessage PreparePassiveElectionConfirmationMessage(BlockElectionDistillate blockElectionDistillate, ElectedCandidateResultDistillate electedCandidateResultDistillate) {

			if(blockElectionDistillate.ElectionContext is IPassiveElectionContext passiveElectionContext) {
				Log.Information("We are elected in a passive election!...");

				// well, we were elected!  wow. lets go ahead and choose our transactions and build our reply

				PassiveElectionCandidacyMessage message = new PassiveElectionCandidacyMessage();

				message.BlockId = electedCandidateResultDistillate.BlockId;
				message.AccountId = blockElectionDistillate.MiningAccountId;
				message.MaturityBlockHash = electedCandidateResultDistillate.MaturityBlockHash;
				
				message.SimpleAnswer = electedCandidateResultDistillate.simpleAnswer;
				message.HardAnswer = electedCandidateResultDistillate.hardAnswer;

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
		public virtual SafeArrayHandle DetermineIfElected(BlockElectionDistillate blockElectionDistillate, AccountId miningAccount) {

			// initially, lets get our candidacy
			SafeArrayHandle candidacyHash = this.DetermineCandidacy(blockElectionDistillate, miningAccount);

			// first step, lets run for the primaries and see if we qualify in the election
			return this.RunForPrimaries(candidacyHash, blockElectionDistillate, miningAccount);
		}

		/// <summary>
		///     This is where we will select our transactions based on the proposed selection algorithm
		/// </summary>
		/// <param name="blockId"></param>
		/// <param name="originalContext"></param>
		/// <returns></returns>
		public List<TransactionId> SelectTransactions(long blockId, BlockElectionDistillate blockElectionDistillate) {

			ITransactionSelectionMethodFactory transactionSelectionMethodFactory = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.CreateBlockComponentsRehydrationFactory().CreateTransactionSelectionMethodFactory();

			// select transaction based on the bounty allocation method to maximise our profit
			//TODO: consider bouty allocation method in our selection in the future
			ITransactionSelectionMethod transactionSelectionMethod = transactionSelectionMethodFactory.CreateTransactionSelectionMethod(this.GetTransactionSelectionMethodType(), blockId, blockElectionDistillate, this.centralCoordinator.ChainComponentProvider.WalletProviderBase, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration, this.centralCoordinator.BlockchainServiceSet);

			// get the transactions we already selected in previous mining, so we dont send them again
			IWalletAccount account = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();
			var existingTransactions = this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetElectionCacheTransactions(account);

			var selectedTransactions = transactionSelectionMethod.PerformTransactionSelection(this.chainEventPoolProvider, existingTransactions);

			this.centralCoordinator.ChainComponentProvider.WalletProviderBase.InsertElectionCacheTransactions(selectedTransactions, blockId, account);

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

			var results = new List<IActiveRepresentativeBallotingApplication>();

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
		protected SafeArrayHandle DetermineCandidacy(BlockElectionDistillate blockElectionDistillate, AccountId miningAccount) {

			if(blockElectionDistillate.ElectionContext.CandidatureMethod.Version.Type == CandidatureMethodTypes.Instance.SimpleHash) {
				if(blockElectionDistillate.ElectionContext.CandidatureMethod.Version == (1, 0)) {
					return ((SimpleHashCandidatureMethod) blockElectionDistillate.ElectionContext.CandidatureMethod).DetermineCandidacy(blockElectionDistillate, miningAccount);
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
		protected SafeArrayHandle RunForPrimaries(SafeArrayHandle candidacy, BlockElectionDistillate blockElectionDistillate, AccountId miningAccount) {

			SafeArrayHandle resultingBallot = candidacy;

			resultingBallot = blockElectionDistillate.ElectionContext.PrimariesBallotingMethod.PerformBallot(resultingBallot, blockElectionDistillate, miningAccount);

			return resultingBallot; // we are simply not a candidate
		}
	}
}