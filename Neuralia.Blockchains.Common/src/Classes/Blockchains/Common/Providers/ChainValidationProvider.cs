using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Simple;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Moderator;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Autographs;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Signatures;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using RestSharp.Validation;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IChainValidationProvider : IChainProvider {
		ValidationResult ValidateAccountKey(ICryptographicKey key, ITransaction transaction);
		Task ValidateBlock(IDehydratedBlock dehydratedBlock, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext);
		Task ValidateBlock(IBlock block, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext);
		Task ValidateTransaction(ITransactionEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular);
		Task ValidateBlockchainMessage(IMessageEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular);

		Task<ValidationResult> ValidateTransactionKeyDictionary(ITransactionEnvelope envelope, byte keyOrdinal, LockContext lockContext);
		Task<ValidationResult> ValidateBlockchainMessageKeyDictionary(ISignedMessageEnvelope envelope, byte keyOrdinal, LockContext lockContext);
		Task<ValidationResult> ValidateSignatureKeyDictionary(AccountId accountId, SafeArrayHandle message, SafeArrayHandle autograph, byte keyOrdinal, LockContext lockContext);

		Task<ValidationResult> ValidateDigest(IBlockchainDigest digest, bool verifyFiles, LockContext lockContext);

		Task ValidateEnvelopedContent(IEnvelope envelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular);

		Task<T> DisableTHS<T>(Func<IChainValidationProvider, LockContext, Task<T>> action, LockContext lockContext);
		IGatesDal GatesDal { get; }
		Task SetKeyGate(AccountId accountId, IdKeyUseIndexSet keyIndexLock);
		Task SetKeyGates(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates);
		Task<KeyUseIndexSet> GetKeyGate(AccountId accountId, byte ordinal);
		Task ClearKeyGates(List<AccountId> accountIds);
	}

	public interface IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainValidationProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class ChainValidationProvider {
		public enum ValidationModes {
			Self,
			Regular
		}
	}

	public abstract class ChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     How many cache entries to keep
		/// </summary>
		protected const int DEFAULT_CACHE_COUNT = 5;

		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		private readonly IGuidService guidService;

		private readonly IBlockchainTimeService timeService;
		private readonly RecursiveAsyncLock thsLocker = new RecursiveAsyncLock();
		protected bool enableTHSVerification = true;

		public ChainValidationProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.centralCoordinator = centralCoordinator;
		}

		private readonly object locker = new object();

		private string GetGatesStoragePath() {
			return Path.Combine(this.centralCoordinator.ChainComponentProvider.WalletProviderBase.GetChainStorageFilesPath(), "Gates");
		}

		private IGatesDal gatesDal;

		public IGatesDal GatesDal {
			get {

				lock(this.locker) {
					if(this.gatesDal == null) {
						this.gatesDal = this.centralCoordinator.ChainDalCreationFactory.CreateGatesDal(this.GetGatesStoragePath(), this.centralCoordinator.BlockchainServiceSet, this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SerializationType);
					}
				}

				return this.gatesDal;
			}
		}

		public Task SetKeyGate(AccountId accountId, IdKeyUseIndexSet keyIndexLock) {
			return this.GatesDal.SetKeyGate(accountId, keyIndexLock);
		}

		public Task SetKeyGates(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates) {
			return this.GatesDal.SetKeyGates(keyGates);
		}

		public Task<KeyUseIndexSet> GetKeyGate(AccountId accountId, byte ordinal) {

			return this.GatesDal.GetKeyGate(accountId, ordinal);
		}

		public Task ClearKeyGates(List<AccountId> accountIds) {
			return this.GatesDal.ClearKeyGates(accountIds);
		}

		public CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;

		public virtual async Task ValidateEnvelopedContent(IEnvelope envelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular) {

			if(envelope is IBlockEnvelope blockEnvelope) {
				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					throw new ApplicationException("Mobile apps can not validate blocks");
				}

				await this.ValidateBlock(blockEnvelope.Contents, gossipOrigin, completedResultCallback, lockContext).ConfigureAwait(false);

				return;
			}

			if(envelope is IMessageEnvelope messageEnvelope) {
				await this.ValidateBlockchainMessage(messageEnvelope, gossipOrigin, completedResultCallback, lockContext, validationMode).ConfigureAwait(false);

				return;
			}

			if(envelope is ITransactionEnvelope transactionEnvelope) {
				await this.ValidateTransaction(transactionEnvelope, gossipOrigin, completedResultCallback, lockContext, validationMode).ConfigureAwait(false);

				return;
			}

			throw new ApplicationException("Invalid envelope type");
		}

		/// <summary>
		/// perform an operation without doing the THS
		/// </summary>
		/// <param name="action"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public async Task<T> DisableTHS<T>(Func<IChainValidationProvider, LockContext, Task<T>> action, LockContext lockContext) {

			using(var state = await this.thsLocker.LockAsync(lockContext).ConfigureAwait(false)) {
				try {
					this.enableTHSVerification = false;

					return await action(this, state).ConfigureAwait(false);
				} finally {
					this.enableTHSVerification = true;
				}
			}
		}

		public async Task ValidateBlock(IDehydratedBlock dehydratedBlock, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext) {

			// lets make sure its rehydrated, we need it fully now

			try {
				dehydratedBlock.RehydrateBlock(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, true);
			} catch(UnrecognizedElementException urex) {

				throw;
			} catch {
				// just invalid
				completedResultCallback?.Invoke(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

			await this.ValidateBlock(dehydratedBlock.RehydratedBlock, gossipOrigin, completedResultCallback, lockContext).ConfigureAwait(false);
		}

		public virtual async Task ValidateBlock(IBlock block, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext) {

			ValidationResult result = null;

			if(block is IGenesisBlock genesisBlock) {
				result = await this.ValidateGenesisBlockOnlineVerification(genesisBlock, block.Hash).ConfigureAwait(false);

				completedResultCallback(result);
			} else if(block is ISimpleBlock simpleBlock) {
				long previousId = block.BlockId.Value - 1;

				async Task PerformBlockValidation(SafeArrayHandle previousBlockHash) {

					// we try 3 times until it validates. some times it fails for some reason
					//TODO: sometimes the validation fails, even if it succeeds a bit later. why?  possible issue to fix
					int attempt = 0;

					do {
						attempt++;
						result = await this.ValidateBlock(simpleBlock, gossipOrigin, block.Hash, previousBlockHash).ConfigureAwait(false);
					} while(result.Invalid && (attempt <= 3));

					completedResultCallback(result);
				}

				await PerformBlockValidation(SafeArrayHandle.Wrap(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockHash)).ConfigureAwait(false);
			} else {
				throw new ApplicationException("Invalid block type");
			}
		}

		public Task<ValidationResult> ValidateBlockchainMessageKeyDictionary(ISignedMessageEnvelope envelope, byte keyOrdinal, LockContext lockContext) {

			if(!this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyOrdinal)) {
				return Task.FromResult(new ValidationResult());
			}

			return this.ValidateEnvelopeKeyDictionary(envelope.Signature.AccountSignature, keyOrdinal, (key, signature) => this.ValidateBlockchainMessageSingleSignature(envelope.Hash, signature, key), this.CreateMessageValidationResult);
		}

		public Task<ValidationResult> ValidateTransactionKeyDictionary(ITransactionEnvelope envelope, byte keyOrdinal, LockContext lockContext) {

			if(!this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyOrdinal)) {
				return Task.FromResult(new ValidationResult());
			}

			if(envelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature) {
				return this.ValidateEnvelopeKeyDictionary(publishedEnvelopeSignature.AccountSignature, keyOrdinal, (key, signature) => this.ValidateTransactionSingleSignature(envelope.Hash, signature, key), this.CreateTransactionValidationResult);
			}

			return Task.FromResult(new ValidationResult());
		}

		/// <summary>
		///     validate an arbitrary message using Key dictionary
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="message"></param>
		/// <param name="autograph"></param>
		/// <param name="keyOrdinal"></param>
		/// <returns></returns>
		public Task<ValidationResult> ValidateSignatureKeyDictionary(AccountId accountId, SafeArrayHandle message, SafeArrayHandle autograph, byte keyOrdinal, LockContext lockContext) {

			IPublishedAccountSignature signature = new PublishedAccountSignature();
			signature.Autograph.Entry = autograph.Entry;
			signature.KeyAddress.OrdinalId = keyOrdinal;
			signature.KeyAddress.AccountId = accountId;

			return this.ValidateEnvelopeKeyDictionary(signature, keyOrdinal, (key, sig) => this.ValidateSingleSignature(message, sig, key), this.CreateTransactionValidationResult);
		}

		public async Task ValidateBlockchainMessage(IMessageEnvelope messageEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular) {

			// lets make sure its rehydrated, we need it fully now

			async Task<ValidationResult> Validate() {
				try {
					messageEnvelope.Contents.Rehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
				} catch(UnrecognizedElementException urex) {

					throw;
				} catch {
					// just invalid
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES);
				}

				// ok,l first lets compare the hashes
				IBlockchainMessage message = messageEnvelope.Contents.RehydratedEvent;

				TimeSpan acceptableRange = TimeSpan.FromHours(1);

				if(messageEnvelope is ISignedMessageEnvelope signedMessageEnvelope) {
					
					//first check the time to ensure we are within the acceptable range
					if(!this.timeService.WithinAcceptableRange(message.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception, acceptableRange)) {
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

						return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE);
					}
					
					using var rebuiltHash = BlockchainHashingUtils.GenerateBlockchainMessageHash(signedMessageEnvelope);
					bool hashValid = signedMessageEnvelope.Hash.Equals(rebuiltHash);

					if(hashValid != true) {

						return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.HASH_INVALID);
					}

					if(messageEnvelope is IModeratorSignedMessageEnvelope moderatorSignedMessageEnvelope) {

						// here we validate a message from the moderator!
						ICryptographicKey moderatorGossipKey = await this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey(GlobalsService.MODERATOR_GOSSIP_KEY_ID).ConfigureAwait(false);

						return await this.ValidateModeratorBlockchainMessageSignature(moderatorSignedMessageEnvelope.Hash, moderatorSignedMessageEnvelope.Signature.AccountSignature, moderatorGossipKey).ConfigureAwait(false);
					} else {

						// if the key is ahead of where we are and we are still syncing, we can use the embeded key to make a summary validation, enough to forward a gossip message
						if(GlobalSettings.ApplicationSettings.SynclessMode || ((signedMessageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) && (signedMessageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced)) {

							MessageValidationResult result = new MessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

							// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
							if(signedMessageEnvelope.Signature.AccountSignature.PublicKey?.PublicKey != null) {

								// ok, we can try to validate it using the included key. it does not mean the mssage is absolutely valid, but there may be a certain validity to it.
								ValidationResult includedResults = await this.ValidateBlockchainMessageSingleSignature(signedMessageEnvelope.Hash, signedMessageEnvelope.Signature.AccountSignature, signedMessageEnvelope.Signature.AccountSignature.PublicKey).ConfigureAwait(false);

								if(includedResults == ValidationResult.ValidationResults.Valid) {

									result = this.CreateMessageValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
								}
							}

							// we are not sure, but it passed this test at least	
							return result;
						}
					}
				} else if(messageEnvelope is IInitiationAppointmentMessageEnvelope initiationAppointmentMessageEnvelope) {
					// here we check THS since it has no signature
					THSRulesSetDescriptor rulesSetDescriptor = null;

					if(!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableWebRegAppointmentInitiationTHS || gossipOrigin) {
						if(TestingUtil.Testing) {
							if(initiationAppointmentMessageEnvelope.THSEnvelopeSignature.RuleSet != THSRulesSet.TestRuleset) {
								return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_RULESET);
							}

							rulesSetDescriptor = THSRulesSet.TestRulesetDescriptor;
						} else if(initiationAppointmentMessageEnvelope.THSEnvelopeSignature.RuleSet != THSRulesSet.InitiationAppointmentDefaultRulesSet) {
							if(initiationAppointmentMessageEnvelope.THSEnvelopeSignature.RuleSet != THSRulesSet.InitiationAppointmentDefaultRulesSet) {
								return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_RULESET);
							}

							rulesSetDescriptor = THSRulesSet.InitiationAppointmentDefaultRulesSetDescriptor;
						}

						return await this.ValidateProvedEnvelope(message.Timestamp, acceptableRange, true, BlockchainHashingUtils.GenerateBlockchainMessageHash(initiationAppointmentMessageEnvelope), initiationAppointmentMessageEnvelope, rulesSetDescriptor, lockContext).ConfigureAwait(false);
					}
				}

				if(GlobalSettings.ApplicationSettings.SynclessMode && validationMode == ChainValidationProvider.ValidationModes.Regular) {
					// mobile mode can not go any further

					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE);
				}

				if(messageEnvelope is ISignedMessageEnvelope signedMessageEnvelope2) {

					if(!this.ValidateBlockchainMessageKeyHierarchy(signedMessageEnvelope2.Signature, message)) {
						// this is a mistake, joint transactions MUST have a joint signature

						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_KEY_TYPE);
					}

					if((signedMessageEnvelope2.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) && (signedMessageEnvelope2.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

						// this doesnt work for us, we can't validate this

						return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID);
					}

					if(signedMessageEnvelope2.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {

						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);
					}

					if(!GlobalSettings.ApplicationSettings.SynclessMode && this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyDictionaryIndex) {

						byte keyOrdinal = signedMessageEnvelope2.Signature.AccountSignature.KeyAddress.OrdinalId;

						if(this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyOrdinal)) {
							ValidationResult fastKeyResult = await this.ValidateBlockchainMessageKeyDictionary(signedMessageEnvelope2, keyOrdinal, lockContext).ConfigureAwait(false);

							// if valid, we got it. if not valid, we will retry with the full loaded key
							if(fastKeyResult?.Valid ?? false) {

								return fastKeyResult;
							}
						}
					}

					// now we must get our key.
					try {

						ICryptographicKey key = null;

						if(GlobalSettings.ApplicationSettings.SynclessMode && validationMode == ChainValidationProvider.ValidationModes.Self) {
							// load the key from the wallet
							using(var walletKey = await centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(signedMessageEnvelope2.Signature.AccountSignature.KeyAddress.OrdinalId, lockContext).ConfigureAwait(true)) {
								key = new XmssCryptographicKey();
								key.SetFromKey(walletKey);
							}
						} else {
							key = await this.GetAccountKey(signedMessageEnvelope2.Signature.AccountSignature.KeyAddress).ConfigureAwait(false);
						}

						if(signedMessageEnvelope2.Signature.AccountSignature.PublicKey?.PublicKey != null) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!signedMessageEnvelope2.Signature.AccountSignature.PublicKey.PublicKey.Equals(key.PublicKey)) {
								// ok, we have a discrepancy. they embedded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here

								return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID);
							}
						}

						if(signedMessageEnvelope2.Contents.RehydratedEvent is IAppointmentVerificationResultsMessage appointmentVerificationResultsMessage) {
							// a special case where we sign with the validator key
							return await this.ValidateBlockchainMessageValidatorSingleSignature(signedMessageEnvelope2.Hash, signedMessageEnvelope2.Signature.AccountSignature, key).ConfigureAwait(false);
						}

						// thats it :)
						return await this.ValidateBlockchainMessageSingleSignature(signedMessageEnvelope2.Hash, signedMessageEnvelope2.Signature.AccountSignature, key).ConfigureAwait(false);

					} catch(Exception ex) {

						//TODO: what to do here?
						this.CentralCoordinator.Log.Fatal(ex, "Failed to validate message.");

						// this is very critical
						return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
					}
				}

				return new ValidationResult(ValidationResult.ValidationResults.Valid);
			}

			ValidationResult verificationResult = await Validate().ConfigureAwait(false);

			completedResultCallback(verificationResult);

		}

		public async Task ValidateTransaction(ITransactionEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback, LockContext lockContext, ChainValidationProvider.ValidationModes validationMode = ChainValidationProvider.ValidationModes.Regular) {

			IChainStateProvider chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;
			BlockChainConfigurations chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			// lets rehydrate the first level
			try {
				transactionEnvelope.RehydrateContents();
			} catch(UnrecognizedElementException urex) {

				throw;
			} catch {
				// just invalid
				completedResultCallback?.Invoke(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

			if(transactionEnvelope.Contents.Uuid == TransactionId.Empty) {
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ID));

				return;
			}

			// make sure the timestamp is not in the future
			DateTime transactionTime = this.timeService.GetTransactionDateTime(transactionEnvelope.Contents.Uuid, chainStateProvider.ChainInception);

			// add a grace minute

			if(transactionTime >= this.timeService.CurrentRealTime.AddMinutes(1)) {
				// its impossible for a transaction timestamp to be higher than our current time.
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TIMESTAMP));

				return;
			}

			// lets make sure its rehydrated, we need it fully now
			try {
				transactionEnvelope.Contents.Rehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
			} catch(UnrecognizedElementException urex) {

				throw;
			} catch {
				// just invalid
				completedResultCallback?.Invoke(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

//#if(!COLORADO_EXCLUSION)
			// lets make sure the expiration of the envelope is still within the timeframe

			var expiration = this.timeService.GetTransactionExpiration(transactionEnvelope, chainStateProvider.ChainInception);

			if(expiration < this.timeService.CurrentRealTime) {
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EXPIRED));

				return;
			}

