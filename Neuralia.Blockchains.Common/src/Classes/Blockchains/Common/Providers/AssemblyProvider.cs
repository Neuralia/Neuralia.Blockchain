using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IAssemblyProvider : IChainProvider {
	}

	public interface IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAssemblyProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		
		Task<IStandardPresentationTransaction> GeneratePresentationTransaction(SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, string accountCode, LockContext lockContext, long? correlationId);
		Task<IPresentationTransactionEnvelope> GeneratePresentationEnvelope(IStandardPresentationTransaction presentationTransaction, SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, AccountCanPublishAPI publishInfo, CorrelationContext correlationContext, LockContext lockContext);

		Task<ITransactionEnvelope> GenerateKeyChangeTransaction(byte newKeyOrdinal, string keyChangeName, bool changeSuperKey, CorrelationContext correlationContext, LockContext lockContext);
		Task<ISignedMessageEnvelope> GenerateOnChainElectionsRegistrationMessage(AccountId electedAccountId, Enums.MiningTiers miningTier, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, LockContext lockContext);
		Task<IInitiationAppointmentMessageEnvelope> GenerateInitiationAppointmentRequestMessage(int preferredRegion, SafeArrayHandle publicKey, LockContext lockContext);
		Task<ISignedMessageEnvelope> GenerateAppointmentRequestMessage(int preferredRegion, LockContext lockContext);
		Task<(ISignedMessageEnvelope envelope, List<int> processedIds)> GenerateAppointmentVerificationResultsMessage(DateTime appointment, Func<DateTime, int, int, Task<List<IAppointmentRequesterResult>>> callback, LockContext lockContext);

		Task<ITransactionEnvelope> GenerateTransaction(ITransaction transaction, LockContext lockContext, Func<LockContext, Task> customProcessing = null, Func<ITransactionEnvelope, ITransaction, Task> finalizationProcessing = null);

		Task<List<ISignedMessageEnvelope>> PrepareElectionMessageEnvelopes(List<IElectionCandidacyMessage> messages, LockContext lockContext);
		Task PrepareTransactionBasics(ITransaction transaction, LockContext lockContext);
		
		Task PerformTHSSignature(ITHSEnvelope thsEnvelope, CancellationToken? cancellationToken, THSRulesSetDescriptor rulesSetDescriptor, Func<long[], int, Task> callback = null, CorrelationContext correlationContext = default);
		Task PerformEnvelopeSignature(IEnvelope envelope, LockContext lockContext, byte expiration = 0);
		Task PerformTransactionEnvelopeSignature(ITransactionEnvelope transactionEnvelope, LockContext lockContext, byte expiration = 0);
		Task PerformMessageEnvelopeSignature(ISignedMessageEnvelope messageEnvelope, LockContext lockContext);
	}

	/// <summary>
	///     this is where we do the transaction creation heavy lifting. Strong
	///     transaction Engineers required ;)
	/// </summary>
	public abstract class AssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly CENTRAL_COORDINATOR CentralCoordinator;

		protected readonly IBlockchainGuidService guidService;
		protected readonly IBlockchainTimeService timeService;

		public AssemblyProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.guidService = centralCoordinator.BlockchainServiceSet.BlockchainGuidService;
			this.CentralCoordinator = centralCoordinator;
		}

		#region Transaction Generation
			public virtual async Task<IStandardPresentationTransaction> GeneratePresentationTransaction(SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, string accountCode, LockContext lockContext, long? correlationId) {
			try {
				IStandardPresentationTransaction standardPresentation = this.CreateNewPresentationTransaction();

				await this.GenerateRawTransaction(standardPresentation, lockContext, async lc => {

					// we also publish the hash of our backup key, in case we ever need it

					this.CentralCoordinator.PostSystemEvent(accountPublicationStepSet.CreatingPresentationTransaction, correlationContext);

					// now lets publish our keys

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletIsLoaded();

					IWalletAccount account = null;

					// now we publish our keys
					if(!string.IsNullOrWhiteSpace(accountCode)) {
						account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);
					} else {
						account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);
					}
					
					// This is a VERY special case. presentation is the only transaction where we have no account ID on the chain.
					// so, we will overwrite the empty accountId and publish our hash of our internal account id, and the mods will assign us a public id
					standardPresentation.TransactionId.Account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetInitiationId(lc).ConfigureAwait(false);
					standardPresentation.CorrelationId = correlationId;
					
					using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.TRANSACTION_KEY_NAME, lc).ConfigureAwait(false)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.TRANSACTION_KEY_NAME}' key");
						}

						standardPresentation.TransactionCryptographicKey.SetFromKey(key);

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId;
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);
					}

					using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.MESSAGE_KEY_NAME, lc).ConfigureAwait(false)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.MESSAGE_KEY_NAME}' key");
						}

						standardPresentation.MessageCryptographicKey.SetFromKey(key);

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId;
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);

					}

					using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.CHANGE_KEY_NAME, lc).ConfigureAwait(false)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.CHANGE_KEY_NAME}' key");
						}

						standardPresentation.ChangeCryptographicKey.SetFromKey(key);

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId;
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);
					}

					using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.SUPER_KEY_NAME, lc).ConfigureAwait(false)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.SUPER_KEY_NAME}' key");
						}

						standardPresentation.SuperCryptographicKey.SetFromKey(key);

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId;
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key, lc).ConfigureAwait(false);
					}

					if(account.WalletAccountType == Enums.AccountTypes.Server) {

						standardPresentation.SetServer();
					}

				}).ConfigureAwait(false);

				return standardPresentation;
			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium presentation transaction", ex);
			}
		}
			
			public virtual async Task<ITransactionEnvelope> GenerateKeyChangeTransaction(byte newKeyOrdinal, string keyChangeName, bool changeSuperKey, CorrelationContext correlationContext, LockContext lockContext) {
			try {
				IStandardAccountKeyChangeTransaction standardAccountKeyChange = this.CreateNewKeyChangeTransaction(newKeyOrdinal);


				ITransactionEnvelope envelope = await this.GenerateTransaction(standardAccountKeyChange, lockContext, async lc => {

					// now lets publish our keys

					if(standardAccountKeyChange.TransactionId.Scope != 0) {
						throw new ApplicationException("Scope must always be 0 for a key change");
					}
					standardAccountKeyChange.TransactionId.Scope = 0;
					
					// now we publish our keys
					IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lc).ConfigureAwait(false);
					BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

					IWalletKey nextKey = null;

					if(!await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsNextKeySet(account.AccountCode, keyChangeName, lc).ConfigureAwait(false)) {
						nextKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateXmssKey(keyChangeName).ConfigureAwait(false);
					} else {
						nextKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadNextKey(account.AccountCode, keyChangeName, lc).ConfigureAwait(false);
					}

					if(nextKey == null) {
						throw new ApplicationException($"Failed to create new xmss '{keyChangeName}' key");
					}

					using(nextKey) {
						// we are declaring this key in this transaction, so lets update our key
						using(var existingKey = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey(account.AccountCode, keyChangeName, lc).ConfigureAwait(false)) {
							nextKey.KeyAddress = existingKey.KeyAddress.Clone();
							nextKey.KeyAddress.KeyUseIndex.IncrementSequence();
						}

						nextKey.KeyAddress.DeclarationTransactionId = standardAccountKeyChange.TransactionId;
						nextKey.KeyAddress.OrdinalId = newKeyOrdinal;
						nextKey.AccountCode = account.AccountCode;

						// lets set it as our next one
						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetNextKey(account.AccountCode, nextKey, lc).ConfigureAwait(false);

						await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet(lc).ConfigureAwait(false);

						// lets publish its public details
						standardAccountKeyChange.NewCryptographicKey.SetFromKey(nextKey);
					}
				}).ConfigureAwait(false);

				return envelope;
			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium key change transaction", ex);
			}
		}
		#endregion
			
		#region message Generation
			public async Task<IInitiationAppointmentMessageEnvelope> GenerateInitiationAppointmentRequestMessage(int preferredRegion, SafeArrayHandle publicKey, LockContext lockContext) {
			try {

				var walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

				InitiationAppointmentRequestMessage initiationAppointmentRequestMessage = this.CreateNewInitiationAppointmentRequestMessage();

				initiationAppointmentRequestMessage.PreferredRegion = preferredRegion;
				var account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
				initiationAppointmentRequestMessage.RequesterId = this.guidService.CreateTransactionId(account.PresentationId, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception).ToGuid();
				initiationAppointmentRequestMessage.WalletAccountType = account.WalletAccountType;

				// ok,  first thing we create an NTru key
				initiationAppointmentRequestMessage.ContactPublicKey = await walletProvider.CreateNTRUPrimeAppointmentRequestKey(lockContext).ConfigureAwait(false);
				initiationAppointmentRequestMessage.IdentityPublicKey = publicKey;
				
				IInitiationAppointmentMessageEnvelope envelope = await this.PrepareInitiationAppointmentBlockchainMessage(initiationAppointmentRequestMessage, lockContext).ConfigureAwait(false);

				return envelope;

			} catch(Exception ex) {
				throw new ApplicationException("failed to generate initiation appointment request message", ex);
			}
		}

		public async Task<ISignedMessageEnvelope> GenerateAppointmentRequestMessage(int preferredRegion, LockContext lockContext) {
			try {

				var walletProvider = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase;

				AppointmentRequestMessage appointmentRequestMessage = this.CreateNewAppointmentRequestMessage();

				appointmentRequestMessage.PreferredRegion = preferredRegion;

				var account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
				appointmentRequestMessage.RequesterId = this.guidService.CreateTransactionId(account.GetAccountId(), this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception).ToGuid();

				// ok,  first thing we create an NTru key
				appointmentRequestMessage.ContactPublicKey = await walletProvider.CreateNTRUPrimeAppointmentRequestKey(lockContext).ConfigureAwait(false);

				ISignedMessageEnvelope envelope = await this.PrepareSignedBlockchainMessage(appointmentRequestMessage, lockContext).ConfigureAwait(false);

				return envelope;

			} catch(Exception ex) {
				throw new ApplicationException("failed to generate appointment request message", ex);
			}
		}

		public async Task<(ISignedMessageEnvelope envelope, List<int> processedIds)> GenerateAppointmentVerificationResultsMessage(DateTime appointment, Func<DateTime, int, int, Task<List<IAppointmentRequesterResult>>> callback, LockContext lockContext) {

			List<int> processedIds = new List<int>();
			try {
				
				AppointmentVerificationResultsMessage appointmentVerificationResults = this.CreateNewAppointmentVerificationResultsMessage();

				appointmentVerificationResults.Appointment = appointment;

				int index = 0;
				int page = 50;
				while(true) {
					List<IAppointmentRequesterResult> entries = await callback(appointment, index*page, page).ConfigureAwait(false);

					if(!entries.Any()) {
						break;
					}
				
					// verify results
					var verificationResults = await CentralCoordinator.ChainComponentProvider.AppointmentsProviderBase.PrepareAppointmentRequesterResult(appointment, entries, lockContext).ConfigureAwait(false);
					
					foreach(var entry in entries) {

						AppointmentVerificationResultsMessage.RequesterResultEntry applicant = new AppointmentVerificationResultsMessage.RequesterResultEntry();
						
						applicant.Index = entry.Index;
						applicant.SecretCode = entry.SecretCode;
						applicant.ConditionVerification = false;
						applicant.CodeRequestTimestamp = entry.RequestedCodeCompleted;
						applicant.THSResults.Entry = SafeArrayHandle.Wrap(entry.THSResults).Entry;

						bool enablePuzzleTHS = !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableAppointmentPuzzleTHS;
						if(entry.PuzzleResults != null && entry.TriggerCompleted.HasValue && entry.PuzzleCompleted.HasValue && (enablePuzzleTHS?entry.THSCompleted.HasValue:true)) {

							try {
								applicant.PuzzleResults = AppointmentsResultTypeSerializer.DeserializeResultSet(SafeArrayHandle.Wrap(entry.PuzzleResults));

								applicant.TriggerTimestamp = entry.TriggerCompleted.Value;
								applicant.PuzzleCompleted = entry.PuzzleCompleted.Value;
								applicant.THSCompleted = entry.THSCompleted;
								
								if(verificationResults.ContainsKey(entry.Index)) {
									applicant.ConditionVerification = verificationResults[entry.Index];
								}
							} catch(Exception ex) {
								this.CentralCoordinator.Log.Error(ex, $"failed to process applicant id {entry.Index} in {nameof(GenerateAppointmentVerificationResultsMessage)}");
								// do nothing, refused nothing more
							}
						}

						processedIds.Add(entry.Id);
						appointmentVerificationResults.Applicants.Add(applicant);
					}

					index++;
				}

				ISignedMessageEnvelope envelope = await this.PrepareSignedBlockchainMessage(appointmentVerificationResults, lockContext).ConfigureAwait(false);

				return (envelope, processedIds);

			} catch(Exception ex) {
				throw new ApplicationException("failed to generate appointment verification results message", ex);
			}
		}
		
		public async Task<ISignedMessageEnvelope> GenerateOnChainElectionsRegistrationMessage(AccountId electedAccountId, Enums.MiningTiers miningTier, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, LockContext lockContext) {
			try {

				IElectionsRegistrationMessage registrationMessage = this.CreateNewMinerRegistrationMessage();

				registrationMessage.AccountId = electedAccountId;
				registrationMessage.MiningTier = miningTier;

				// now, we encrypt our data for the moderator to see

				using var bytes = electionsCandidateRegistrationInfo.Dehydrate();
				registrationMessage.EncryptedMessage.Entry = this.EncryptToModerator(bytes).Entry;
				ISignedMessageEnvelope envelope = await this.PrepareSignedBlockchainMessage(registrationMessage, lockContext).ConfigureAwait(false);

				return envelope;

			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium key change transaction", ex);
			}
		}
		
		
		protected async Task<IInitiationAppointmentMessageEnvelope> PrepareInitiationAppointmentBlockchainMessage(IInitiationAppointmentRequestMessage message, LockContext lockContext) {

			// // first, ensure that our account has NOT been published. otherwise, we can't use it
			Enums.PublicationStatus status = (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

			if(status != Enums.PublicationStatus.New) {
				throw new ApplicationException("Our Account has been published and confirmed. we can not create an initiation appointment yet with it.");
			}

			this.PrepareMessageBasics(message);

			IInitiationAppointmentMessageEnvelope envelope = this.CreateNewInitiationAppointmentMessageEnvelope();

			envelope.Contents = message.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

			envelope.THSEnvelopeSignature = new InitiationAppointmentEnvelopeSignature();
			envelope.THSEnvelopeSignature.RuleSet = THSRulesSet.InitiationAppointmentDefaultRulesSet;

			if(TestingUtil.Testing) {
				envelope.THSEnvelopeSignature.RuleSet = THSRulesSet.TestRuleset;
			}
			return envelope;
		}

		#endregion
			
		#region Preparation
			
		/// <summary>
		///     Generate a generic transaction. This version will save the wallet after generating
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="customProcessing">The custom processing to prepare the inner transaction</param>
		/// <param name="finalizationProcessing">
		///     Final processing done once the transaction is ready
		///     and signed
		/// </param>
		public async Task<ITransactionEnvelope> GenerateTransaction(ITransaction transaction, LockContext lockContext, Func<LockContext, Task> customProcessing = null, Func<ITransactionEnvelope, ITransaction, Task> finalizationProcessing = null) {

			await this.GenerateRawTransaction(transaction, lockContext, customProcessing).ConfigureAwait(false);

			return await this.PrepareTransactionEnvelope(transaction, lockContext, null, finalizationProcessing).ConfigureAwait(false);
		}

		/// <summary>
		///     prepare the transaction basics header data. This version is the one to use for most
		///     transaction types
		/// </summary>
		/// <param name="transaction"></param>
		public virtual async Task PrepareTransactionBasics(ITransaction transaction, LockContext lockContext) {

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight == 0) {
				throw new ApplicationException("Genesis block was never synced");
			}

			IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

			AccountId accountId = account.PublicAccountId;

			if(account.Status == Enums.PublicationStatus.New) {
				// new accounts dont have a public accountId. lets use the hash
				accountId = account.PresentationId;
			}

			this.PrepareTransactionBasics(transaction, accountId, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		protected async Task GenerateRawTransaction(ITransaction transaction, LockContext lockContext, Func<LockContext, Task> customProcessing = null) {

			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

			if(status != Enums.PublicationStatus.Published) {
				if(transaction is IStandardPresentationTransaction) {
					if(status == Enums.PublicationStatus.Dispatched) {
						throw new ApplicationException("Our account has been dispatched, but is not yet confirmed. We can not present ourselves again on the blockchain.");
					}
				} else {
					throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
				}
			}

			await this.PrepareTransactionBasics(transaction, lockContext).ConfigureAwait(false);

			// allow for the custom transaction processing
			if(customProcessing != null) {
				await customProcessing(lockContext).ConfigureAwait(false);
			}
		}
		
		
		protected virtual void PrepareTransactionBasics(ITransaction transaction, AccountId accountId, DateTime chainInception) {

			transaction.TransactionId = new TransactionId(this.guidService.CreateTransactionId(accountId, chainInception));

		}

		protected virtual void PrepareMessageBasics(IBlockchainMessage message) {

			message.Timestamp = this.timeService.GetChainDateTimeOffset(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}
		
		protected async Task<ISignedMessageEnvelope> PrepareSignedBlockchainMessage(IBlockchainMessage message, LockContext lockContext) {

			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

			if(status != Enums.PublicationStatus.Published) {
				throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
			}

			this.PrepareMessageBasics(message);

			ISignedMessageEnvelope envelope = await this.PrepareSignedMessageEnvelope(message, lockContext).ConfigureAwait(false);

			return envelope;
		}

		
		#endregion
			
		#region Envelopes
				
			
			public virtual async Task<IPresentationTransactionEnvelope> GeneratePresentationEnvelope(IStandardPresentationTransaction presentationTransaction, SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, AccountCanPublishAPI publishInfo, CorrelationContext correlationContext, LockContext lockContext) {
				try {

					IPresentationTransactionEnvelope envelope = (IPresentationTransactionEnvelope) await this.PrepareTransactionEnvelope(presentationTransaction, lockContext, e => {

						if(e is IPresentationTransactionEnvelope pe) {

							if(Guid.TryParse(publishInfo.RequesterId, out Guid requesterId)) {
								pe.RequesterId = requesterId;
							}

							if(long.TryParse(publishInfo.ConfirmationCode, out long confirmationCode)) {
								pe.ConfirmationCode = confirmationCode;
							}
							
							pe.THSEnvelopeSignature.RuleSet = THSRulesSet.PresentationDefaultRulesSet;

							if(TestingUtil.Testing) {
								pe.THSEnvelopeSignature.RuleSet = THSRulesSet.TestRuleset;
							}
							
							if(presentationTransaction.IsServer) {
								pe.THSEnvelopeSignature.RuleSet = THSRulesSet.ServerPresentationDefaultRulesSet;
								if(TestingUtil.Testing) {
									pe.THSEnvelopeSignature.RuleSet = THSRulesSet.TestRuleset;
								}
							}
						}
						return Task.CompletedTask;
					}).ConfigureAwait(false);

					// lets get it our transaction now
					envelope.Contents = presentationTransaction.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

					return envelope;
				} catch(Exception ex) {
					throw new ApplicationException("failed to generate neuralium presentation transaction", ex);
				}
			}
			
			public async Task<List<ISignedMessageEnvelope>> PrepareElectionMessageEnvelopes(List<IElectionCandidacyMessage> messages, LockContext lockContext) {

				List<ISignedMessageEnvelope> envelopes = new List<ISignedMessageEnvelope>();

				foreach(IElectionCandidacyMessage message in messages) {
					var envelope = await this.PrepareSignedBlockchainMessage(message, lockContext).ConfigureAwait(false);

					await this.PerformEnvelopeSignature(envelope, lockContext).ConfigureAwait(false);
					
					envelopes.Add(envelope);
				}

				return envelopes;
			}
		
		
		/// <summary>
		///     perform all the operations for the signature of the transaction
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="Key"></param>
		/// <param name="NextKey"></param>
		protected virtual async Task<ITransactionEnvelope> PrepareTransactionEnvelope(ITransaction transaction, LockContext lockContext, Func<ITransactionEnvelope, Task> furtherPreparations = null, Func<ITransactionEnvelope, ITransaction, Task> finalizationProcessing = null) {
			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false)).Status;

			if(status != Enums.PublicationStatus.Published) {
				if(transaction is IStandardPresentationTransaction) {
					if(status == Enums.PublicationStatus.Dispatched) {
						throw new ApplicationException("Our account has been dispatched, but is not yet confirmed. We can not present ourselves again on the blockchain.");
					}
				} else {
					throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
				}
			}

			if((transaction == null) || (transaction.TransactionId.Account == default(AccountId))) {
				throw new ApplicationException("The presentation transaction must be created before we can generate the envelope.");

			}
			
			try {

				EnvelopeSignatureType signatureType = EnvelopeSignatureTypes.Instance.Published;

				if(transaction is IStandardPresentationTransaction) {
					signatureType = EnvelopeSignatureTypes.Instance.Presentation;
				}

				ITransactionEnvelope transactionEnvelope = this.CreateNewTransactionEnvelope(signatureType);
				
				//any other preparations
				if(furtherPreparations != null) {
					await furtherPreparations(transactionEnvelope).ConfigureAwait(false);
				}
				
				if(transaction is IStandardPresentationTransaction presentationTransaction) {

					IPresentationTransactionEnvelope presentationTransactionEnvelope = (IPresentationTransactionEnvelope) transactionEnvelope;

					var account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount(lockContext).ConfigureAwait(false);

					if(presentationTransactionEnvelope.ConfirmationCode.HasValue && presentationTransactionEnvelope.ConfirmationCode.Value != 0) {
						// ok, lets prove our identity by signing the ConfirmationCode Id
						bool createIdentitySignature = true;
#if(COLORADO_EXCLUSION)
						if(presentationTransactionEnvelope.ConfirmationCode == GlobalsService.TESTING_APPOINTMENT_CODE) {
							createIdentitySignature = false;
						}
#endif
						if(createIdentitySignature) {
							SafeArrayHandle bytes = SafeArrayHandle.Create(sizeof(long));
							TypeSerializer.Serialize(presentationTransactionEnvelope.ConfirmationCode.Value, bytes.Span);

							var rehydrator = DataSerializationFactory.CreateRehydrator(account.AccountAppointment.IdentitySignatureKey);
							IXmssWalletKey key = new XmssWalletKey();
							key.Rehydrate(rehydrator);

							using XMSSProvider xmssProvider = new XMSSProvider(key.HashType, key.BackupHashType, key.TreeHeight, Enums.ThreadMode.ThreeQuarter, key.NoncesExponent);
							xmssProvider.Initialize();

							presentationTransactionEnvelope.IdentityAutograph = (await xmssProvider.Sign(bytes, key.PrivateKey, extraNodeCache: key.NextKeyNodeCache).ConfigureAwait(false)).signature;
						}
					}
					
					if(presentationTransaction.AccountType == Enums.AccountTypes.Server) {
						using var dehydrator = DataSerializationFactory.CreateDehydrator();
						PresentationTransactionEnvelope.PresentationMetadata metadata = new PresentationTransactionEnvelope.PresentationMetadata();

						// store the stride in our wallet

						using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, lockContext).ConfigureAwait(false)) {

							if(key == null) {
								throw new ApplicationException($"Failed to load '{GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME}' key");
							}

							metadata.ValidatorSignatureCryptographicKey.SetFromKey(key);
						}

						using(INTRUPrimeWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<INTRUPrimeWalletKey>(account.AccountCode, GlobalsService.VALIDATOR_SECRET_KEY_NAME, lockContext).ConfigureAwait(false)) {

							if(key == null) {
								throw new ApplicationException($"Failed to load '{GlobalsService.VALIDATOR_SECRET_KEY_NAME}' key");
							}

							metadata.ValidatorSecretCryptographicKey.SetFromKey(key);
						}
						
						account.Stride = SafeArrayHandle.Create(Constants.DEFAULT_STRIDE_LENGTH);
						account.Stride.FillSafeRandom();

						metadata.Stride = account.Stride.Clone();

						metadata.Dehydrate(dehydrator);
						using var bytes = dehydrator.ToArray();

						// encrypt to the mods
						((IPresentationTransactionEnvelope) transactionEnvelope).Metadata = this.EncryptToModeratorValidatorSecrets(bytes);
					}
				}

				if(finalizationProcessing != null) {
					await finalizationProcessing(transactionEnvelope, transaction).ConfigureAwait(false);
				}

				// lets get it our transaction now
				transactionEnvelope.Contents = transaction.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

				return transactionEnvelope;

			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic transaction signature", ex);
			}
		}
		
		protected virtual async Task<ISignedMessageEnvelope> PrepareSignedMessageEnvelope(IBlockchainMessage message, LockContext lockContext) {
			try {

				ISignedMessageEnvelope messageEnvelope = this.CreateNewSignedMessageEnvelope();
				messageEnvelope.Contents = message.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);
				
				return messageEnvelope;

			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic message", ex);
			}
		}

		
		protected virtual async Task<IInitiationAppointmentMessageEnvelope> PrepareInitiationAppointmentMessageEnvelope(IInitiationAppointmentRequestMessage message, LockContext lockContext) {

			IInitiationAppointmentMessageEnvelope messageEnvelope = this.CreateNewInitiationAppointmentMessageEnvelope();

			return messageEnvelope;
		}

		
		protected ITransactionEnvelope CreateNewTransactionEnvelope(EnvelopeSignatureType signatureType) {
			ITransactionEnvelope envelope = null;

			if(signatureType == EnvelopeSignatureTypes.Instance.Presentation) {
				envelope = this.CreateNewPresentationTransactionEnvelope();
			} else {
				envelope = this.CreateNewTransactionEnvelope();
			}

			if(signatureType == EnvelopeSignatureTypes.Instance.Published) {
				envelope.Signature = new PublishedEnvelopeSignature();

			}

			// else if(signatureType == EnvelopeSignatureTypes.Instance.SingleSecret) {
			// 	envelope.Signature = new SecretEnvelopeSignature();
			// } 
			else if(signatureType == EnvelopeSignatureTypes.Instance.Joint) {
				envelope.Signature = new JointEnvelopeSignature();

			} else if(signatureType == EnvelopeSignatureTypes.Instance.JointPublished) {
				envelope.Signature = new JointPublishedEnvelopeSignature();
			} else if(signatureType == EnvelopeSignatureTypes.Instance.Presentation) {
				IPresentationTransactionEnvelope presentationTransactionEnvelope = (IPresentationTransactionEnvelope) envelope;
				presentationTransactionEnvelope.Signature = new PresentationEnvelopeSignature();
				presentationTransactionEnvelope.THSEnvelopeSignature = new THSEnvelopeSignature();
				presentationTransactionEnvelope.THSEnvelopeSignature.RuleSet = THSRulesSet.PresentationDefaultRulesSet;
				
				if(TestingUtil.Testing) {
					presentationTransactionEnvelope.THSEnvelopeSignature.RuleSet = THSRulesSet.TestRuleset;
				}
			}

			return envelope;
		}
		

		#region signatures
			
			

			public virtual Task PerformEnvelopeSignature(IEnvelope envelope, LockContext lockContext, byte expiration = 0) {

				if(envelope is ITransactionEnvelope transactionEnvelope) {
					return this.PerformTransactionEnvelopeSignature(transactionEnvelope, lockContext, expiration);
				}
				else if(envelope is ISignedMessageEnvelope messageEnvelope) {
					return this.PerformMessageEnvelopeSignature(messageEnvelope, lockContext);
				}
				throw new ApplicationException("Invalid envelope type");
			}

			
			
		/// <summary>
		///     perform all the operations for the signature of the transaction
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="Key"></param>
		/// <param name="NextKey"></param>
		public virtual async Task PerformTransactionEnvelopeSignature(ITransactionEnvelope transactionEnvelope, LockContext lockContext, byte expiration = 0) {
			try {

				string keyName = GlobalsService.TRANSACTION_KEY_NAME;
				// key change transactions have a special allowance to go further in the xmss limits
				ITransaction transaction = transactionEnvelope.Contents.RehydratedEvent;
				
				bool allowPassKeyLimit = transaction is IKeychange;
				
				byte effectiveExpiration = expiration;
				
				transactionEnvelope.SetExpiration(effectiveExpiration, transaction.TransactionId, this.CentralCoordinator.BlockchainServiceSet.BlockchainTimeService, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
				
				// we will wait for the results, this is a VERY important event

				// the first step. we set the extended transaction id key use index for our XMSS key with our current state
				if(transactionEnvelope.Signature is PresentationEnvelopeSignature) {

					keyName = GlobalsService.TRANSACTION_KEY_NAME;
					using IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false);

					if((key.Status != Enums.KeyStatus.New) || (key.KeyAddress.AnnouncementBlockId != 0)) {
						throw new ApplicationException("Key has been published!");
					}

					// publish our key indices only if configured to do so. its important. we should though, because security through obscurity is not valid.
					if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.PublishKeyUseIndices) {
						transaction.TransactionMeta.KeyUseIndex = key.KeyAddress.KeyUseIndex.Clone2();
					}

					if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.PublishKeyUseLocks) {
						transaction.TransactionMeta.KeyUseLock = key.KeyAddress.KeyUseIndex.Clone2();
					}
				}
				else {
					
					if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
						
						keyName = GlobalsService.CHANGE_KEY_NAME;
						if(keyChangeTransaction.IsChangingChangeKey || keyChangeTransaction.IsChangingSuperKey) {
							keyName = GlobalsService.SUPER_KEY_NAME;
						}
					}
					using IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false);

					if((key.Status == Enums.KeyStatus.New) || (key.KeyAddress.AnnouncementBlockId == 0)) {
						throw new ApplicationException("Key has not been published!");
					}

					// publish our key indices only if configured to do so. its important. we should though, because security through obscurity is not valid.
					if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.PublishKeyUseIndices) {
						transaction.TransactionMeta.KeyUseIndex = key.KeyAddress.KeyUseIndex.Clone2();
					}

					if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.PublishKeyUseLocks) {
						// the key locked here will never be allowed again. this logic can be improved
						//TODO: elaborate this logic
						if(key.KeyAddress.KeyUseIndex.KeyUseIndex > 0) {
							transaction.TransactionMeta.KeyUseLock = new IdKeyUseIndexSet(key.KeyAddress.KeyUseIndex.KeyUseSequenceId, Math.Max(key.KeyAddress.KeyUseIndex.KeyUseIndex - 1, 0), key.KeyAddress.OrdinalId);
						} else {
							transaction.TransactionMeta.KeyUseLock = new IdKeyUseIndexSet(Math.Max(key.KeyAddress.KeyUseIndex.KeyUseSequenceId.Value - 1, 0), key.ChangeHeight - 1, key.KeyAddress.OrdinalId);
						}
					}
				}

				// now as the last step in the building, we hash the entire transaction to get the sakura tree root

				using var generatedHash = BlockchainHashingUtils.GenerateEnvelopedTransactionHash(transactionEnvelope, transaction);
				transactionEnvelope.Hash.Entry = generatedHash.Entry;

				async Task SignTransaction(IWalletKey key) {
					// hash the finalized transaction

					this.CentralCoordinator.Log.Verbose("Singing transaction...");

					SafeArrayHandle signature = null;

					if(key is IXmssWalletKey xmssWalletKey) {
						signature = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignTransactionXmss(transactionEnvelope.Hash, xmssWalletKey, lockContext, allowPassKeyLimit).ConfigureAwait(false);
					} else {
						signature = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignTransaction(transactionEnvelope.Hash, key, lockContext, allowPassKeyLimit).ConfigureAwait(false);
					}

					this.CentralCoordinator.Log.Verbose("Transaction successfully signed.");

					Task SetSignature(IAccountSignatureBase sig) {
						if(sig is IPublishedAccountSignature publishedAccountSignature) {

							publishedAccountSignature.KeyAddress = key.KeyAddress.Clone();

							// now we set our public key in case anybody would need it. mostly syncing nodes that are not yet up to date
							if(key is IXmssWalletKey xmssWalletKey1) {

								publishedAccountSignature.PublicKey = KeyFactory.ConvertKey(xmssWalletKey1);
							} else {
								throw new ApplicationException("Invalid key type");
							}

							// and sign the whole thing with our key
							publishedAccountSignature.Autograph.Entry = signature.Entry;
						} else if(sig is IPresentationAccountSignature presentationAccountSignature) {
							// and sign the whole thing with our key
							if(key is IXmssWalletKey xmssPresentationWalletKey && xmssPresentationWalletKey.KeyAddress.OrdinalId == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
								// good
								presentationAccountSignature.Autograph.Entry = signature.Entry;
							} else {
								throw new ApplicationException("invalid presentation key type");
							}
						} else if(sig is IPromisedSecretComboAccountSignature secretComboSig) {
							if(key is ISecretDoubleWalletKey secretDoubleWalletKey) {
								// a secret key publishes only the hash
								secretComboSig.PromisedPublicKey.Entry = secretDoubleWalletKey.PublicKey.Entry;
								secretComboSig.PromisedNonce1 = secretDoubleWalletKey.PromisedNonce1;
								secretComboSig.PromisedNonce2 = secretDoubleWalletKey.PromisedNonce2;

							}

							if(key is ISecretComboWalletKey secretComboWalletKey) {
								// a secret key publishes only the hash
								secretComboSig.PromisedPublicKey.Entry = secretComboWalletKey.PublicKey.Entry;
								secretComboSig.PromisedNonce1 = secretComboWalletKey.PromisedNonce1;
								secretComboSig.PromisedNonce2 = secretComboWalletKey.PromisedNonce2;

							} else {
								throw new ApplicationException("Wallet key is not of secret type.");
							}

							// and sign the whole thing with our key
							secretComboSig.Autograph.Entry = signature.Entry;
						} else if(sig is IPromisedSecretAccountSignature secretSig) {
							if(key is ISecretWalletKey secretWalletKey) {
								// a secret key publishes only the hash
								secretSig.PromisedPublicKey.Entry = secretWalletKey.PublicKey.Entry;

								// and sign the whole thing with our key
								secretSig.Autograph.Entry = signature.Entry;
							} else {
								throw new ApplicationException("Wallet key is not of secret type.");
							}
						} else {
							throw new ApplicationException("Invalid signature type.");
						}

						return Task.CompletedTask;
					}

					if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Published) {
						await SetSignature(((IPublishedEnvelopeSignature) transactionEnvelope.Signature).AccountSignature).ConfigureAwait(false);
					}
					
					else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Presentation) {

						IPresentationTransactionEnvelope presentationTransactionEnvelope = (IPresentationTransactionEnvelope) transactionEnvelope;
						await SetSignature(presentationTransactionEnvelope.PresentationEnvelopeSignature.AccountSignature).ConfigureAwait(false);

					} else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Joint) {
						// add the first signature
						//TODO: revise all this
						IPublishedAccountSignature accountSignature = new PublishedAccountSignature();

						await SetSignature(accountSignature).ConfigureAwait(false);

						((IJointEnvelopeSignature) transactionEnvelope.Signature).AccountSignatures.Add(accountSignature);
					} else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.JointPublished) {
						// add the first signature
						IPublishedAccountSignature accountSignature = new PublishedAccountSignature();

						await SetSignature(accountSignature).ConfigureAwait(false);

						((IJointEnvelopeSignature) transactionEnvelope.Signature).AccountSignatures.Add(accountSignature);
					}
				}

				if(transactionEnvelope.Signature is PresentationEnvelopeSignature) {
					// for the presentation, nothing so sign
					using IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false);

					await SignTransaction(key).ConfigureAwait(false);

					// increment the key use index
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalChainStateKeyHeight(key, lockContext).ConfigureAwait(false);

					// ok, we signed this transaction, so lets add it to our keyLog since our key has changed in the wallet already
					IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(key.AccountCode, lockContext).ConfigureAwait(false);
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertKeyLogTransactionEntry(account, transaction.TransactionId, transaction.TransactionMeta.KeyUseIndex, key.KeyAddress.OrdinalId, lockContext).ConfigureAwait(false);

				} else if(transactionEnvelope.Signature is InitiationAppointmentEnvelopeSignature initiationAppointmentEnvelopeSignature) {
					// we do nothing for now
				}
				else {
					using IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false);

					await SignTransaction(key).ConfigureAwait(false);

					// increment the key use index
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalChainStateKeyHeight(key, lockContext).ConfigureAwait(false);

					// ok, we signed this transaction, so lets add it to our keyLog since our key has changed in the wallet already
					IWalletAccount account = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(key.AccountCode, lockContext).ConfigureAwait(false);
					await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertKeyLogTransactionEntry(account, transaction.TransactionId, transaction.TransactionMeta.KeyUseIndex, key.KeyAddress.OrdinalId, lockContext).ConfigureAwait(false);

				}
			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic transaction signature", ex);
			}
		}

		public virtual async Task PerformMessageEnvelopeSignature(ISignedMessageEnvelope messageEnvelope, LockContext lockContext) {
			try {

				IBlockchainMessage message = messageEnvelope.Contents.RehydratedEvent;
				
				// now as the last step in the building, we hash the entire transaction to get the sakura tree root

				string keyName = GlobalsService.MESSAGE_KEY_NAME;

				if(message is IAppointmentVerificationResultsMessage) {
					keyName = GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME;
				}

				using var generatedHash = BlockchainHashingUtils.GenerateBlockchainMessageHash(messageEnvelope);
				messageEnvelope.Hash.Entry = generatedHash.Entry;

				// load our key, and use it to set what we need to
				using(IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false)) {
					
					this.CentralCoordinator.Log.Verbose("Singing message...");

					messageEnvelope.Signature.AccountSignature.KeyAddress = key.KeyAddress.Clone();

					messageEnvelope.Signature.AccountSignature.PublicKey = KeyFactory.ConvertKey(key);

					// and sign the whole thing with our key
					messageEnvelope.Signature.AccountSignature.Autograph.Entry = (await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(messageEnvelope.Hash, key, lockContext).ConfigureAwait(false)).Entry;

					this.CentralCoordinator.Log.Verbose("Message successfully signed.");
				}
			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic message signature", ex);
			}
		}
		
		public async Task PerformTHSSignature(ITHSEnvelope thsEnvelope, CancellationToken? cancellationToken, THSRulesSetDescriptor rulesSetDescriptor,  Func<long[], int, Task> callback = null, CorrelationContext correlationContext = default) {

			// this is a very special case where we hash before we create the envelope
			try {

				DateTime start = DateTime.Now;
				
				// first thing, trigger our intent to begin a THS.
				await CentralCoordinator.PostSystemEventImmediate(SystemEventGenerator.THSTrigger(), correlationContext).ConfigureAwait(false);
				Thread.Sleep(100);
				
				string key = thsEnvelope.Key;
				
				try {
					this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PauseNetwork();
					
					using var thsHash = BlockchainHashingUtils.GenerateTHSHash(thsEnvelope);

					using(THSEngine thsEngine = new THSEngine(thsEnvelope.THSEnvelopeSignatureBase.RuleSet, rulesSetDescriptor, GlobalSettings.ApplicationSettings.THSMemoryType)) {
						await thsEngine.Initialize(THSEngine.THSModes.Generate).ConfigureAwait(false);

						this.CentralCoordinator.Log.Information($"Beginning time hard signature. HashTargetDifficulty: {rulesSetDescriptor.HashTargetDifficulty}. Expected time span: {rulesSetDescriptor.TargetTimespan}. Nonce target count: {rulesSetDescriptor.NonceTarget}");

						THSState state = null;

						try {
							state = await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadCachedTHSState(key).ConfigureAwait(false);
						} catch(Exception ex) {
							this.CentralCoordinator.Log.Debug($"Failed to load THS cache!");
						}

						if(state != null) {
							if(SafeArrayHandle.Wrap(state.Hash) != thsHash) {
								state = null;
							}
						}

						if(state == null) {
							state = new THSState();
							state.Hash = thsHash.ToExactByteArrayCopy();
						}

						ClosureWrapper<int> lastRound = new ClosureWrapper<int>(state.thsState.Round);
						ClosureWrapper<DateTime> lastCheckpoint = DateTime.Now.AddMinutes(1);
						
						thsEnvelope.THSEnvelopeSignatureBase.Solution = await thsEngine.PerformTHS(thsHash, cancellationToken, (hashTargetDifficulty, targetTotalDuration, estimatedIterationTime, estimatedRemainingTime, startingNonce, startingRound, targetRoundNonce, solutions) => {
							this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.THSBegin(hashTargetDifficulty, rulesSetDescriptor.Rounds, targetTotalDuration, estimatedIterationTime, estimatedRemainingTime, startingNonce, startingRound, targetRoundNonce, solutions), correlationContext);

							return Task.CompletedTask;
						}, async (currentNonces, currentRound, totalNonce, solutions, estimatedIterationTime, estimatedRemainingTime, benchmarkSpeedRatio) => {

							if(callback != null) {
								await callback(currentNonces, currentRound).ConfigureAwait(false);
							}

							lastRound.Value = currentRound;
							this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.THSIteration(currentNonces, DateTime.Now - start, estimatedIterationTime, estimatedRemainingTime, benchmarkSpeedRatio), correlationContext);
							Thread.Sleep(5);

							this.CentralCoordinator.Log.Information($"THS Iteration. Current nonces: [{string.Join(',', currentNonces)}].");

							long smallestCurrentNonce = currentNonces[0];

							if(totalNonce != 0 && lastCheckpoint.Value < DateTime.Now) {
								// update the state
								state.thsState.Nonce = smallestCurrentNonce;
								state.thsState.Round = currentRound;
								state.thsState.TotalNonce = totalNonce;

								state.thsState.Solutions.Clear();

								foreach(var solution in solutions) {
									state.thsState.Solutions.Add(new THSProcessState.SolutionEntry(solution.solution, solution.nonce));
								}

								await CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.SaveCachedTHSState(state, key).ConfigureAwait(false);

								lastCheckpoint.Value = DateTime.Now.AddMinutes(1);
							}

						}, (currentRound, lastNonce, lastSolution) => {
							this.CentralCoordinator.PostSystemEvent(SystemEventGenerator.THSRound(currentRound, lastNonce, lastSolution), correlationContext);

							this.CentralCoordinator.Log.Information($"THS Round. Current round: {currentRound}. last solution nonce: {lastNonce}. Last solution: {lastSolution}");

							return Task.CompletedTask;
						}, state.thsState).ConfigureAwait(false);

						await CentralCoordinator.PostSystemEventImmediate(SystemEventGenerator.THSSolution(thsEnvelope.THSEnvelopeSignatureBase.Solution, rulesSetDescriptor.HashTargetDifficulty), correlationContext).ConfigureAwait(false);
						this.CentralCoordinator.Log.Information($"THS solution found!");
					}

					try {
						this.CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.ClearCachedTHSState(key);
					} catch(Exception ex) {
						this.CentralCoordinator.Log.Debug($"Failed to clear THS cache!");
					}

				} finally {
					this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RestoreNetwork();
				}

			} catch(Exception ex) {
				throw new ApplicationException("Failed to generate presentation transaction time hard signature", ex);
			}
		}

		#endregion
		#endregion

		#region tools
			
			protected SafeArrayHandle EncryptToModerator(SafeArrayHandle bytes) {
				ICryptographicKey key = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<ICryptographicKey>(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID).WaitAndUnwrapException();

				if(key is NTRUPrimeCryptographicKey ntruCryptographicKey) {
					return LargeMessageEncryptor.Encrypt(bytes, ntruCryptographicKey.PublicKey, LargeMessageEncryptor.EncryptionStrength.Regular);
					
				} else if(key is McElieceCryptographicKey mcElieceCryptographicKey) {
					using McElieceEncryptor encryptor = new McElieceEncryptor();

					return encryptor.Encrypt(bytes, mcElieceCryptographicKey.PublicKey, mcElieceCryptographicKey.McElieceCipherMode);
				}

				throw new ApplicationException("Invalid moderator key type");
			}
			
			protected SafeArrayHandle EncryptToModeratorValidatorSecrets(SafeArrayHandle bytes) {
				ICryptographicKey key = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<ICryptographicKey>(GlobalsService.MODERATOR_VALIDATOR_SECRETS_KEY_ID).WaitAndUnwrapException();

				if(key is NTRUPrimeCryptographicKey ntruCryptographicKey) {

					return LargeMessageEncryptor.Encrypt(bytes, ntruCryptographicKey.PublicKey, LargeMessageEncryptor.EncryptionStrength.Strong);
				} else if(key is McElieceCryptographicKey mcElieceCryptographicKey) {
					using McElieceEncryptor encryptor = new McElieceEncryptor();

					return encryptor.Encrypt(bytes, mcElieceCryptographicKey.PublicKey, mcElieceCryptographicKey.McElieceCipherMode);
				}

				throw new ApplicationException("Invalid moderator key type");
			}
		#endregion
			
		#region creators
			protected abstract IStandardPresentationTransaction CreateNewPresentationTransaction();
			protected abstract IStandardAccountKeyChangeTransaction CreateNewKeyChangeTransaction(byte ordinalId);

			protected abstract ITransactionEnvelope CreateNewTransactionEnvelope();
			protected abstract IPresentationTransactionEnvelope CreateNewPresentationTransactionEnvelope();

			protected abstract ISignedMessageEnvelope CreateNewSignedMessageEnvelope();
			protected abstract IInitiationAppointmentMessageEnvelope CreateNewInitiationAppointmentMessageEnvelope();

			protected abstract IElectionsRegistrationMessage CreateNewMinerRegistrationMessage();
			protected abstract InitiationAppointmentRequestMessage CreateNewInitiationAppointmentRequestMessage();
			protected abstract AppointmentRequestMessage CreateNewAppointmentRequestMessage();
			protected abstract AppointmentVerificationResultsMessage CreateNewAppointmentVerificationResultsMessage();
		#endregion
	}

}