//#endif

			// lets make sure the envelope correctly represents the transaction type
			bool isModerator = transactionEnvelope.Contents.RehydratedEvent is IModerationTransaction;

			if(isModerator) {
				completedResultCallback?.Invoke(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.MODERATION_TRANSACTION_NOT_ACCEPTED));

				return;
			}

			bool isPresentationEnvelope = transactionEnvelope is IPresentationTransactionEnvelope;
			IPresentationTransactionEnvelope presentationTransactionEnvelope = transactionEnvelope as IPresentationTransactionEnvelope;
			bool isPresentation = transactionEnvelope.Contents.RehydratedEvent is IPresentation;
			bool isKeyChange = transactionEnvelope.Contents.RehydratedEvent is IStandardAccountKeyChangeTransaction;
			bool isThsSigned = transactionEnvelope is ITHSEnvelope;
			
			if((isPresentation && !isPresentationEnvelope) || (!isPresentation && isPresentationEnvelope)) {
				completedResultCallback?.Invoke(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION));

				return;
			}

#if(!COLORADO_EXCLUSION)
			TimeSpan acceptableRange = TimeSpan.FromHours(1);

			if(transactionEnvelope.Signature is IJointEnvelopeSignature || transactionEnvelope.Contents.RehydratedEvent is IJointTransaction) {
				acceptableRange = TimeSpan.FromDays(1);
			}

			//first check the time to ensure we are within the acceptable range
			if(!isThsSigned && !this.timeService.WithinAcceptableRange(transactionEnvelope.Contents.RehydratedEvent.TransactionId.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception, acceptableRange)) {
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

				return;
			}
#endif

			// ok,l first lets compare the hashes
			ITransaction transaction = transactionEnvelope.Contents.RehydratedEvent;

			// check the account Id types
			bool isTempPresentationAccountId = transaction.TransactionId.Account.IsPresentation;

			// make sure the transaction ID has the right type
			if((isPresentation && !isTempPresentationAccountId) || (!isPresentation && isTempPresentationAccountId)) {
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ACCOUNT_ID_TYPE_INVALID));

				return;
			}

			// if there is a certificate id provided, lets check it
			bool? accreditationCertificateValid = null;

			using var rebuiltHash = BlockchainHashingUtils.GenerateEnvelopedTransactionHash(transactionEnvelope);
			bool hashValid = transactionEnvelope.Hash.Equals(rebuiltHash);

			if(hashValid != true) {
				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.HASH_INVALID));

				return;
			}

			if(transactionEnvelope.AccreditationCertificates.Any()) {

				accreditationCertificateValid = await this.CentralCoordinator.ChainComponentProvider.AccreditationCertificateProviderBase.IsAnyTransactionCertificateValid(transactionEnvelope.AccreditationCertificates, transaction.TransactionId, Enums.CertificateApplicationTypes.Envelope).ConfigureAwait(false);
			}

			// perform basic validations
			ValidationResult result = await this.PerformBasicTransactionValidation(transaction, transactionEnvelope, accreditationCertificateValid).ConfigureAwait(false);

			if(result.Valid) {
				result = await this.ValidateTransactionTypes(transactionEnvelope, transaction, gossipOrigin, lockContext).ConfigureAwait(false);
			}

			if(result != ValidationResult.ValidationResults.Valid) {
				completedResultCallback(result);

				return;
			}

			if(transactionEnvelope.Signature is ISingleEnvelopeSignature singleEnvelopeSignature) {

				if(transaction is IJointTransaction) {
					// this is a mistake, joint transactions MUST have a joint signature
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.JOINT_TRANSACTION_SINGLE_SIGNATURE));

					return;
				}

				KeyAddress keyAddress = null;
				IPublishedAccountSignature publishedAccountSignature = null;

				if(transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature) {

					if(!this.ValidateTransactionKeyHierarchy(publishedEnvelopeSignature.AccountSignature, transaction)) {
						// this is a mistake, joint transactions MUST have a joint signature
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE));

						return;
					}

					keyAddress = publishedEnvelopeSignature.AccountSignature.KeyAddress;
					publishedAccountSignature = publishedEnvelopeSignature.AccountSignature;

					if(keyAddress == null) {
						var checkResult = this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_ADDRESS);
						completedResultCallback(checkResult);

						return;
					}

					if(keyAddress.KeyUseIndex != null) {
						if(!await this.CheckKeyGate(keyAddress).ConfigureAwait(false)) {
							var checkResult = this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_FAILED_INDEX_LOCK);
							completedResultCallback(checkResult);

							return;
						}
					}
				} else if(transactionEnvelope.Signature is IPresentationEnvelopeSignature presentationEnvelopeSignature) {
					// thats fine, do nothing
					completedResultCallback(result);

					return;
				}

				// else if(transactionEnvelope.Signature is ISecretEnvelopeSignature secretEnvelopeSignature) {
				// 	keyAddress = secretEnvelopeSignature.AccountSignature.KeyAddress;
				// 	publishedAccountSignature = secretEnvelopeSignature.AccountSignature;
				// } 
				else {
					throw new ApplicationException("unsupported envelope signature type");
				}

				// if there is an embedded public key, wew can try using it
				if(!isKeyChange && publishedAccountSignature != null) {
					// if the key is ahead of where we are and we are still syncing, we can use the embedded key to make a summary validation, enough to forward a gossip message
					if(GlobalSettings.ApplicationSettings.SynclessMode || ((publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) && (publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced)) {

						TransactionValidationResult embdedKeyResult = new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

						// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
						if(publishedAccountSignature.PublicKey?.PublicKey != null) {

							// ok, we can try to validate it using the included key. it does not mean the message is absolutely valid, but there may be a certain validity to it.
							ValidationResult includedResults = await this.ValidateTransactionSingleSignature(transactionEnvelope.Hash, publishedAccountSignature, publishedAccountSignature.PublicKey).ConfigureAwait(false);

							if(includedResults == ValidationResult.ValidationResults.Valid) {

								// we are not sure, but it passed this test at least	
								embdedKeyResult = this.CreateTransactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
							}
						}

						// we are not sure, but it passed this test at least	
						completedResultCallback(embdedKeyResult);

						return;
					}

					if((publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) && (publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

						// this doesnt work for us, we can't validate this
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

						return;
					}
				}

				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					// mobile mode can not go any further
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE));

					return;
				}

				if(publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED));

					return;
				}

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyDictionaryIndex && this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyAddress.OrdinalId)) {
					// ok, we can take the fast route!

					ValidationResult fastKeyResult = await this.ValidateTransactionKeyDictionary(transactionEnvelope, keyAddress.OrdinalId, lockContext).ConfigureAwait(false);

					// if valid, we got it. if not valid, we will retry with the full loaded key
					if(fastKeyResult?.Valid ?? false) {
						completedResultCallback(fastKeyResult);

						return;
					}
				}

				try {
					if(result.Valid) {
						// now we must get our key. 
						ICryptographicKey key = await this.GetAccountKey(keyAddress).ConfigureAwait(false);

						if(publishedAccountSignature?.PublicKey?.PublicKey != null) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!publishedAccountSignature.PublicKey.PublicKey.Equals(key.PublicKey)) {
								// ok, we have a discrepancy. they embedded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here
								completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

								return;
							}
						}

						// thats it :)
						// if(singleEnvelopeSignature is ISecretEnvelopeSignature secretEnvelopeSignature) {
						//
						// 	if(key is ISecretDoubleCryptographicKey secretCryptographicKey) {
						// 		result = await this.ValidateSecretSignature(transactionEnvelope.Hash, secretEnvelopeSignature.AccountSignature, secretCryptographicKey).ConfigureAwait(false);
						// 	}
						// } else 
						if(transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature2) {
							result = await this.ValidateTransactionSingleSignature(transactionEnvelope.Hash, publishedEnvelopeSignature2.AccountSignature, key).ConfigureAwait(false);
						}

						completedResultCallback(result);
					}
				} catch(Exception ex) {
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

					//TODO: what to do here?
					this.CentralCoordinator.Log.Fatal(ex, "Failed to validate transaction.");

					// this is very critical
					throw;
				}

			} else if(transactionEnvelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {

				//TODO: enable joint accounts 
				await centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.RequireNodeUpdate(centralCoordinator.ChainId.Value, centralCoordinator.ChainName), new CorrelationContext()).ConfigureAwait(false);

				completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID));

				return;

				if(transactionEnvelope.Contents.RehydratedEvent is IJointTransaction) {

					result = await this.ValidateJointTransactionTypes(transactionEnvelope).ConfigureAwait(false);

					if(result.Invalid) {
						completedResultCallback(result);

						return;
					}
				}

				List<AccountId> requiredAccountIds = new List<AccountId>();
				List<AccountId> permittedAccountIds = new List<AccountId>();

				if(transactionEnvelope.Signature is IJointPublishedEnvelopeSignature jointPublishedEnvelopeSignature) {

					// ok, this one was published. let's get the rules form the transaction
					IIndexedTransaction jointTransaction = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadIndexedTransaction(jointPublishedEnvelopeSignature.Address);

					if(jointTransaction is IJointMembers jointMembersTransction) {

						permittedAccountIds = jointMembersTransction.MemberAccounts.Select(e => e.AccountId.ToAccountId()).ToList();
						requiredAccountIds = jointMembersTransction.MemberAccounts.Where(e => e.Required).Select(e => e.AccountId.ToAccountId()).ToList();
					} else {
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_REFERENCED_TRANSACTION));

						return;
					}
				} else {
					// all are required
					permittedAccountIds.AddRange(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress.AccountId));
					requiredAccountIds.AddRange(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress.AccountId));
				}

				// validatde that all included accounts are permitted
				List<AccountId> currentSignatureAccounts = jointEnvelopeSignature.AccountSignatures.Select(e => e.KeyAddress.AccountId).ToList();

				foreach(AccountId accountId in currentSignatureAccounts) {
					// the account must be permitted
					if(!permittedAccountIds.Contains(accountId)) {
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTS));

						return;
					}
				}

				// now make sure all required accounts are included in the envelope
				if(requiredAccountIds.Any(e => !currentSignatureAccounts.Contains(e))) {
					// we have missing required accounts
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MISSING_REQUIRED_JOINT_ACCOUNT));

					return;
				}

				foreach(var accountSignature in jointEnvelopeSignature.AccountSignatures) {
					if(!await this.CheckKeyGate(accountSignature.KeyAddress).ConfigureAwait(false)) {
						var checkResult = this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_FAILED_INDEX_LOCK);
						completedResultCallback(checkResult);

						return;
					}

					if(!this.ValidateTransactionKeyHierarchy(accountSignature, transaction)) {
						// this is a mistake, joint transactions MUST have a joint signature
						completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE));

						return;
					}
				}

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyDictionaryIndex) {
					// ok, we can take the fast route!
					//TODO: this is a bit all or nothing here. Some keys may be available as fast, others may not. mix the schemes optimally
					if(jointEnvelopeSignature.AccountSignatures.All(s => this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(s.KeyAddress.OrdinalId))) {
						Dictionary<AccountId, ICryptographicKey> keys = new Dictionary<AccountId, ICryptographicKey>();

						bool usesEmbededKey = false;

						foreach(IPublishedAccountSignature signature in jointEnvelopeSignature.AccountSignatures) {
							// see if we can get a key
							(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)? keyResults = await this.RebuildTransactionXmssKeyDictionary(signature, signature.KeyAddress.OrdinalId).ConfigureAwait(false);

							if(keyResults.HasValue) {

								usesEmbededKey = keyResults.Value.usesEmbededKey;

								if(keyResults.Value.result != null) {
									completedResultCallback(keyResults.Value.result);

									return;
								}

								if(keyResults.Value.xmssKey != null) {
									keys.Add(signature.KeyAddress.AccountId, keyResults.Value.xmssKey);
								}
							}
						}

						if(keys.Any()) {

							result = await this.ValidateTransactionMultipleSignatures(transactionEnvelope.Hash, transactionEnvelope, transactionEnvelope.Contents.RehydratedEvent, jointEnvelopeSignature.AccountSignatures, keys).ConfigureAwait(false);

							// if we used any embeded key, we can not fully trust the results
							if(result.Valid && usesEmbededKey) {
								result = this.CreateTransactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
							}

							completedResultCallback(result);

							return;
						}
					}
				}

				try {
					//.ToDictionary(t => t.Key, t => t.Value.Keyset.Keys[jointEnvelopeSignature.AccountSignatures.Single(s => s.KeyAddress.DeclarationTransactionId.Account == t.Key).KeyAddress.OrdinalId])

					Dictionary<AccountId, ICryptographicKey> keys = await this.GetAccountKeys(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress).ToList()).ConfigureAwait(false);

					// validate any embeded key to ensure if they were provided, they were right
					foreach((AccountId accountId, ICryptographicKey cryptoKey) in keys) {

						IPublishedAccountSignature publicSignature = jointEnvelopeSignature.AccountSignatures.SingleOrDefault(s => s.KeyAddress.AccountId == accountId);

						if(publicSignature?.PublicKey?.PublicKey != null) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!publicSignature.PublicKey.PublicKey.Entry.Equals(cryptoKey.PublicKey.Entry)) {
								// ok, we have a discrepansy. they embeded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here
								completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

								return;
							}
						}
					}

					// thats it :)
					result = await this.ValidateTransactionMultipleSignatures(transactionEnvelope.Hash, transactionEnvelope, transactionEnvelope.Contents.RehydratedEvent, jointEnvelopeSignature.AccountSignatures, keys).ConfigureAwait(false);

					completedResultCallback(result);

				} catch(Exception ex) {
					completedResultCallback(this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

					//TODO: what to do here?
					this.CentralCoordinator.Log.Fatal(ex, "Failed to validate transction.");

					// this is very critical
					throw;
				}
			}
		}

		/// <summary>
		/// here we verify which key is allowed to signed which signatures
		/// </summary>
		/// <param name="envelopeSignature"></param>
		/// <param name="transaction"></param>
		/// <returns></returns>
		protected virtual bool ValidateTransactionKeyHierarchy(IPublishedAccountSignature accountSignature, ITransaction transaction) {

			byte keyOrdinalId = accountSignature.KeyAddress.OrdinalId;

			if(transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
				// this is a special case with special rules
				byte changeKeyOrdinal = keyChangeTransaction.NewCryptographicKey.Ordinal;

				if(changeKeyOrdinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID || changeKeyOrdinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID || changeKeyOrdinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID || changeKeyOrdinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
					return keyOrdinalId == GlobalsService.CHANGE_KEY_ORDINAL_ID || changeKeyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID;
				}

				if(changeKeyOrdinal == GlobalsService.CHANGE_KEY_ORDINAL_ID || changeKeyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
					return keyOrdinalId == GlobalsService.SUPER_KEY_ORDINAL_ID;
				}

				return false;
			}

			// rules for all general transactions
			if(keyOrdinalId == GlobalsService.TRANSACTION_KEY_ORDINAL_ID || keyOrdinalId == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				return true;
			}

			return false;
		}

		protected virtual bool ValidateBlockchainMessageKeyHierarchy(IPublishedEnvelopeSignature envelopeSignature, IBlockchainMessage message) {

			byte keyOrdinalId = envelopeSignature.AccountSignature.KeyAddress.OrdinalId;

			if(message is IAppointmentVerificationResultsMessage resultsMessage) {
				// this is a special case with special rules
				return keyOrdinalId == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID;
			}

			// rules for all general transactions
			if(keyOrdinalId == GlobalsService.MESSAGE_KEY_ORDINAL_ID || keyOrdinalId == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				return true;
			}

			return false;
		}

		public async Task<ValidationResult> ValidateDigest(IBlockchainDigest digest, bool verifyFiles, LockContext lockContext) {

			//TODO: enable digests
			await this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.RequireNodeUpdate(this.centralCoordinator.ChainId.Value, this.centralCoordinator.ChainName), new CorrelationContext()).ConfigureAwait(false);

			return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID);

			// first, we validate the hash itself against the online double hash file
			if(!this.CentralCoordinator.ChainSettings.SkipDigestHashVerification) {

				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

				string digestHashesPath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.DigestHashesPath;
				FileExtensions.EnsureDirectoryStructure(digestHashesPath, this.CentralCoordinator.FileSystem);

				string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;
				(SafeArrayHandle sha2, SafeArrayHandle sha3) genesis = await fetchingService.FetchDigestHash(hashUrl, digestHashesPath, digest.DigestId).ConfigureAwait(false);

				bool hashVerifyResult = BlockchainDoubleHasher.VerifyDigestHash(digest, genesis.sha2, genesis.sha3);

				genesis.sha2.Return();
				genesis.sha3.Return();

				if(!hashVerifyResult) {
					return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.FAILED_DIGEST_HASH_VALIDATION);
				}
			}

			// lets make sure its rehydrated, we need it fully now

			ValidatingDigestChannelSet validatingDigestChannelSet = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.CreateValidationDigestChannelSet(digest.DigestId, digest.DigestDescriptor);
			/////////////////////////////////////////////////////////

			// ok, lets validate the files.
			using HashNodeList descriptorNodes = new HashNodeList();

			foreach(KeyValuePair<ushort, BlockchainDigestChannelDescriptor> channel in digest.DigestDescriptor.Channels.OrderBy(f => f.Key)) {

				uint slices = 1;

				if(channel.Value.GroupSize > 0) {
					slices = (uint) Math.Ceiling((double) channel.Value.LastEntryId / channel.Value.GroupSize);
				}

				Dictionary<(int index, int file), HashNodeList> cascadingHashSets = new Dictionary<(int index, int file), HashNodeList>();

				// prepare our hashing nodeAddressInfo structure
				foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor> index in channel.Value.DigestChannelIndexDescriptors) {
					foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor> file in index.Value.Files) {
						cascadingHashSets.Add((index.Key, file.Key), new HashNodeList());
					}
				}

				if(channel.Value.DigestChannelIndexDescriptors.ContainsKey(channel.Key)) {
					for(uint i = 1; i <= slices; i++) {
						// perform the actual hash
						Dictionary<int, Dictionary<int, SafeArrayHandle>> sliceHashes = verifyFiles ? validatingDigestChannelSet.Channels[channel.Key].HashChannel((int) i) : null;

						foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor> indexSet in channel.Value.DigestChannelIndexDescriptors) {
							foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor> fileset in indexSet.Value.Files) {

								BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor fileDescriptor = channel.Value.DigestChannelIndexDescriptors[indexSet.Key].Files[fileset.Key];
								SafeArrayHandle descriptorHash = fileDescriptor.DigestChannelIndexFilePartDescriptors[i].Hash;

								// if we are also verifying the files hash, then we do it here
								if(verifyFiles && !descriptorHash.Equals(sliceHashes[indexSet.Key][fileset.Key])) {
									// optional files will pass anyways
									if(!fileDescriptor.IsOptional) {
										return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_SLICE_HASH);
									}
								}

								// add the hash for the tree hashing
								cascadingHashSets[(indexSet.Key, fileset.Key)].Add(descriptorHash);
							}
						}
					}
				}

				// now the rest of the structure
				using HashNodeList nodes = new HashNodeList();
				using HashNodeList topnodes = new HashNodeList();

				topnodes.Add(channel.Value.DigestChannelIndexDescriptors.Count);

				foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor> indexDescriptor in channel.Value.DigestChannelIndexDescriptors.OrderBy(f => f.Key)) {

					nodes.Add(indexDescriptor.Value.Files.Count);

					foreach(KeyValuePair<int, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor> entry in indexDescriptor.Value.Files.OrderBy(f => f.Key)) {

						using HashNodeList nodesParts = new HashNodeList();

						nodesParts.Add(entry.Value.DigestChannelIndexFilePartDescriptors.Count);

						foreach(KeyValuePair<uint, BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor.DigestChannelIndexFilePartDescriptor> entry2 in entry.Value.DigestChannelIndexFilePartDescriptors.OrderBy(f => f.Key)) {

							nodesParts.Add(entry2.Value.Hash);
						}

						using SafeArrayHandle nodesPartsHash = HashingUtils.Hash3(nodesParts);

						if(!entry.Value.Hash.Equals(entry.Value.Hash.Entry)) {
							return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
						}

						nodes.Add(entry.Value.Hash);
					}

					using SafeArrayHandle nodesHash = HashingUtils.Hash3(nodes);

					if(!indexDescriptor.Value.Hash.Equals(nodesHash)) {
						return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
					}

					topnodes.Add(indexDescriptor.Value.Hash);
				}

				using SafeArrayHandle topnodesHash = HashingUtils.Hash3(topnodes);

				if(!channel.Value.Hash.Equals(topnodesHash)) {
					return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
				}

				descriptorNodes.Add(channel.Value.Hash);
			}

			using SafeArrayHandle descriptorNodesHash = HashingUtils.Hash3(descriptorNodes);

			if(!digest.DigestDescriptor.Hash.Equals(descriptorNodesHash)) {
				return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
			}

			// we did it, our files match!!  now the digest itself...

			// finally, at the top of the pyramid, lets compare the hashes
			(SafeArrayHandle sha2, SafeArrayHandle sha3) digestHashes = HashingUtils.ExtractCombinedDualHash(digest.Hash);
			(SafeArrayHandle sha2, SafeArrayHandle sha3) rebuiltHashes = HashingUtils.GenerateDualHash(digest);

			if(!digestHashes.sha2.Equals(rebuiltHashes.sha2) || !digestHashes.sha3.Equals(rebuiltHashes.sha3)) {
				return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_HASH);
			}

			// ensure that a valid key is beign used
			if(!this.ValidateDigestKeyTree(digest.Signature.KeyAddress.OrdinalId)) {
				return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_KEY);
			}

			// now the signature
			// ok, check the signature
			// first thing, get the key from our chain state
			IXmssmtCryptographicKey moderatorKey = await this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<IXmssmtCryptographicKey>(digest.Signature.KeyAddress.OrdinalId).ConfigureAwait(false);

			if((moderatorKey.PublicKey == null) || moderatorKey.PublicKey.IsEmpty) {
				throw new ApplicationException("Moderator key was not found in the chain state.");
			}

			// thats it :)
			ValidationResult result = await this.ValidateDigestSignature(digest.Hash, digest.Signature, moderatorKey).ConfigureAwait(false);

			// we did it, this is a valid digest!
			return result;
		}

		public async Task<(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)?> RebuildXmssKeyDictionary(IPublishedAccountSignature signature, byte keyOrdinal, Func<ValidationResult.ValidationResults, EventValidationErrorCode, ValidationResult> resultCreation) {

			if(!this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyOrdinal)) {
				this.CentralCoordinator.Log.Debug("Key dictionary was not enabled.");

				return null;
			}

			this.CentralCoordinator.Log.Debug("Key dictionary was enabled.");

			bool usesEmbededKey = false;

			// ok, we can take the fast route!
			try {
				(SafeArrayHandle keyBytes, byte treeheight, byte noncesExponent, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType)? keyBytes = await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadAccountKeyFromIndex(signature.KeyAddress.AccountId, keyOrdinal).ConfigureAwait(false);

				if((keyBytes?.keyBytes != null) && !keyBytes.Value.keyBytes.IsZero) {

					IXmssCryptographicKey xmssKey = null;

					if((signature.PublicKey?.PublicKey != null) && signature.PublicKey.PublicKey.HasData) {
						// ok, this message has an embeded public key. lets confirm its the same that we pulled up
						if(!signature.PublicKey.PublicKey.Equals(keyBytes.Value.keyBytes)) {
							// ok, we have a discrepancy. they embedded a key that does not match the public record. 
							//TODO: we should log the peer for bad acting here
							return (resultCreation(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID), null, false);
						}

						if(signature.PublicKey is IXmssCryptographicKey xmssPublicKey && (signature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) && (signature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced) {

							// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case. lets copy the key it has and use it
							xmssKey = new XmssCryptographicKey(xmssPublicKey);

							usesEmbededKey = true;
						}
					}

					if(xmssKey == null) {
						xmssKey = new XmssCryptographicKey();

						xmssKey.Ordinal = signature.KeyAddress.OrdinalId;
						xmssKey.Index = signature.KeyAddress.KeyUseIndex.KeyUseIndexSet.Clone();
						xmssKey.PublicKey.Entry = keyBytes.Value.keyBytes.Entry;
						xmssKey.TreeHeight = keyBytes.Value.treeheight;

						if(keyOrdinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
							xmssKey.NoncesExponent = WalletProvider.TRANSACTION_KEY_NONCES_EXPONENT;
						} else if(keyOrdinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
							xmssKey.NoncesExponent = WalletProvider.MESSAGE_KEY_NONCES_EXPONENT;
						}

						xmssKey.HashType = keyBytes.Value.hashType;
						xmssKey.BackupHashType = keyBytes.Value.backupHashType;
					}

					return (null, xmssKey, usesEmbededKey);
				}

				this.CentralCoordinator.Log.Debug("Failed to load Key dictionary. Keys were empty.");
			} catch(Exception ex) {
				this.CentralCoordinator.Log.Debug(ex, "Failed to load Key dictionary. Keys were empty.");
			}

			return null;
		}

		/// <summary>
		///     Attempt to validate an envelope using the Key dictionary file
		/// </summary>
		/// <param name="messageEnvelope"></param>
		/// <param name="keyOrdinal"></param>
		/// <returns></returns>
		public async Task<ValidationResult> ValidateEnvelopeKeyDictionary(IPublishedAccountSignature signature, byte keyOrdinal, Func<IXmssCryptographicKey, IPublishedAccountSignature, Task<ValidationResult>> validationCallback, Func<ValidationResult.ValidationResults, EventValidationErrorCode, ValidationResult> resultCreation) {

			if(!this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.KeyDictionaryEnabled(keyOrdinal)) {
				return null;
			}

			// see if we can get a key
			(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)? keyResults = await this.RebuildXmssKeyDictionary(signature, keyOrdinal, resultCreation).ConfigureAwait(false);

			if(!keyResults.HasValue) {
				return null;
			}

			if(keyResults.Value.result != null) {
				return keyResults.Value.result;
			}

			if(keyResults.Value.xmssKey == null) {
				return null;
			}

			// ok we got a key, lets go forward
			return await validationCallback(keyResults.Value.xmssKey, signature).ConfigureAwait(false);

		}

		public Task<(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)?> RebuildTransactionXmssKeyDictionary(IPublishedAccountSignature signature, byte keyOrdinal) {

			return this.RebuildXmssKeyDictionary(signature, keyOrdinal, this.CreateTransactionValidationResult);
		}

		/// <summary>
		///     valida joint transactions to ensure that their signatues match their expected type
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateJointTransactionTypes(ITransactionEnvelope transactionEnvelope) {

			if(transactionEnvelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {
				if(transactionEnvelope.Contents.RehydratedEvent is IJointTransaction<IThreeWayJointSignatureType>) {

					// we need exactly 3 signatures
					if((jointEnvelopeSignature.AccountSignatures.Count != 3) || jointEnvelopeSignature.AccountSignatures.Any(e => e.Autograph.IsZero)) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_COUNT);
					}

					if(transactionEnvelope.Contents.RehydratedEvent is IThreeWayGatedTransaction threeWayGatedTransaction) {

						// we need to match these accounts perfectly
						if(!jointEnvelopeSignature.AccountSignatures.All(e => threeWayGatedTransaction.ImpactedAccounts.Contains(e.KeyAddress.AccountId))) {
							return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTS);
						}
					}
				}

				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.JOINT_TRANSACTION_SINGLE_SIGNATURE);
		}

		protected virtual async Task<ValidationResult> PerformBasicTransactionValidation(ITransaction transaction, ITransactionEnvelope envelope, bool? accreditationCertificateValid) {

			bool validCertificate = accreditationCertificateValid.HasValue && accreditationCertificateValid.Value;

			// some transaction types can not be more than one a second
			if(!validCertificate && transaction is IRateLimitedTransaction && (transaction.TransactionId.Scope != 0)) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE);
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Once we have all the pieces, perform the actual synchronous validation
		/// </summary>
		/// <param name="block"></param>
		/// <param name="hash"></param>
		/// <param name="previousBlockHash"></param>
		/// <returns></returns>
		protected async Task<ValidationResult> ValidateBlock(ISimpleBlock block, bool gossipOrigin, SafeArrayHandle hash, SafeArrayHandle previousBlockHash) {

			SafeArrayHandle usablePreviousBlockHash = previousBlockHash;

			long? loadBlockId = null;
			LockContext lockContext = null;

			// make sure we always run this check atomically, while we are 100% that an insert/interpret is not happening at the same time (chain sync vs gossip insert competition)
			BlockValidationResult results = await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PerformAtomicChainHeightOperation(async lc => {

				long diskBlockHeight = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;

				if((diskBlockHeight + 1) == block.BlockId.Value) {
					// ok, its the last block, we just use the previous hash
					return null;
				}

				if(diskBlockHeight >= block.BlockId.Value) {
					loadBlockId = block.BlockId.Value;

					// its a previous block that we already have, we can still validate it. let's load the hash from disk.

					return null;
				}

				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.CantValidate, BlockValidationErrorCodes.Instance.LAST_BLOCK_HEIGHT_INVALID);
			}, lockContext).ConfigureAwait(false);

			if(results != null) {
				return results;
			}

			if(loadBlockId.HasValue) {
				usablePreviousBlockHash = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockHash(loadBlockId.Value);
			}

			bool hashValid = hash.Equals(BlockchainHashingUtils.GenerateBlockHash(block, usablePreviousBlockHash));

			if(hashValid == false) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
			}

			// ensure that a valid key is being used
			if(!this.ValidateBlockKeyTree(block.SignatureSet.ModeratorKeyOrdinal)) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_DIGEST_KEY);
			}

			// ok, check the signature
			// first thing, get the key from our chain state
			var entry = await this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKeyAndIndex(block.SignatureSet.ModeratorKeyOrdinal).ConfigureAwait(false);

			var blockSignatureSignature = block.SignatureSet.BlockSignature;

			// simply use it as is
			if(entry.key.IsEmpty) {
				//moderatorKey.Dispose();
				//TODO: should we check for an invalid key here?  we could reload it form the block, but how to know which block updated the key last?
				throw new ApplicationException("invalid moderator key");
			}

			// make sure that the key is at least higher than the expect key
			if(block.BlockId > 2 && blockSignatureSignature.KeyUseIndex < entry.keyIndex) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_KEY_SEQUENCE);
			}

			// thats it :)
			return await this.ValidateBlockSignature(hash, block, entry.key).ConfigureAwait(false);

		}

		/// <summary>
		///     if the key was corrupt in the chain state, for whatever reason, we try to get it again from the last saved block
		/// </summary>
		/// <returns></returns>

		// private ICryptographicKey ReloadModeratorSequentialKey() {
		//
		// 	ICryptographicKey key = null;
		//
		// 	Repeater.Repeat(() => {
		// 		IBlock block = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadLatestBlock();
		//
		// 		if(block == null) {
		// 			throw new ApplicationException("Failed to load previous block");
		// 		}
		//
		// 		key = block.SignatureSet.ConvertToSecretKey();
		// 	});
		//
		// 	return key;
		// }
		protected async Task<ValidationResult> ValidateGenesisBlock(IGenesisBlock block, SafeArrayHandle hash) {

			using(SafeArrayHandle newHash = BlockchainHashingUtils.GenerateGenesisBlockHash(block)) {
				bool hashValid = hash.Equals(newHash);

				if(hashValid != true) {
					return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
				}
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected virtual async Task<ValidationResult> ValidateGenesisBlockOnlineVerification(IGenesisBlock genesisBlock, SafeArrayHandle hash) {
			// ok, at this point, the signer is who he/she says he/she is. now we confirm the transaction signature
			//for a genesisModeratorAccountPresentation transaction, thats all we verify

			//lets compare the hashes we fetch from the official website
			if(!this.CentralCoordinator.ChainSettings.SkipGenesisHashVerification) {

				try {
					//TODO: here we validate the hash from http file here
					IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

					string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;

					this.CentralCoordinator.Log.Information("Validating Genesis block hash against the official reference genesis hash file.");

					(SafeArrayHandle sha2, SafeArrayHandle sha3)? genesis = await fetchingService.FetchGenesisHash(hashUrl, this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GenesisFolderPath, genesisBlock.Name).ConfigureAwait(false);

					if(!genesis.HasValue) {
						this.CentralCoordinator.Log.Fatal("Official reference Genesis block hash file could not be acquired!");

						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_PTAH_HASH_VERIFICATION_FAILED);
					}

					(SafeArrayHandle genesisSha2, SafeArrayHandle genesisSha3) = BlockchainDoubleHasher.GetCombinedHash(genesisBlock, genesis.Value.sha2, genesis.Value.sha3);
					bool result = BlockchainDoubleHasher.VerifyGenesisHash(genesisBlock, genesis.Value.sha2, genesis.Value.sha3);

					string sha264 = genesis.Value.sha2.Entry.ToBase58();
					string sha364 = genesis.Value.sha3.Entry.ToBase58();

					string genesisSha264 = genesisSha2.Entry.ToBase58();
					string genesisSha364 = genesisSha3.Entry.ToBase58();

					genesis.Value.sha2.Return();
					genesis.Value.sha3.Return();

					genesisSha2.Return();
					genesisSha3.Return();

					if(!result) {
						this.CentralCoordinator.Log.Fatal($"Genesis block hash (SHA2_512 '{genesisSha264}' and SHA3_512 '{genesisSha364}') has been verified against the official reference hashes (SHA2_512 '{sha264}' and SHA3_512 '{sha364}') and has been found to be invalid!");

						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_PTAH_HASH_VERIFICATION_FAILED);
					}

					this.CentralCoordinator.Log.Information($"Genesis block hash (SHA2_512 '{genesisSha264}' and SHA3_512 '{genesisSha364}') has been verified against the official reference hashes (SHA2_512 '{sha264}' and SHA3_512 '{sha364}') and has been found to be valid.");

				} catch(Exception ex) {
					this.CentralCoordinator.Log.Error(ex, "Failed to query and verify genesis verification Hash.");

					return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_PTAH_HASH_VERIFICATION_FAILED);
				}
			} else {
				this.CentralCoordinator.Log.Warning("Skipping Genesis block official reference hash file verification.");

			}

			return await this.ValidateGenesisBlock(genesisBlock, hash).ConfigureAwait(false);
		}

		protected async Task<bool> CheckKeyGate(KeyAddress keyAddress) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableKeyGates) {

				var gateIndexLock = await this.GetKeyGate(keyAddress.AccountId, keyAddress.OrdinalId).ConfigureAwait(false);

				if(keyAddress.KeyUseIndex <= gateIndexLock) {
					// this key is invalid
					return false;
				}
			}

			return true;
		}

		/// <summary>
		///     Load a single key from the blockchain files
		/// </summary>
		/// <param name="keyAddress"></param>
		/// <returns></returns>
		protected async Task<ICryptographicKey> GetAccountKey(KeyAddress keyAddress) {

			if(await this.CheckKeyGate(keyAddress).ConfigureAwait(false)) {
				return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadFullKey(keyAddress);
			}

			throw new ApplicationException("Invalid key gate");
		}

		/// <summary>
		///     Load multiple keys form the blockchain files
		/// </summary>
		/// <param name="keyAddresses"></param>
		/// <returns></returns>
		protected async Task<Dictionary<AccountId, ICryptographicKey>> GetAccountKeys(List<KeyAddress> keyAddresses) {

			foreach(var keyAddress in keyAddresses) {
				if(!await this.CheckKeyGate(keyAddress).ConfigureAwait(false)) {
					throw new ApplicationException("Invalid key gate");
				}
			}

			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadFullKeys(keyAddresses);

		}

		/// <summary>
		/// ensure to process all transaction types here
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		/// <param name="transaction"></param>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateTransactionTypes(ITransactionEnvelope transactionEnvelope, ITransaction transaction, bool gossipOrigin, LockContext lockContext) {
			ValidationResult result = new ValidationResult(ValidationResult.ValidationResults.Valid);

			if(result.Valid && !transaction.TransactionId.Account.IsValid) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ACCOUNT);
			}

			if(result.Valid && transaction is IStandardPresentationTransaction presentationTransaction && transactionEnvelope is IPresentationTransactionEnvelope presentationTransactionEnvelope) {

				result = await this.ValidatePresentationTransaction(presentationTransactionEnvelope, presentationTransaction, gossipOrigin, lockContext).ConfigureAwait(false);
			}

			if(result.Valid && transaction is IJointPresentationTransaction jointPresentationTransaction) {
				// lets do a special validation first, but it will go through the usual after
				result = this.ValidateJointPresentationTransaction(transactionEnvelope, jointPresentationTransaction, gossipOrigin);
			}

			if(result.Valid && transaction is IStandardAccountKeyChangeTransaction keyChangeTransaction) {
				// lets do a special validation first, but it will go through the usual after
				result = this.ValidateKeyChangeTransaction(transactionEnvelope, keyChangeTransaction, lockContext);
			}

			if(result.Valid && transaction is ISetAccountRecoveryTransaction accountRecoveryTransaction) {
				// lets do a special validation first, but it will go through the usual after
				if(accountRecoveryTransaction.Operation == SetAccountRecoveryTransaction.OperationTypes.Create && (accountRecoveryTransaction.AccountRecoveryHash == null || accountRecoveryTransaction.AccountRecoveryHash.IsZero || accountRecoveryTransaction.AccountRecoveryHash.Length > 200)) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				} else if(accountRecoveryTransaction.Operation == SetAccountRecoveryTransaction.OperationTypes.Revoke && accountRecoveryTransaction.AccountRecoveryHash != null) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				} else {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}
			}

			if(result.Valid && transaction is IGatedTransaction gatedTransaction) {
				if(gatedTransaction.SenderAccountId != gatedTransaction.TransactionId.Account) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(gatedTransaction.CorrelationId == 0) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}
			}

			if(result.Valid && transaction is IThreeWayGatedTransaction threeWayGatedTransaction) {
				// lets do a special validation first, but it will go through the usual after
				if(!threeWayGatedTransaction.VerifierAccountId.IsValid || threeWayGatedTransaction.VerifierAccountId == threeWayGatedTransaction.TransactionId.Account || threeWayGatedTransaction.VerifierAccountId == threeWayGatedTransaction.ReceiverAccountId) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(!threeWayGatedTransaction.ReceiverAccountId.IsValid || threeWayGatedTransaction.ReceiverAccountId == threeWayGatedTransaction.TransactionId.Account) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(threeWayGatedTransaction.Duration == 0) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}
			}

			if(result.Valid && transaction is IGatedJudgementTransaction gatedJudgementTransaction) {
				// lets do a special validation first, but it will go through the usual after
				if(!gatedJudgementTransaction.VerifierAccountId.IsValid || gatedJudgementTransaction.VerifierAccountId == gatedJudgementTransaction.SenderAccountId || gatedJudgementTransaction.VerifierAccountId == gatedJudgementTransaction.ReceiverAccountId) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(!gatedJudgementTransaction.SenderAccountId.IsValid || gatedJudgementTransaction.SenderAccountId == gatedJudgementTransaction.ReceiverAccountId) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(!gatedJudgementTransaction.ReceiverAccountId.IsValid) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}

				if(gatedJudgementTransaction.Judgement != GatedJudgementTransaction.GatedJudgements.Accepted && gatedJudgementTransaction.Judgement != GatedJudgementTransaction.GatedJudgements.Rejected) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
				}
			}

			return result;
		}

		protected virtual async Task<ValidationResult> ValidatePresentationTransaction(IPresentationTransactionEnvelope envelope, IStandardPresentationTransaction transaction, bool gossipOrigin, LockContext lockContext) {

			
			// ok, let's check the THS
			//TODO: this should be done asynchronously. its too time expensive. return a routed task and continue on the other side.
			ValidationResult result = new ValidationResult(ValidationResult.ValidationResults.Valid);

			bool confirmationCode = envelope.ConfirmationCode.HasValue && envelope.ConfirmationCode.Value != 0;

			if(transaction.AccountType == Enums.AccountTypes.User && !confirmationCode) {
#if(!COLORADO_EXCLUSION)
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.USER_ACCOUNT_PRESENTATION_NO_APPOINTMENT_CODE);
#endif
			}

			bool isServer = transaction.AccountType == Enums.AccountTypes.Server || transaction.IsServer;

			if(isServer || ((transaction.AccountType == Enums.AccountTypes.User && (!this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.DisableWebRegUserPresentationTHS || gossipOrigin)))) {

				THSRulesSetDescriptor rulesSetDescriptor = null;

				if(TestingUtil.Testing) {
					if(envelope.THSEnvelopeSignature.RuleSet != THSRulesSet.TestRuleset) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_RULESET);
					}

					rulesSetDescriptor = THSRulesSet.TestRulesetDescriptor;
				} else if(isServer) {
					if(envelope.THSEnvelopeSignature.RuleSet != THSRulesSet.ServerPresentationDefaultRulesSet) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_RULESET);
					}

					rulesSetDescriptor = THSRulesSet.ServerPresentationDefaultRulesSetDescriptor;
				} else if(transaction.AccountType == Enums.AccountTypes.User) {
					if(envelope.THSEnvelopeSignature.RuleSet != THSRulesSet.PresentationDefaultRulesSet) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_RULESET);
					}

					rulesSetDescriptor = THSRulesSet.PresentationDefaultRulesSetDescriptor;
				}

				TimeSpan acceptableRange = envelope.GetExpirationSpan() + TimeSpan.FromHours(1);
				result = await this.ValidateProvedEnvelope(transaction.TransactionId.Timestamp,acceptableRange, false, BlockchainHashingUtils.GenerateTHSHash(envelope), envelope, rulesSetDescriptor, lockContext).ConfigureAwait(false);

				if(result.Invalid) {
					this.CentralCoordinator.Log.Warning("Presentation transaction failed THS verification");

					return result;
				}
			}
#if(!COLORADO_EXCLUSION)
			if(confirmationCode && (envelope.IdentityAutograph == null || envelope.IdentityAutograph.IsZero)) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.IDENTITY_AUTOGRAPHN_NOT_SET);
			}
#endif

			// validate the keys
			result = this.ValidateAccountKey(transaction.TransactionCryptographicKey, transaction);

			if(result.Invalid) {
				return result;
			}

			result = this.ValidateAccountKey(transaction.MessageCryptographicKey, transaction);

			if(result.Invalid) {
				return result;
			}

			result = this.ValidateAccountKey(transaction.ChangeCryptographicKey, transaction);

			if(result.Invalid) {
				return result;
			}

			result = this.ValidateAccountKey(transaction.SuperCryptographicKey, transaction);

			if(result.Invalid) {
				return result;
			}

			return result;
		}

		protected virtual ValidationResult ValidateJointPresentationTransaction(ITransactionEnvelope envelope, IJointPresentationTransaction transaction, bool gossipOrigin) {

			if(envelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {

				// check that the signatures match the declared accounts
				if(jointEnvelopeSignature.AccountSignatures.Count < transaction.RequiredSignatureCount) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT);
				}

				// check required ones
				List<AccountId> signatureAccounts = jointEnvelopeSignature.AccountSignatures.Select(a => a.KeyAddress.DeclarationTransactionId.Account).ToList();
				List<AccountId> requiredSignatures = transaction.MemberAccounts.Where(a => a.Required).Select(a => a.AccountId.ToAccountId()).ToList();
				List<AccountId> allAccounts = transaction.MemberAccounts.Select(a => a.AccountId.ToAccountId()).ToList();

				if(!requiredSignatures.All(a => signatureAccounts.Contains(a))) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTs);
				}

				// if any are not in the signatures list, we fail
				if(signatureAccounts.Any(s => !allAccounts.Contains(s))) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTs);
				}
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected virtual ValidationResult ValidateKeyChangeTransaction(ITransactionEnvelope transactionEnvelope, IStandardAccountKeyChangeTransaction transaction, LockContext lockContext) {

			var result = this.ValidateAccountKey(transaction.NewCryptographicKey, transaction);

			if(result.Invalid) {
				return result;
			}

			return result;
		}

		/// <summary>
		/// validate an account key for essential parameters
		/// </summary>
		/// <param name="key"></param>
		/// <param name="transaction"></param>
		/// <returns></returns>
		public ValidationResult ValidateAccountKey(ICryptographicKey key, ITransaction transaction) {

			var accountId = transaction.TransactionId.Account;

			Enums.KeyHashType hashType = 0;

			int bits = 0;

			if(key.Ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				bits = WalletProvider.TRANSACTION_KEY_HASH_BITS;
			} else if(key.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				bits = WalletProvider.MESSAGE_KEY_HASH_BITS;
			} else if(key.Ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
				bits = WalletProvider.CHANGE_KEY_HASH_BITS;
			} else if(key.Ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				bits = WalletProvider.SUPER_KEY_HASH_BITS;
			} else if(key.Ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
				bits = WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS;
			}

			bool TestHashBits(Enums.KeyHashType keyHash, int hashBits) {

				var result = ((int) keyHash & Enums.HASH512) == 0;

				return hashBits == 512 ? !result : result;
			}

			bool TestKeyHashBits(IXmssCryptographicKey key2, int hashBits) {
				return TestHashBits(key2.HashType, hashBits) && TestHashBits(key2.BackupHashType, hashBits);
			}

			if(key.PublicKey == null || key.PublicKey.IsZero) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_BYTES);
			}

			if(key is IXmssmtCryptographicKey xmssmtCryptographicKey) {

				//TODO: its not urgent since no transaction use this yet, but this should be added eventually
			} else if(key is IXmssCryptographicKey xmssCryptographicKey) {
				if(key.Ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID || key.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID || key.Ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID || key.Ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID || (accountId.IsServer && key.Ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID)) {

					if(xmssCryptographicKey.TreeHeight < WalletProvider.MINIMAL_XMSS_KEY_HEIGHT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_MINIMUM_SIZE);
					}

					if(xmssCryptographicKey.UseIndex < 0) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ENTRY);
					}

					if(!TestKeyHashBits(xmssCryptographicKey, bits)) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_BITS);
					}

					if((bits == 256 && xmssCryptographicKey.PublicKey.Length != XMSSPublicKey.PUBLIC_KEY_SIZE_256) || (bits == 512 && xmssCryptographicKey.PublicKey.Length != XMSSPublicKey.PUBLIC_KEY_SIZE_512)) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_BYTE_SIZE);
					}

					if(xmssCryptographicKey.NoncesExponent <= 0 || xmssCryptographicKey.NoncesExponent > xmssCryptographicKey.TreeHeight) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					}

					if(key.Ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID && xmssCryptographicKey.NoncesExponent != WalletProvider.TRANSACTION_KEY_NONCES_EXPONENT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					} else if(key.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID && xmssCryptographicKey.NoncesExponent != WalletProvider.MESSAGE_KEY_NONCES_EXPONENT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					} else if(key.Ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID && xmssCryptographicKey.NoncesExponent != WalletProvider.MESSAGE_KEY_NONCES_EXPONENT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					} else if(key.Ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID && xmssCryptographicKey.NoncesExponent != WalletProvider.SUPER_KEY_NONCES_EXPONENT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					} else if(key.Ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID && xmssCryptographicKey.NoncesExponent != WalletProvider.VALIDATOR_SIGNATURE_NONCES_EXPONENT) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_NONCES_EXPONENT);
					}
				} else {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE);
				}
			} else if(key is INTRUPrimeCryptographicKey ntruPrimeCryptographicKey) {

				if(accountId.IsServer && key.Ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
					if(ntruPrimeCryptographicKey.Strength != NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761 && ntruPrimeCryptographicKey.Strength != NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_857) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_STRENGTH);
					}

					if(ntruPrimeCryptographicKey.PublicKey.Length != new NTRUPrimeApiParameters(ntruPrimeCryptographicKey.Strength).PublicKeyBytes) {
						return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_BYTE_SIZE);
					}
				} else {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE);
				}
			} else {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE);
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected async Task<ValidationResult> ValidateProvedEnvelope(TransactionTimestamp timestamp, TimeSpan acceptableTimeRange, bool addExpectedTHSBuffer, SafeArrayHandle hash, ITHSEnvelope thsEnvelope, THSRulesSetDescriptor rulesSetDescriptor, LockContext lockContext) {

			if(!this.enableTHSVerification) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
			}
			
			if(!this.timeService.WithinThsAcceptableRange(timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception, acceptableTimeRange, addExpectedTHSBuffer, rulesSetDescriptor)) {
				return this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE);
			}
			
			using(var state = await this.thsLocker.LockAsync(lockContext).ConfigureAwait(false)) {

				if(thsEnvelope.THSEnvelopeSignatureBase.Solution == null || thsEnvelope.THSEnvelopeSignatureBase.Solution.IsEmpty) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_SOLUTION);
				}

				if(thsEnvelope.THSEnvelopeSignatureBase.Solution.Solutions.Count > rulesSetDescriptor.MaxRounds) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_TOO_MANY_SOLUTIONS);
				}
				
				using THSEngine thsEngine = new THSEngine(thsEnvelope.THSEnvelopeSignatureBase.RuleSet, rulesSetDescriptor, GlobalSettings.ApplicationSettings.THSMemoryType);
				await thsEngine.Initialize(THSEngine.THSModes.Verify).ConfigureAwait(false);

				return await thsEngine.Verify(hash, thsEnvelope.THSEnvelopeSignatureBase.Solution).ConfigureAwait(false) == false ? this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_THS_SOLUTION) : this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
			}
		}

		/// <summary>
		///     Validate a group of signatures. any fails, all fail.
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateMultipleSignatures(SafeArrayHandle hash, ITransactionEnvelope envelope, ITransaction transaction, List<IPublishedAccountSignature> signatures, Dictionary<AccountId, ICryptographicKey> keys) {
			// validate the secret nonce with the published key, if it matches the promise.

			foreach(IPublishedAccountSignature signature in signatures) {

				AccountId accountId = signature.KeyAddress.AccountId;

				if(!keys.ContainsKey(accountId)) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_KEY_ACCOUNT);
				}

				ICryptographicKey key = keys[accountId];

				SafeArrayHandle activeHash = hash;

				if(accountId != transaction.TransactionId.Account) {
					// ok, this is not the main emitter. we have to hash with it's key index
					activeHash = BlockchainHashingUtils.GenerateEnvelopedTransactionHash(envelope, accountId);
				}

				// here we must rehash the transaction, with the keyuseindex UNLESS it is the emitter of the transaction

				//TODO: can joint accounts have public signatures?  i think not...
				//				if(signature is IPromisedSecretBlockAccountSignature secretAccountSignature) {
				//					if(key is ISecretCryptographicKey secretCryptographicKey) {
				//						result = this.ValidateSecretSignature(hash, secretAccountSignature, secretCryptographicKey);
				//					} else {
				//						return false;
				//					}
				//				} else {
				//					result = this.ValidateSingleSignature(hash, signature, key);
				//				}
				ValidationResult result = await this.ValidateSingleSignature(activeHash, signature, key).ConfigureAwait(false);

				if(result != ValidationResult.ValidationResults.Valid) {
					return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE);
				}
			}

			return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Validate a secret key signature
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateSecretSignature(SafeArrayHandle hash, IPromisedSecretAccountSignature signature, ISecretDoubleCryptographicKey key) {
			// validate the secret nonce with the published key, if it matches the promise.

			(SafeArrayHandle sha2, SafeArrayHandle sha3) hashedKey = HashingUtils.HashSecretKey(signature.PromisedPublicKey);

			// make sure they match as promised
			if(!hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		/// <summary>
		///     Validate a secret key signature
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateSecretComboSignature(SafeArrayHandle hash, IPromisedSecretComboAccountSignature signature, ISecretComboCryptographicKey key) {
			// validate the secret nonce with the published key, if it matches the promise.

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(signature.Autograph);

			SafeArrayHandle signature1 = (SafeArrayHandle) rehydrator.ReadNonNullableArray();
			SafeArrayHandle signature2 = (SafeArrayHandle) rehydrator.ReadNonNullableArray();

			(SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) hashedKey = HashingUtils.HashSecretComboKey(signature.PromisedPublicKey, signature.PromisedNonce1, signature.PromisedNonce2);

			// make sure they match as promised
			if((key.NonceHash != hashedKey.nonceHash) || !hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);

		}

		protected virtual async Task<ValidationResult> ValidateBlockchainMessageSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			if(key.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashType bitSize = xmssCryptographicKey.HashType;

					if(!((bitSize == Enums.KeyHashType.SHA2_256) || (bitSize == Enums.KeyHashType.SHA3_256))) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		protected virtual async Task<ValidationResult> ValidateBlockchainMessageValidatorSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			if(key.Ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashType bitSize = xmssCryptographicKey.HashType;

					if(!((bitSize == Enums.KeyHashType.SHA2_512) || (bitSize == Enums.KeyHashType.SHA3_512))) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		protected virtual async Task<ValidationResult> ValidateModeratorBlockchainMessageSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			if(key.Ordinal == GlobalsService.MODERATOR_GOSSIP_KEY_ID) {
				if(!(key is IXmssCryptographicKey)) {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MODERATOR_MESSAGE_XMSS_KEY_TYPE);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		protected virtual async Task<ValidationResult> ValidateTransactionSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			if(signature.Autograph == null || signature.Autograph.IsZero) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_AUTOGRAPH);
			}

			ValidationResult result = this.ValidateTransactionKey(key);

			if(result.Invalid) {
				return result;
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		protected virtual async Task<ValidationResult> ValidateTransactionMultipleSignatures(SafeArrayHandle hash, ITransactionEnvelope envelope, ITransaction transaction, List<IPublishedAccountSignature> signatures, Dictionary<AccountId, ICryptographicKey> keys) {

			if(signatures.Any(s => s.Autograph == null || s.Autograph.IsZero)) {
				return this.CreateTransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_AUTOGRAPH);
			}

			foreach(ICryptographicKey key in keys.Values) {
				ValidationResult result = this.ValidateTransactionKey(key);

				if(result.Invalid) {
					return result;
				}
			}

			return await this.ValidateMultipleSignatures(hash, envelope, transaction, signatures, keys).ConfigureAwait(false);
		}

		protected virtual ValidationResult ValidateTransactionKey(ICryptographicKey key) {

			bool CheckKeyBits(Enums.KeyHashType bitSize, int KEY_BIT_SIZE) {
				bool bit512Set = bitSize.HasFlag((Enums.KeyHashType) Enums.HASH512);

				return ((KEY_BIT_SIZE == 256 && !bit512Set) || (KEY_BIT_SIZE == 512 && bit512Set));
			}

			if(key.Ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashType bitSize = xmssCryptographicKey.HashType;

					if(!CheckKeyBits(xmssCryptographicKey.HashType, WalletProvider.TRANSACTION_KEY_HASH_BITS)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_TYPE);
				}
			} else if(key.Ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {

					if(!CheckKeyBits(xmssCryptographicKey.HashType, WalletProvider.MESSAGE_KEY_HASH_BITS)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_TYPE);
				}
			} else if(key.Ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {

					if(!CheckKeyBits(xmssCryptographicKey.HashType, WalletProvider.CHANGE_KEY_HASH_BITS)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_TYPE);
				}
			} else if(key.Ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					// all good
					if(!CheckKeyBits(xmssCryptographicKey.HashType, WalletProvider.SUPER_KEY_HASH_BITS)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SUPERKEY_KEY_TYPE);
				}
			} else if(key.Ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {

					if(!CheckKeyBits(xmssCryptographicKey.HashType, WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE);
				}
			} else if(key.Ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
				if(key is INTRUPrimeCryptographicKey ntruPrimeCryptographicKey) {
					// ok
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_KEY_TYPE);
			}

			return new TransactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Validate a single signature
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		protected virtual async Task<ValidationResult> ValidateSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			SignatureProviderBase provider = null;

			using SafeArrayHandle publicKey = key.PublicKey.Branch();

			switch(key) {
				case IXmssmtCryptographicKey xmssmtCryptographicKey:
					using(provider = new XMSSMTProvider(xmssmtCryptographicKey.HashType, xmssmtCryptographicKey.BackupHashType, xmssmtCryptographicKey.TreeHeight, xmssmtCryptographicKey.TreeLayers, GlobalSettings.ApplicationSettings.XmssThreadMode, xmssmtCryptographicKey.NoncesExponent)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

				case IXmssCryptographicKey xmssCryptographicKey:
					using(provider = new XMSSProvider(xmssCryptographicKey.HashType, xmssCryptographicKey.BackupHashType, xmssCryptographicKey.TreeHeight, GlobalSettings.ApplicationSettings.XmssThreadMode, xmssCryptographicKey.NoncesExponent)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

				// case SecretDoubleCryptographicKey secretCryptographicKey:
				//
				// 	if(signature is IFirstAccountKey firstAccountKey) {
				// 		// ok, for first account key, we have no original key to reffer to, so we use the public key published
				// 		publicKey = firstAccountKey.PublicKey;
				//
				// 		using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
				// 			provider.Initialize();
				//
				// 			bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);
				//
				// 			if(result == false) {
				// 				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				// 			}
				// 		}
				// 	} else if(signature is IPromisedSecretAccountSignature secretAccountSignature) {
				// 		// ok, for secret accounts, we verified that it matched the promise, so we can used the provided public key
				// 		publicKey = secretAccountSignature.PromisedPublicKey;
				//
				// 		using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(signature.Autograph);
				//
				// 		SafeArrayHandle signature1 = rehydrator.ReadNonNullableArray();
				// 		SafeArrayHandle signature2 = rehydrator.ReadNonNullableArray();
				//
				// 		using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
				// 			provider.Initialize();
				//
				// 			bool result = await provider.Verify(hash, signature1, publicKey).ConfigureAwait(false);
				//
				// 			if(result == false) {
				// 				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				// 			}
				//
				// 			// second signature
				// 			result = await provider.Verify(hash, signature2, secretCryptographicKey.SecondKey.Key).ConfigureAwait(false);
				//
				// 			if(result == false) {
				// 				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				// 			}
				// 		}
				//
				// 	} else {
				// 		return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE);
				// 	}
				//
				// 	break;
				//
				// case SecretCryptographicKey secretCryptographicKey:
				// 	provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory);
				//
				// 	using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
				// 		provider.Initialize();
				//
				// 		bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);
				//
				// 		if(result == false) {
				// 			return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				// 		}
				// 	}
				//
				// 	break;
				// case QTeslaCryptographicKey qTeslaCryptographicKey:
				// 	provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory);
				//
				// 	using(provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory)) {
				// 		provider.Initialize();
				//
				// 		bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);
				//
				// 		if(result == false) {
				// 			return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				// 		}
				// 	}
				//
				// 	break;

				default:

					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_KEY_TYPE);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Validate a single signature
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		protected virtual async Task<ValidationResult> ValidateBlockSignature(SafeArrayHandle hash, IBlock block, ICryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			if(block.SignatureSet.BlockSignature.IsHashPublished && !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SkipPeriodicBlockHashVerification) {
				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

				string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;
				SafeArrayHandle publishedHash = await fetchingService.FetchBlockPublicHash(hashUrl, block.BlockId.Value).ConfigureAwait(false);

				if(!hash.Equals(publishedHash)) {
					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.FAILED_PUBLISHED_HASH_VALIDATION);
				}
			}

			if(key is IXmssmtCryptographicKey xmssmtCryptographicKey) {

				return await VerifyXmssmtSignature(hash, block.SignatureSet.BlockSignature.Autograph, xmssmtCryptographicKey).ConfigureAwait(false);
			} else if(key is IXmssCryptographicKey xmssCryptographicKey) {

				return await VerifyXmssSignature(hash, block.SignatureSet.BlockSignature.Autograph, xmssCryptographicKey, true).ConfigureAwait(false);
			} else if(key is ITripleXmssCryptographicKey tripleXmssCryptographicKey) {
				return await VerifyTripleXmssSignature(hash, block.SignatureSet.BlockSignature.Autograph, tripleXmssCryptographicKey).ConfigureAwait(false);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_KEY_CORRELATION_TYPE);
		}

		protected async Task<ValidationResult> VerifyXmssSignature(SafeArrayHandle hash, SafeArrayHandle autograph, IXmssCryptographicKey xmssCryptographicKey, bool isBlockVerification = false) {
			using XMSSProvider provider = new XMSSProvider(xmssCryptographicKey.HashType, xmssCryptographicKey.BackupHashType, xmssCryptographicKey.TreeHeight, GlobalSettings.ApplicationSettings.XmssThreadMode, xmssCryptographicKey.NoncesExponent);

			provider.Initialize();

			XMSSSignaturePathCache cache = null;

			if(isBlockVerification) {
				SafeArrayHandle lastBlockXmssKeySignaturePathCache = SafeArrayHandle.CreateClone(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockXmssKeySignaturePathCache);

				if(lastBlockXmssKeySignaturePathCache != null && !lastBlockXmssKeySignaturePathCache.IsZero) {
					cache = new XMSSSignaturePathCache();

					cache.Load(lastBlockXmssKeySignaturePathCache.Entry);
				}
			}

			bool result = await provider.Verify(hash, autograph, xmssCryptographicKey.PublicKey, cache).ConfigureAwait(false);

			if(result == false) {
				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected async Task<ValidationResult> VerifyXmssmtSignature(SafeArrayHandle hash, SafeArrayHandle autograph, IXmssmtCryptographicKey xmssmtCryptographicKey) {
			using XMSSMTProvider provider = new XMSSMTProvider(xmssmtCryptographicKey.HashType, xmssmtCryptographicKey.BackupHashType, xmssmtCryptographicKey.TreeHeight, xmssmtCryptographicKey.TreeLayers, GlobalSettings.ApplicationSettings.XmssThreadMode, xmssmtCryptographicKey.NoncesExponent);

			provider.Initialize();

			bool result = await provider.Verify(hash, autograph, xmssmtCryptographicKey.PublicKey).ConfigureAwait(false);

			if(result == false) {
				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected async Task<ValidationResult> VerifyTripleXmssSignature(SafeArrayHandle hash, SafeArrayHandle autographBytes, ITripleXmssCryptographicKey tripleXmssCryptographicKey) {

			var rehydrator = DataSerializationFactory.CreateRehydrator(autographBytes);
			TripleXmssAutograph autograph = new TripleXmssAutograph();
			autograph.Rehydrate(rehydrator);
			var result = await VerifyXmssSignature(hash, autograph.FirstAutograph, tripleXmssCryptographicKey.FirstCryptographicKey).ConfigureAwait(false);

			if(result.Invalid) {
				return result;
			}

			result = await this.VerifyXmssmtSignature(hash, autograph.SecondAutograph, tripleXmssCryptographicKey.SecondCryptographicKey).ConfigureAwait(false);

			if(result.Invalid) {
				return result;
			}

			result = await VerifyXmssmtSignature(hash, autograph.ThirdAutograph, tripleXmssCryptographicKey.ThirdCryptographicKey).ConfigureAwait(false);

			if(result.Invalid) {
				return result;
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected virtual async Task<ValidationResult> ValidateDigestSignature(SafeArrayHandle hash, IPublishedAccountSignature signature, IXmssmtCryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			using XMSSMTProvider provider = new XMSSMTProvider(key.HashType, key.BackupHashType, key.TreeHeight, key.TreeLayers, GlobalSettings.ApplicationSettings.XmssThreadMode, key.NoncesExponent);

			provider.Initialize();

			bool result = await provider.Verify(hash, signature.Autograph, key.PublicKey).ConfigureAwait(false);

			if(result == false) {
				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);

		}

		/// <summary>
		///     validate that the provided key is a permitted one
		/// </summary>
		/// <param name="ordinal"></param>
		/// <returns></returns>
		protected bool ValidateDigestKeyTree(byte ordinal) {
			if(ordinal == GlobalsService.MODERATOR_DIGEST_BLOCKS_KEY_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_DIGEST_BLOCKS_CHANGE_KEY_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_PTAH_KEY_ID) {
				return true;
			}

			return false;

		}

		protected bool ValidateBlockKeyTree(byte ordinal) {
			if(ordinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_BLOCKS_CHANGE_KEY_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_SUPER_CHANGE_KEY_ID) {
				return true;
			}

			if(ordinal == GlobalsService.MODERATOR_PTAH_KEY_ID) {
				return true;
			}

			return false;
		}

		protected ValidationResult CreateValidationResult(ValidationResult.ValidationResults result) {
			return new ValidationResult(result);
		}

		protected ValidationResult CreateValidationResult(ValidationResult.ValidationResults result, IEventValidationErrorCodeBase errorCode) {
			return new ValidationResult(result, errorCode);
		}

		protected ValidationResult CreateValidationResult(ValidationResult.ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes) {
			return new ValidationResult(result, errorCodes);
		}

		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, BlockValidationErrorCode errorCode);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, List<BlockValidationErrorCode> errorCodes);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, IEventValidationErrorCodeBase errorCode);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes);

		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, MessageValidationErrorCode errorCode);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, List<MessageValidationErrorCode> errorCodes);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, IEventValidationErrorCodeBase errorCode);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes);

		protected abstract TransactionValidationResult CreateTransactionValidationResult(ValidationResult.ValidationResults result);
		protected abstract TransactionValidationResult CreateTransactionValidationResult(ValidationResult.ValidationResults result, TransactionValidationErrorCode errorCode);
		protected abstract TransactionValidationResult CreateTransactionValidationResult(ValidationResult.ValidationResults result, List<TransactionValidationErrorCode> errorCodes);
		protected abstract TransactionValidationResult CreateTransactionValidationResult(ValidationResult.ValidationResults result, IEventValidationErrorCodeBase errorCode);
		protected abstract TransactionValidationResult CreateTransactionValidationResult(ValidationResult.ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes);

		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, DigestValidationErrorCode errorCode);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, List<DigestValidationErrorCode> errorCodes);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, IEventValidationErrorCodeBase errorCode);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, List<IEventValidationErrorCodeBase> errorCodes);
	}
}