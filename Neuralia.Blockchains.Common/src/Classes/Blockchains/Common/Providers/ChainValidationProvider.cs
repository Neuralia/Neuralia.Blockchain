using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization.Exceptions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.Gated;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Addresses;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Signatures;
using Neuralia.Blockchains.Core.Cryptography.Signatures.QTesla;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IChainValidationProvider: IChainProvider {
		Task ValidateBlock(IDehydratedBlock dehydratedBlock, bool gossipOrigin, Action<ValidationResult> completedResultCallback);
		Task ValidateBlock(IBlock block, bool gossipOrigin, Action<ValidationResult> completedResultCallback);
		Task ValidateTransaction(ITransactionEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback);
		Task ValidateBlockchainMessage(IMessageEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback);

		Task<ValidationResult> ValidateTransactionFastKey(ITransactionEnvelope envelope, byte keyOrdinal);
		Task<ValidationResult> ValidateBlockchainMessageFastKey(IMessageEnvelope envelope, byte keyOrdinal);
		Task<ValidationResult> ValidateSignatureFastKey(AccountId accountId, ByteArray message, ByteArray autograph, byte keyOrdinal);

		Task<ValidationResult> ValidateDigest(IBlockchainDigest digest, bool verifyFiles);

		Task ValidateEnvelopedContent(IEnvelope envelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback);
	}

	public interface IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainValidationProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}
	

	public abstract class ChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     How many cache entries to keep
		/// </summary>
		protected const int DEFAULT_CACHE_COUNT = 5;

		private readonly IGuidService guidService;

		private readonly IBlockchainTimeService timeService;
		
		protected readonly CENTRAL_COORDINATOR centralCoordinator;

		public ChainValidationProvider(CENTRAL_COORDINATOR centralCoordinator) : base() {
			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.timeService = centralCoordinator.BlockchainServiceSet.BlockchainTimeService;
			this.centralCoordinator = centralCoordinator;
		}

		public CENTRAL_COORDINATOR CentralCoordinator => this.centralCoordinator;
		
		public virtual async Task ValidateEnvelopedContent(IEnvelope envelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback) {

			if(envelope is IBlockEnvelope blockEnvelope) {
				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					throw new ApplicationException("Mobile apps can not validate blocks");
				}

				await this.ValidateBlock(blockEnvelope.Contents, gossipOrigin, completedResultCallback).ConfigureAwait(false);

				return;
			}

			if(envelope is IMessageEnvelope messageEnvelope) {
				await this.ValidateBlockchainMessage(messageEnvelope, gossipOrigin, completedResultCallback).ConfigureAwait(false);

				return;
			}

			if(envelope is ITransactionEnvelope transactionEnvelope) {
				await this.ValidateTransaction(transactionEnvelope, gossipOrigin, completedResultCallback).ConfigureAwait(false);

				return;
			}

			throw new ApplicationException("Invalid envelope type");
		}

		public async Task ValidateBlock(IDehydratedBlock dehydratedBlock, bool gossipOrigin, Action<ValidationResult> completedResultCallback) {

			// lets make sure its rehydrated, we need it fully now

			try {
				dehydratedBlock.RehydrateBlock(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, true);
			} 
			catch(UnrecognizedElementException urex) {
				
				throw;
			}
			catch {
				// just invalid
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

			await this.ValidateBlock(dehydratedBlock.RehydratedBlock, gossipOrigin, completedResultCallback).ConfigureAwait(false);
		}

		public virtual async Task ValidateBlock(IBlock block, bool gossipOrigin, Action<ValidationResult> completedResultCallback) {

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
					} while(result.Invalid && attempt <= 3);

					completedResultCallback(result);
				}

				await PerformBlockValidation(ByteArray.Wrap(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockHash)).ConfigureAwait(false);
			} else {
				throw new ApplicationException("Invalid block type");
			}
		}

		public async Task<(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)?> RebuildXmssFastKey(IPublishedAccountSignature signature, byte keyOrdinal, Func<ValidationResult.ValidationResults, EventValidationErrorCode, ValidationResult> resultCreation) {

			if(!this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FastKeyEnabled(keyOrdinal)) {
				Log.Debug("Fastkeys were not enabled.");
				return null;
			}
			Log.Debug("Fastkeys were enabled.");
			
			bool usesEmbededKey = false;

			// ok, we can take the fast route!
			try {
				var keyBytes = await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadAccountKeyFromIndex(signature.KeyAddress.AccountId, keyOrdinal).ConfigureAwait(false);

				if(keyBytes?.keyBytes != null && !keyBytes.Value.keyBytes.IsZero) {

					IXmssCryptographicKey xmssKey = null;

					if(signature.PublicKey?.Key != null && signature.PublicKey.Key.HasData) {
						// ok, this message has an embeded public key. lets confirm its the same that we pulled up
						if(!signature.PublicKey.Key.Equals(keyBytes.Value.keyBytes)) {
							// ok, we have a discrepancy. they embedded a key that does not match the public record. 
							//TODO: we should log the peer for bad acting here
							return (resultCreation(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID), null, false);
						}

						if(signature.PublicKey is IXmssCryptographicKey xmssPublicKey && signature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight && signature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced) {

							// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case. lets copy the key it has and use it
							xmssKey = new XmssCryptographicKey();

							xmssKey.Id         = xmssPublicKey.Id;
							xmssKey.Key.Entry  = xmssPublicKey.Key.Entry.Clone();
							xmssKey.TreeHeight = xmssPublicKey.TreeHeight;
							xmssKey.BitSize    = xmssPublicKey.BitSize;
							usesEmbededKey     = true;
						}
					}

					if(xmssKey == null) {
						xmssKey = new XmssCryptographicKey();

						xmssKey.Id         = signature.KeyAddress.OrdinalId;
						xmssKey.Key.Entry  = keyBytes.Value.keyBytes.Entry;
						xmssKey.TreeHeight = keyBytes.Value.treeheight;
						xmssKey.BitSize    = keyBytes.Value.hashBits;
					}

					return (null, xmssKey, usesEmbededKey);
				} 
				
				Log.Debug("Failed to load fast keys. Keys were empty.");
			} catch(Exception ex) {
				Log.Debug(ex, "Failed to load fast keys. Keys were empty.");
			}
			
			return null;
		}

		/// <summary>
		/// Attempt to validate an envelope using the fast keys file
		/// </summary>
		/// <param name="messageEnvelope"></param>
		/// <param name="keyOrdinal"></param>
		/// <returns></returns>
		public async Task<ValidationResult> ValidateEnvelopeFastKey(IPublishedAccountSignature signature, byte keyOrdinal, Func<IXmssCryptographicKey, IPublishedAccountSignature, Task<ValidationResult>> validationCallback, Func<ValidationResult.ValidationResults, EventValidationErrorCode, ValidationResult> resultCreation) {

			if(keyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				return null;
			}
			
			// see if we can get a key
			var keyResults = await this.RebuildXmssFastKey(signature, keyOrdinal, resultCreation).ConfigureAwait(false);

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

		public Task<ValidationResult> ValidateBlockchainMessageFastKey(IMessageEnvelope envelope, byte keyOrdinal) {

			if(keyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				return Task.FromResult(new ValidationResult());
			}
			return this.ValidateEnvelopeFastKey(envelope.Signature.AccountSignature, keyOrdinal, (key, signature) => this.ValidateBlockchainMessageSingleSignature(envelope.Hash, signature, key), this.CreateMessageValidationResult);
		}

		public Task<ValidationResult> ValidateTransactionFastKey(ITransactionEnvelope envelope, byte keyOrdinal) {

			if(keyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				return Task.FromResult(new ValidationResult());
			}
			
			if(envelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature) {
				return this.ValidateEnvelopeFastKey(publishedEnvelopeSignature.AccountSignature, keyOrdinal, (key, signature) => this.ValidateTransationSingleSignature(envelope.Hash, signature, key), this.CreateTrasactionValidationResult);
			}

			return Task.FromResult(new ValidationResult());
		}

		/// <summary>
		/// validate an arbitrary message using fast keys
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="message"></param>
		/// <param name="autograph"></param>
		/// <param name="keyOrdinal"></param>
		/// <returns></returns>
		public Task<ValidationResult> ValidateSignatureFastKey(AccountId accountId, ByteArray message, ByteArray autograph, byte keyOrdinal) {

			IPublishedAccountSignature signature = new PublishedAccountSignature();
			signature.Autograph.Entry = autograph;
			signature.KeyAddress.OrdinalId = keyOrdinal;
			signature.KeyAddress.AccountId = accountId;

			return this.ValidateEnvelopeFastKey(signature, keyOrdinal, (key, sig) => this.ValidateSingleSignature(message, sig, key), this.CreateTrasactionValidationResult);
		}

		public Task<(ValidationResult result, IXmssCryptographicKey xmssKey, bool usesEmbededKey)?> RebuildTransactionXmssFastKey(IPublishedAccountSignature signature, byte keyOrdinal) {

			return this.RebuildXmssFastKey(signature, keyOrdinal, this.CreateTrasactionValidationResult);
		}

		public async Task ValidateBlockchainMessage(IMessageEnvelope messageEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback) {

			// lets make sure its rehydrated, we need it fully now

			try {
				messageEnvelope.Contents.RehydrateMessage(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
			}
			catch(UnrecognizedElementException urex) {
				

				throw;
			}
			catch {
				// just invalid
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

			

			// ok,l first lets compare the hashes
			IBlockchainMessage message = messageEnvelope.Contents.RehydratedMessage;
			
			TimeSpan acceptablerange = TimeSpan.FromHours(1);


			//first check the time to ensure we are within the acceptable range
			
#if(!COLORADO_EXCLUSION)
			// multi sig  transactions are excluded from time limit checks
			if(!this.timeService.WithinAcceptableRange(message.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception, acceptablerange)) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

				return;
			}
#endif

			bool hashValid = messageEnvelope.Hash.Equals(HashingUtils.GenerateHash(message));

			if(hashValid != true) {
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.HASH_INVALID));

				return;
			}

			// if the key is ahead of where we are and we are still syncing, we can use the embeded key to make a summary validation, enough to forward a gossip message
			if(GlobalSettings.ApplicationSettings.SynclessMode || messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight && messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced) {

				MessageValidationResult result = new MessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

				// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
				if(messageEnvelope.Signature.AccountSignature.PublicKey?.Key != null) {

					// ok, we can try to validate it using the included key. it does not mean the mssage is absolutely valid, but there may be a certain validity to it.
					ValidationResult includedResults = await this.ValidateBlockchainMessageSingleSignature(messageEnvelope.Hash, messageEnvelope.Signature.AccountSignature, messageEnvelope.Signature.AccountSignature.PublicKey).ConfigureAwait(false);

					if(includedResults == ValidationResult.ValidationResults.Valid) {

						result = this.CreateMessageValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
					}
				}

				// we are not sure, but it passed this test at least	
				completedResultCallback(result);

				return;
			}

			if(GlobalSettings.ApplicationSettings.SynclessMode) {
				// mobile mode can not go any further
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE));

				return;
			}

			if(messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight && messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

				// this doesnt work for us, we can't validate this
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

				return;
			}

			if(messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED));

				return;
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {

				byte keyOrdinal = messageEnvelope.Signature.AccountSignature.KeyAddress.OrdinalId;

				ValidationResult fastKeyResult = await this.ValidateBlockchainMessageFastKey(messageEnvelope, keyOrdinal).ConfigureAwait(false);

				if(fastKeyResult != null) {
					completedResultCallback(fastKeyResult);

					return;
				}
			}

			// now we must get our key.
			try {

				ICryptographicKey key = this.GetAccountKey(messageEnvelope.Signature.AccountSignature.KeyAddress);

				if(messageEnvelope.Signature.AccountSignature.PublicKey?.Key != null) {
					// ok, this message has an embeded public key. lets confirm its the same that we pulled up
					if(!messageEnvelope.Signature.AccountSignature.PublicKey.Key.Equals(key.Key)) {
						// ok, we have a discrepancy. they embedded a key that does not match the public record. 
						//TODO: we should log the peer for bad acting here
						completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

						return;
					}
				}

				// thats it :)
				ValidationResult result = await this.ValidateBlockchainMessageSingleSignature(messageEnvelope.Hash, messageEnvelope.Signature.AccountSignature, key).ConfigureAwait(false);

				completedResultCallback(result);

			} catch(Exception ex) {
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

				//TODO: what to do here?
				Log.Fatal(ex, "Failed to validate message.");

				// this is very critical
				throw;
			}
		}

		public async Task ValidateTransaction(ITransactionEnvelope transactionEnvelope, bool gossipOrigin, Action<ValidationResult> completedResultCallback) {

			var chainStateProvider = this.centralCoordinator.ChainComponentProvider.ChainStateProviderBase;
			var chainConfiguration = this.centralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			
#if(!COLORADO_EXCLUSION)

			// lets make sure the expiration of the envelope is still within the timeframe
			if(transactionEnvelope.GetExpirationTime(this.timeService, chainStateProvider.ChainInception) < DateTime.UtcNow) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EXPIRED));

				return;
			}
#endif
			// lets rehydrate the first level
			try {
				transactionEnvelope.RehydrateContents();
			} 
			catch(UnrecognizedElementException urex) {
				
				throw;
			}
			catch {
				// just invalid
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}

			if(transactionEnvelope.Contents.Uuid == TransactionId.Empty) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_ID));

				return;
			}
			
			// if its gossip and a presentation and its not allowed, we reject. here we check early based on envelope. we will check again later
			if(transactionEnvelope.IsPresentation && gossipOrigin && !chainStateProvider.AllowGossipPresentations && !chainConfiguration.AllowGossipPresentations) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.GOSSIP_PRESENTATION_TRANSACTIONS_NOT_ALLOWED));

				return;
			}

			// make sure the timestamp is not in the future
			DateTime transactionTime = this.timeService.GetTransactionDateTime(transactionEnvelope.Contents.Uuid, chainStateProvider.ChainInception);

			if(transactionTime >= DateTime.UtcNow) {
				// its impossible for a transaction timestamp to be higher than our current time.
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TIMESTAMP));

				return;
			}

			// lets make sure its rehydrated, we need it fully now
			try {
				transactionEnvelope.Contents.RehydrateTransaction(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);
			} 
			catch(UnrecognizedElementException urex) {
				
				throw;
			}
			catch {
				// just invalid
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.INVALID_BYTES));

				return;
			}
			
			// lets make sure the envelope correctly represents the transaction type
			bool isPresentation = transactionEnvelope.Contents.RehydratedTransaction is IPresentation;
						
			if(isPresentation && !transactionEnvelope.IsPresentation || !isPresentation && transactionEnvelope.IsPresentation) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_TYPE_ENVELOPE_REPRESENTATION));

				return;
			}
			
			// if its gossip and a presentation and its not allowed, we reject. we check again now that we know the true type mof the transaction
			if(isPresentation && gossipOrigin && !chainStateProvider.AllowGossipPresentations && !chainConfiguration.AllowGossipPresentations) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.GOSSIP_PRESENTATION_TRANSACTIONS_NOT_ALLOWED));

				return;
			}
			
#if(!COLORADO_EXCLUSION)

			TimeSpan acceptablerange = TimeSpan.FromHours(1);

			if(transactionEnvelope.Signature is IJointEnvelopeSignature || transactionEnvelope.Contents.RehydratedTransaction is IJointTransaction) {
				acceptablerange = TimeSpan.FromDays(1);
			}
			//first check the time to ensure we are within the acceptable range
			if(!this.timeService.WithinAcceptableRange(transactionEnvelope.Contents.RehydratedTransaction.TransactionId.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception, acceptablerange)) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

				return;
			}
#endif

			// ok,l first lets compare the hashes
			ITransaction transaction = transactionEnvelope.Contents.RehydratedTransaction;
			
			bool hashValid = transactionEnvelope.Hash.Equals(BlockchainHashingUtils.GenerateTransactionHash(transactionEnvelope));

			if(hashValid != true) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.HASH_INVALID));

				return;
			}

			// if there is a certificate id provided, lets check it
			bool? accreditationCertificateValid = null;

			if(transactionEnvelope.AccreditationCertificates.Any()) {

				accreditationCertificateValid = await this.CentralCoordinator.ChainComponentProvider.AccreditationCertificateProviderBase.IsAnyTransactionCertificateValid(transactionEnvelope.AccreditationCertificates, transaction.TransactionId, Enums.CertificateApplicationTypes.Envelope).ConfigureAwait(false);
			}

			// perform basic validations
			ValidationResult result = await this.PerformBasicTransactionValidation(transaction, transactionEnvelope, accreditationCertificateValid).ConfigureAwait(false);

			if(result == ValidationResult.ValidationResults.Valid && transaction is IStandardPresentationTransaction presentationTransaction) {

				result = await this.ValidatePresentationTransaction(transactionEnvelope, presentationTransaction).ConfigureAwait(false);

				completedResultCallback(result);

				return;
			}

			if(result == ValidationResult.ValidationResults.Valid && transaction is IJointPresentationTransaction jointPresentationTransaction) {
				// lets do a special validation first, but it will go through the usual after
				result = this.ValidateJointPresentationTransaction(transactionEnvelope, jointPresentationTransaction);
			}

			if(result != ValidationResult.ValidationResults.Valid) {
				completedResultCallback(result);

				return;
			}

			if(transactionEnvelope.Signature is ISingleEnvelopeSignature singleEnvelopeSignature) {

				if(transactionEnvelope.Contents.RehydratedTransaction is IJointTransaction) {
					// this is a mistake, joint transactions MUST have a joint signature
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.JOINT_TRANSACTION_SINGLE_SIGNATURE));

					return;
				}
				
				KeyAddress keyAddress = null;
				IPublishedAccountSignature publishedAccountSignature = null;

				if(transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature) {
					keyAddress = publishedEnvelopeSignature.AccountSignature.KeyAddress;
					publishedAccountSignature = publishedEnvelopeSignature.AccountSignature;
				} else if(transactionEnvelope.Signature is ISecretEnvelopeSignature secretEnvelopeSignature) {
					keyAddress = secretEnvelopeSignature.AccountSignature.KeyAddress;
					publishedAccountSignature = secretEnvelopeSignature.AccountSignature;
				} else {
					throw new ApplicationException("unsupported envelope signature type");
				}

				// if there is an embedded public key, wew can try using it
				if(publishedAccountSignature != null) {
					// if the key is ahead of where we are and we are still syncing, we can use the embeded key to make a summary validation, enough to forward a gossip message
					if(GlobalSettings.ApplicationSettings.SynclessMode || publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight && publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced) {

						TransactionValidationResult embdedKeyResult = new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

						// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
						if(publishedAccountSignature.PublicKey?.Key != null) {

							// ok, we can try to validate it using the included key. it does not mean the mssage is absolutely valid, but there may be a certain validity to it.
							ValidationResult includedResults = await this.ValidateTransationSingleSignature(transactionEnvelope.Hash, publishedAccountSignature, publishedAccountSignature.PublicKey).ConfigureAwait(false);

							if(includedResults == ValidationResult.ValidationResults.Valid) {

								// we are not sure, but it passed this test at least	
								embdedKeyResult = this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
							}
						}

						// we are not sure, but it passed this test at least	
						completedResultCallback(embdedKeyResult);

						return;
					}

					if(publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight && publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

						// this doesnt work for us, we can't validate this
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

						return;
					}
				}

				if(GlobalSettings.ApplicationSettings.SynclessMode) {
					// mobile mode can not go any further
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE));

					return;
				}

				if(publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight) {
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED));

					return;
				}

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex && keyAddress.OrdinalId != GlobalsService.SUPER_KEY_ORDINAL_ID) {
					// ok, we can take the fast route!
	
					ValidationResult fastKeyResult = await this.ValidateTransactionFastKey(transactionEnvelope, keyAddress.OrdinalId).ConfigureAwait(false);

					if(fastKeyResult != null) {
						completedResultCallback(fastKeyResult);

						return;
					}
				}

				try {
					if(result == ValidationResult.ValidationResults.Valid) {
						// now we must get our key. 
						ICryptographicKey key = this.GetAccountKey(keyAddress);

						if(publishedAccountSignature?.PublicKey?.Key != null) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!publishedAccountSignature.PublicKey.Key.Equals(key.Key)) {
								// ok, we have a discrepansy. they embeded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here
								completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

								return;
							}
						}

						// thats it :)
						if(singleEnvelopeSignature is ISecretEnvelopeSignature secretEnvelopeSignature) {

							if(key is ISecretDoubleCryptographicKey secretCryptographicKey) {
								result = await this.ValidateSecretSignature(transactionEnvelope.Hash, secretEnvelopeSignature.AccountSignature, secretCryptographicKey).ConfigureAwait(false);
							}
						} else if(transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature2) {
							result = await this.ValidateTransationSingleSignature(transactionEnvelope.Hash, publishedEnvelopeSignature2.AccountSignature, key).ConfigureAwait(false);
						}

						completedResultCallback(result);
					}
				} catch(Exception ex) {
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

					//TODO: what to do here?
					Log.Fatal(ex, "Failed to validate transaction.");

					// this is very critical
					throw;
				}
				
			} else if(transactionEnvelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {

				if(transactionEnvelope.Contents.RehydratedTransaction is IJointTransaction) {

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
					var jointTransaction = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadMasterTransaction(jointPublishedEnvelopeSignature.Address);

					if(jointTransaction is IJointMembers jointMembersTransction) {

						permittedAccountIds = jointMembersTransction.MemberAccounts.Select(e => e.AccountId.ToAccountId()).ToList();
						requiredAccountIds = jointMembersTransction.MemberAccounts.Where(e => e.Required).Select(e => e.AccountId.ToAccountId()).ToList();
					}
					else {
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_REFERENCED_TRANSACTION));

						return;
					}
				} else{
					// all are required
					permittedAccountIds.AddRange(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress.AccountId));
					requiredAccountIds.AddRange(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress.AccountId));
				}

				// validatde that all included accounts are permitted
				var currentSignatureAccounts = jointEnvelopeSignature.AccountSignatures.Select(e => e.KeyAddress.AccountId).ToList();
				foreach(var accountId in currentSignatureAccounts) {
					// the account must be permitted
					if(!permittedAccountIds.Contains(accountId)) {
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTS));

						return;
					}
				}

				// now make sure all required accounts are included in the envelope
				if(requiredAccountIds.Any(e => !currentSignatureAccounts.Contains(e))) {
					// we have missing required accounts
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MISSING_REQUIRED_JOINT_ACCOUNT));

					return;
				}

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {
					// ok, we can take the fast route!
					//TODO: this is a bit all or nothing here. Some keys may be available as fast, others may not. mix the schemes optimally
					if(jointEnvelopeSignature.AccountSignatures.All(s => this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FastKeyEnabled(s.KeyAddress.OrdinalId))) {
						var keys = new Dictionary<AccountId, ICryptographicKey>();

						bool usesEmbededKey = false;

						foreach(IPublishedAccountSignature signature in jointEnvelopeSignature.AccountSignatures) {
							// see if we can get a key
							var keyResults = await this.RebuildTransactionXmssFastKey(signature, signature.KeyAddress.OrdinalId).ConfigureAwait(false);

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
							
							result = await this.ValidateTransationMultipleSignatures(transactionEnvelope.Hash, transactionEnvelope, transactionEnvelope.Contents.RehydratedTransaction, jointEnvelopeSignature.AccountSignatures, keys).ConfigureAwait(false);

							// if we used any embeded key, we can not fully trust the results
							if(result == ValidationResult.ValidationResults.Valid && usesEmbededKey) {
								result = this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
							}

							completedResultCallback(result);

							return;
						}
					}
				}

				try {
					//.ToDictionary(t => t.Key, t => t.Value.Keyset.Keys[jointEnvelopeSignature.AccountSignatures.Single(s => s.KeyAddress.DeclarationTransactionId.Account == t.Key).KeyAddress.OrdinalId])

					var keys = this.GetAccountKeys(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress).ToList());

					// validate any embeded key to ensure if they were provided, they were right
					foreach((AccountId accountId, ICryptographicKey cryptoKey) in keys) {

						IPublishedAccountSignature publicSignature = jointEnvelopeSignature.AccountSignatures.SingleOrDefault(s => s.KeyAddress.AccountId == accountId);

						if(publicSignature?.PublicKey?.Key != null) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!publicSignature.PublicKey.Key.Entry.Equals(cryptoKey.Key.Entry)) {
								// ok, we have a discrepansy. they embeded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here
								completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

								return;
							}
						}
					}

					// thats it :)
					result = await this.ValidateTransationMultipleSignatures(transactionEnvelope.Hash, transactionEnvelope, transactionEnvelope.Contents.RehydratedTransaction, jointEnvelopeSignature.AccountSignatures, keys).ConfigureAwait(false);

					completedResultCallback(result);

				} catch (Exception ex){
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

					//TODO: what to do here?
					Log.Fatal(ex, "Failed to validate transction.");

					// this is very critical
					throw;
				}
			}
		}

		/// <summary>
		/// valida joint transactions to ensure that their signatues match their expected type
		/// </summary>
		/// <param name="transactionEnvelope"></param>
		/// <returns></returns>
		protected virtual async Task<ValidationResult> ValidateJointTransactionTypes(ITransactionEnvelope transactionEnvelope) {

			if(transactionEnvelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {
				if(transactionEnvelope.Contents.RehydratedTransaction is IJointTransaction<IThreeWayJointSignatureType>) {

					// we need exactly 3 signatures
					if(jointEnvelopeSignature.AccountSignatures.Count != 3 || jointEnvelopeSignature.AccountSignatures.Any(e => e.Autograph.IsZero)) {
						return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_COUNT);
					}

					if(transactionEnvelope.Contents.RehydratedTransaction is IThreeWayGatedTransaction threeWayGatedTransaction) {

						// we need to match these accounts perfectly
						if(!jointEnvelopeSignature.AccountSignatures.All(e => threeWayGatedTransaction.TargetAccounts.Contains(e.KeyAddress.AccountId))) {
							return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTS);
						}
					}
				}

				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);
			} 
			
			return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.JOINT_TRANSACTION_SINGLE_SIGNATURE);
		}
		
		public async Task<ValidationResult> ValidateDigest(IBlockchainDigest digest, bool verifyFiles) {

			// first, we validate the hash itself against the online double hash file
			if(!this.CentralCoordinator.ChainSettings.SkipDigestHashVerification) {

				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

				string digestHashesPath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.DigestHashesPath;
				FileExtensions.EnsureDirectoryStructure(digestHashesPath, this.CentralCoordinator.FileSystem);

				string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;
				(SafeArrayHandle sha2, SafeArrayHandle sha3) genesis = fetchingService.FetchDigestHash(hashUrl, digestHashesPath, digest.DigestId);

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
			HashNodeList descriptorNodes = new HashNodeList();

			foreach(var channel in digest.DigestDescriptor.Channels.OrderBy(f => f.Key)) {

				uint slices = 1;

				if(channel.Value.GroupSize > 0) {
					slices = (uint) Math.Ceiling((double) channel.Value.LastEntryId / channel.Value.GroupSize);
				}

				var cascadingHashSets = new Dictionary<(int index, int file), HashNodeList>();

				// prepare our hashing nodeAddressInfo structure
				foreach(var index in channel.Value.DigestChannelIndexDescriptors) {
					foreach(var file in index.Value.Files) {
						cascadingHashSets.Add((index.Key, file.Key), new HashNodeList());
					}
				}

				if(channel.Value.DigestChannelIndexDescriptors.ContainsKey(channel.Key)) {
					for(uint i = 1; i <= slices; i++) {
						// perform the actual hash
						var sliceHashes = verifyFiles ? validatingDigestChannelSet.Channels[channel.Key].HashChannel((int) i) : null;


						foreach(var indexSet in channel.Value.DigestChannelIndexDescriptors) {
							foreach(var fileset in indexSet.Value.Files) {

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
				HashNodeList nodes = new HashNodeList();
				HashNodeList topnodes = new HashNodeList();
				
				topnodes.Add(channel.Value.DigestChannelIndexDescriptors.Count);
				foreach(var indexDescriptor in channel.Value.DigestChannelIndexDescriptors.OrderBy(f => f.Key)) {

					nodes.Add(indexDescriptor.Value.Files.Count);
					foreach(var entry in indexDescriptor.Value.Files.OrderBy(f => f.Key)) {
			
						HashNodeList nodesParts = new HashNodeList();
			
						nodesParts.Add(entry.Value.DigestChannelIndexFilePartDescriptors.Count);
						foreach(var entry2 in entry.Value.DigestChannelIndexFilePartDescriptors.OrderBy(f => f.Key)) {
			
							nodesParts.Add(entry2.Value.Hash);
						}
			
						var nodesPartsHash = HashingUtils.Hash3(nodesParts).Entry;
						
						if(!entry.Value.Hash.Equals(entry.Value.Hash.Entry)) {
							return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
						}
						nodes.Add(entry.Value.Hash);
					}
			
					var nodesHash = HashingUtils.Hash3(nodes).Entry;
						
					if(!indexDescriptor.Value.Hash.Equals(nodesHash)) {
						return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
					}
					topnodes.Add(indexDescriptor.Value.Hash);
				}
			
				var topnodesHash = HashingUtils.Hash3(topnodes).Entry;
				
				if(!channel.Value.Hash.Equals(topnodesHash)) {
					return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
				}
				descriptorNodes.Add(channel.Value.Hash);	
			}

			SafeArrayHandle descriptorNodesHash = HashingUtils.Hash3(descriptorNodes);

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
			IXmssmtCryptographicKey moderatorKey = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<IXmssmtCryptographicKey>(digest.Signature.KeyAddress.OrdinalId);

			if(moderatorKey.Key == null || moderatorKey.Key.IsEmpty) {
				throw new ApplicationException("Moderator key was not found in the chain state.");
			}

			// thats it :)
			ValidationResult result = await this.ValidateDigestSignature(digest.Hash, digest.Signature, moderatorKey).ConfigureAwait(false);

			// we did it, this is a valid digest!
			return result;
		}

		protected virtual async Task<ValidationResult> PerformBasicTransactionValidation(ITransaction transaction, ITransactionEnvelope envelope, bool? accreditationCertificateValid) {

			bool validCertificate = accreditationCertificateValid.HasValue && accreditationCertificateValid.Value;

			// some transaction types can not be more than one a second
			if(!validCertificate && transaction is IRateLimitedTransaction && transaction.TransactionId.Scope != 0) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.TRANSACTION_TYPE_ALLOWS_SINGLE_SCOPE);
			}

			return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Once we have all the pieces, perform the actual synchronous validation
		/// </summary>
		/// <param name="block"></param>
		/// <param name="hash"></param>
		/// <param name="previousBlockHash"></param>
		/// <returns></returns>
		protected async Task<ValidationResult> ValidateBlock(ISimpleBlock block, bool gossipOrigin, SafeArrayHandle hash, SafeArrayHandle previousBlockHash){

			var usablePreviousBlockHash = previousBlockHash;

			long? loadBlockId = null;
			LockContext lockContext = null;
			// make sure we always run this check atomically, while we are 100% that an insert/interpret is not happening at the same time (chain sync vs gossip insert competition)
			var results = await this.CentralCoordinator.ChainComponentProvider.BlockchainProviderBase.PerformAtomicChainHeightOperation(async (lc) => {

				var diskBlockHeight = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.DiskBlockHeight;
				
				if(diskBlockHeight + 1 == block.BlockId.Value) {
					// ok, its the last block, we just use the previous hash
					return null;
				}
				else if(diskBlockHeight >= block.BlockId.Value){
					loadBlockId = block.BlockId.Value;
					// its a previous block that we already have, we can still validate it. let's load the hash from disk.
					
					return null;
				}
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.CantValidate, BlockValidationErrorCodes.Instance.LAST_BLOCK_HEIGHT_INVALID);
			}, lockContext).ConfigureAwait(false);

			if (results != null){
				return results;
			}
	
			if (loadBlockId.HasValue){
				usablePreviousBlockHash = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadBlockHash(loadBlockId.Value);
			}
			bool hashValid = hash.Equals(BlockchainHashingUtils.GenerateBlockHash(block, usablePreviousBlockHash));

			if(hashValid == false) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
			}

			// ensure that a valid key is being used
			if(!this.ValidateBlockKeyTree(block.SignatureSet.ModeratorKey)) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_DIGEST_KEY);
			}

			BlockSignatureSet.BlockSignatureTypes signatureType = block.SignatureSet.AccountSignatureType;

			// ok, check the signature
			// first thing, get the key from our chain state
			ICryptographicKey moderatorKey = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey(block.SignatureSet.ModeratorKey);

			if(signatureType == BlockSignatureSet.BlockSignatureTypes.Xmss) {

				// simply use it as is
				if(moderatorKey.IsEmpty) {
					//moderatorKey.Dispose();
					//TODO: shjould we check for an invalid key here?  we could reload it form the block, but how to know which block udpated the key last?
					throw new ApplicationException("invalid moderator key");
				}

			} else if(signatureType == BlockSignatureSet.BlockSignatureTypes.SecretSequential) {
				if(moderatorKey is SecretDoubleCryptographicKey secretDoubleCryptographicModeratorKey) {
					// For blocks, we will transform the hash of the key into a secret key

					if(moderatorKey.IsEmpty) {
						moderatorKey = this.ReloadModeratorSequentialKey();
					}

				} else {
					throw new ApplicationException("Invalid block key.");
				}
			} else if(signatureType == BlockSignatureSet.BlockSignatureTypes.SuperSecret) {
				// ok, here we used a super key
				if(moderatorKey.IsEmpty) {
					//moderatorKey.Dispose();
					//TODO: shjould we check for an invalid key here?  we could reload it form the block, but how to know which block udpated the key last?
					throw new ApplicationException("invalid moderator key");
				}

			}

			// thats it :)
			return await this.ValidateBlockSignature(hash, block, moderatorKey).ConfigureAwait(false);

		}

		/// <summary>
		/// if the key was corrupt in the chain state, for whatever reason, we try to get it again from the last saved block
		/// </summary>
		/// <returns></returns>
		private ICryptographicKey ReloadModeratorSequentialKey() {

			ICryptographicKey key = null;

			Repeater.Repeat(() => {
				var block = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadLatestBlock();

				if(block == null) {
					throw new ApplicationException("Failed to load previous block");
				}

				key = block.SignatureSet.ConvertToSecretKey();
			});

			return key;
		}

		protected async Task<ValidationResult> ValidateGenesisBlock(IGenesisBlock block, SafeArrayHandle hash) {

			using(var newHash = BlockchainHashingUtils.GenerateGenesisBlockHash(block)) {
				bool hashValid = hash.Equals(newHash);

				if(hashValid != true) {
					return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
				}
			}

			// ok, check the signature

			// for the genesisModeratorAccountPresentation block, we will transform the key into a secret key
			SecretCryptographicKey secretCryptographicKey = new SecretCryptographicKey();
			secretCryptographicKey.SecurityCategory = QTESLASecurityCategory.SecurityCategories.PROVABLY_SECURE_III;
			secretCryptographicKey.Key.Entry = ((GenesisBlockAccountSignature) block.SignatureSet.BlockAccountSignature).PublicKey.Entry;

			return await this.ValidateBlockSignature(hash, block, secretCryptographicKey).ConfigureAwait(false);

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
					
					Log.Information($"Validating Genesis block hash against the official reference genesis hash file.");

					(SafeArrayHandle sha2, SafeArrayHandle sha3)? genesis = fetchingService.FetchGenesisHash(hashUrl, this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GenesisFolderPath, genesisBlock.Name);

					if(!genesis.HasValue) {
						Log.Fatal($"Official reference Genesis block hash file could not be acquired!");

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
						Log.Fatal($"Genesis block hash (SHA2_512 '{genesisSha264}' and SHA3_512 '{genesisSha364}') has been verified against the official reference hashes (SHA2_512 '{sha264}' and SHA3_512 '{sha364}') and has been found to be invalid!");

						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_PTAH_HASH_VERIFICATION_FAILED);
					}
					
					Log.Information($"Genesis block hash (SHA2_512 '{genesisSha264}' and SHA3_512 '{genesisSha364}') has been verified against the official reference hashes (SHA2_512 '{sha264}' and SHA3_512 '{sha364}') and has been found to be valid.");

				} catch(Exception ex) {
					Log.Error(ex, "Failed to query and verify genesis verification Hash.");

					return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_PTAH_HASH_VERIFICATION_FAILED);
				}
			} else {
				Log.Warning($"Skipping Genesis block official reference hash file verification.");

			}
			return await this.ValidateGenesisBlock(genesisBlock, hash).ConfigureAwait(false);
		}

		/// <summary>
		///     Load a single key from the blockchain files
		/// </summary>
		/// <param name="keyAddress"></param>
		/// <returns></returns>
		protected ICryptographicKey GetAccountKey(KeyAddress keyAddress) {

			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadFullKey(keyAddress);
		}

		/// <summary>
		///     Load multiple keys form the blockchain files
		/// </summary>
		/// <param name="keyAddresses"></param>
		/// <returns></returns>
		protected Dictionary<AccountId, ICryptographicKey> GetAccountKeys(List<KeyAddress> keyAddresses) {

			return this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadFullKeys(keyAddresses);

		}

		protected virtual async Task<ValidationResult> ValidatePresentationTransaction(ITransactionEnvelope envelope, IStandardPresentationTransaction transaction) {

			// ok, let's check the POW
			//TODO: this should be done asynchronously. its too time expensive. return a routed task and continue on the other side.
			ValidationResult result = null;
#if(!COLORADO_EXCLUSION)
			result = this.ValidateProvedTransaction(transaction);

			if(result != ValidationResult.ValidationResults.Valid) {

				Log.Warning("Presentation transaction failed POW verification");

				return result;
			}
#endif
			if(envelope.Signature is IPresentationEnvelopeSignature presentationEnvelopeSignature) {

				// for presentation transactions, the key is in the signature.
				QTeslaCryptographicKey secretCryptographicKey = new QTeslaCryptographicKey();
				secretCryptographicKey.SecurityCategory = presentationEnvelopeSignature.AccountSignature.SecurityCategory;
				secretCryptographicKey.Key.Entry = presentationEnvelopeSignature.AccountSignature.PublicKey.Entry;

				result = await this.ValidateSingleSignature(envelope.Hash, presentationEnvelopeSignature.AccountSignature, secretCryptographicKey).ConfigureAwait(false);
			}

			return result;
		}

		protected virtual ValidationResult ValidateJointPresentationTransaction(ITransactionEnvelope envelope, IJointPresentationTransaction transaction) {

			if(envelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {

				// check that the signatures match the declared accounts
				if(jointEnvelopeSignature.AccountSignatures.Count < transaction.RequiredSignatureCount) {
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNT_COUNT);
				}

				// check required ones
				var signatureAccounts = jointEnvelopeSignature.AccountSignatures.Select(a => a.KeyAddress.DeclarationTransactionId.Account).ToList();
				var requiredSignatures = transaction.MemberAccounts.Where(a => a.Required).Select(a => a.AccountId.ToAccountId()).ToList();
				var allAccounts = transaction.MemberAccounts.Select(a => a.AccountId.ToAccountId()).ToList();

				if(!requiredSignatures.All(a => signatureAccounts.Contains(a))) {
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTs);
				}

				// if any are not in the signatures list, we fail
				if(signatureAccounts.Any(s => !allAccounts.Contains(s))) {
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE_ACCOUNTs);
				}
			}

			return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		protected ValidationResult ValidateProvedTransaction(IProved provedTransaction) {
			if(provedTransaction.PowSolutions.Count > GlobalsService.POW_MAX_SOLUTIONS) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_POW_SOLUTIONS_COUNT);
			}

			AesSearchPow pow = new AesSearchPow();

			return pow.Verify(HashingUtils.GenerateHash(provedTransaction), provedTransaction.PowNonce, provedTransaction.PowDifficulty, provedTransaction.PowSolutions, Enums.ThreadMode.Single) == false ? this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_POW_SOLUTION) : this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);

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
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_KEY_ACCOUNT);
				}

				ICryptographicKey key = keys[accountId];


				SafeArrayHandle activeHash = hash;

				if(accountId != transaction.TransactionId.Account) {
					// ok, this is not the main emitter. we have to hash with it's key index
					activeHash = BlockchainHashingUtils.GenerateTransactionHash(envelope, accountId);
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
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_SIGNATURE);
				}
			}

			return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);
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

			(SafeArrayHandle sha2, SafeArrayHandle sha3) hashedKey = HashingUtils.HashSecretKey(signature.PromisedPublicKey.ToExactByteArray());

			// make sure they match as promised
			if(!hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
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

			SafeArrayHandle signature1 = rehydrator.ReadNonNullableArray();
			SafeArrayHandle signature2 = rehydrator.ReadNonNullableArray();

			(SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) hashedKey = HashingUtils.HashSecretComboKey(signature.PromisedPublicKey.ToExactByteArray(), signature.PromisedNonce1, signature.PromisedNonce2);

			// make sure they match as promised
			if(key.NonceHash != hashedKey.nonceHash || !hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);

		}

		protected virtual async Task<ValidationResult> ValidateBlockchainMessageSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			if(key.Id == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!(bitSize == Enums.KeyHashBits.SHA2_256 || bitSize == Enums.KeyHashBits.SHA3_256)) {
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

		protected virtual async Task<ValidationResult> ValidateTransationSingleSignature(SafeArrayHandle hash, IAccountSignature signature, ICryptographicKey key) {

			ValidationResult result = this.ValidateTransationKey(key);

			if(result.Invalid) {
				return result;
			}

			return await this.ValidateSingleSignature(hash, signature, key).ConfigureAwait(false);
		}

		protected virtual async Task<ValidationResult> ValidateTransationMultipleSignatures(SafeArrayHandle hash, ITransactionEnvelope envelope, ITransaction transaction, List<IPublishedAccountSignature> signatures, Dictionary<AccountId, ICryptographicKey> keys) {

			foreach(ICryptographicKey key in keys.Values) {
				ValidationResult result = this.ValidateTransationKey(key);

				if(result.Invalid) {
					return result;
				}
			}

			return await this.ValidateMultipleSignatures(hash, envelope, transaction, signatures, keys).ConfigureAwait(false);
		}

		protected virtual ValidationResult ValidateTransationKey(ICryptographicKey key) {
			if(key.Id == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!bitSize.HasFlag((Enums.KeyHashBits)Enums.HASH512)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_TYPE);
				}
			}else if(key.Id == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(bitSize.HasFlag((Enums.KeyHashBits)Enums.HASH512)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_TYPE);
				}
			} else if(key.Id == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!bitSize.HasFlag((Enums.KeyHashBits)Enums.HASH512)) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_TYPE);
				}
			} else if(key.Id == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				if(key is ISecretCryptographicKey secretCryptographicKey) {
					// all good
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SUPERKEY_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_KEY_TYPE);
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

			SafeArrayHandle publicKey = key.Key.Branch();

			switch(key) {
				case IXmssmtCryptographicKey xmssmtCryptographicKey:
					using(provider = new XMSSMTProvider((Enums.KeyHashBits) xmssmtCryptographicKey.BitSize, xmssmtCryptographicKey.TreeHeight, xmssmtCryptographicKey.TreeLayer)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

				case IXmssCryptographicKey xmssCryptographicKey:
					using(provider = new XMSSProvider((Enums.KeyHashBits) xmssCryptographicKey.BitSize, xmssCryptographicKey.TreeHeight)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

				case SecretDoubleCryptographicKey secretCryptographicKey:

					if(signature is IFirstAccountKey firstAccountKey) {
						// ok, for first account key, we have no original key to reffer to, so we use the public key published
						publicKey = firstAccountKey.PublicKey;

						using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
							provider.Initialize();

							bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}
						}
					} else if(signature is IPromisedSecretAccountSignature secretAccountSignature) {
						// ok, for secret accounts, we verified that it matched the promise, so we can used the provided public key
						publicKey = secretAccountSignature.PromisedPublicKey;

						using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(signature.Autograph);

						SafeArrayHandle signature1 = rehydrator.ReadNonNullableArray();
						SafeArrayHandle signature2 = rehydrator.ReadNonNullableArray();

						using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
							provider.Initialize();

							bool result = await provider.Verify(hash, signature1, publicKey).ConfigureAwait(false);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}

							// second signature
							result = await provider.Verify(hash, signature2, secretCryptographicKey.SecondKey.Key).ConfigureAwait(false);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}
						}

					} else {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE);
					}

					break;
				
				case SecretCryptographicKey secretCryptographicKey:
					provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory);

					using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;
				case QTeslaCryptographicKey qTeslaCryptographicKey:
					provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory);

					using(provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature.Autograph, publicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

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

			if(block.SignatureSet.BlockAccountSignature.IsHashPublished && !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SkipPeriodicBlockHashVerification) {
				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;
				
				string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;
				SafeArrayHandle publishedHash = fetchingService.FetchBlockPublicHash(hashUrl, block.BlockId.Value);

				if(!hash.Equals(publishedHash)) {
					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.FAILED_PUBLISHED_HASH_VALIDATION);
				}
			}

			//TODO: this needs major cleaning...

			if(key is SecretPentaCryptographicKey secretPentaCryptographicKey) {

				if(block.SignatureSet.BlockAccountSignature is SuperSecretBlockAccountSignature secretAccountSignature) {

					(SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) hashed = HashingUtils.HashSecretComboKey(secretAccountSignature.PromisedPublicKey.ToExactByteArray(), secretAccountSignature.PromisedNonce1, secretAccountSignature.PromisedNonce2);

					if(hashed.nonceHash != secretPentaCryptographicKey.NonceHash || !secretPentaCryptographicKey.NextKeyHashSha2.Equals(hashed.sha2) || !secretPentaCryptographicKey.NextKeyHashSha3.Equals(hashed.sha3)) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED);
					}

					using IDataRehydrator autorgaphRehydrator = DataSerializationFactory.CreateRehydrator(block.SignatureSet.BlockAccountSignature.Autograph);

					SafeArrayHandle signature1 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretPentaCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature1, secretAccountSignature.PromisedPublicKey).ConfigureAwait(false);

						signature1.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					SafeArrayHandle signature2 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretPentaCryptographicKey.SecondKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature2, secretPentaCryptographicKey.SecondKey.Key).ConfigureAwait(false);

						signature2.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					SafeArrayHandle signature3 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretPentaCryptographicKey.ThirdKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature3, secretPentaCryptographicKey.ThirdKey.Key).ConfigureAwait(false);

						signature3.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					SafeArrayHandle signature4 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretPentaCryptographicKey.FourthKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature4, secretPentaCryptographicKey.FourthKey.Key).ConfigureAwait(false);

						signature4.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					SafeArrayHandle signature5 = autorgaphRehydrator.ReadNonNullableArray();

					using(XMSSMTProvider provider = new XMSSMTProvider((Enums.KeyHashBits) secretPentaCryptographicKey.FifthKey.BitSize, secretPentaCryptographicKey.FifthKey.TreeHeight, secretPentaCryptographicKey.FifthKey.TreeLayer)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature5, key.Key).ConfigureAwait(false);

						signature5.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					// ok, when we got here, its doing really well. but we still need to validate the confirmation Id. let's do this
					IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;
						
					string hashUrl = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.HashUrl;
					var remoteConfirmationUuid = fetchingService.FetchSuperkeyConfirmationUuid(hashUrl, block.BlockId.Value);

					if(!remoteConfirmationUuid.HasValue || remoteConfirmationUuid.Value != secretAccountSignature.ConfirmationUuid) {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);

				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			if(key is SecretDoubleCryptographicKey secretDoubleCryptographicKey) {

				SafeArrayHandle publicKey = null;

				if(block.SignatureSet.BlockAccountSignature is SecretBlockAccountSignature secretAccountSignature) {

					(SafeArrayHandle sha2, SafeArrayHandle sha3, int nonceHash) hashed = HashingUtils.HashSecretComboKey(secretAccountSignature.PromisedPublicKey.ToExactByteArray(), secretAccountSignature.PromisedNonce1, secretAccountSignature.PromisedNonce2);

					if(hashed.nonceHash != secretDoubleCryptographicKey.NonceHash || !secretDoubleCryptographicKey.NextKeyHashSha2.Equals(hashed.sha2) || !secretDoubleCryptographicKey.NextKeyHashSha3.Equals(hashed.sha3)) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED);
					}

					using IDataRehydrator autorgaphRehydrator = DataSerializationFactory.CreateRehydrator(block.SignatureSet.BlockAccountSignature.Autograph);

					SafeArrayHandle signature1 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretDoubleCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature1, secretAccountSignature.PromisedPublicKey).ConfigureAwait(false);

						signature1.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					SafeArrayHandle signature2 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(secretDoubleCryptographicKey.SecondKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, signature2, secretDoubleCryptographicKey.SecondKey.Key).ConfigureAwait(false);

						signature2.Return();

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);

				}

				if(block.SignatureSet.BlockAccountSignature is GenesisBlockAccountSignature genesisBlockAccountSignature) {

					publicKey = genesisBlockAccountSignature.PublicKey;
				} else {
					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
				}

				using(SignatureProviderBase provider = new QTeslaProvider(secretDoubleCryptographicKey.SecurityCategory)) {
					provider.Initialize();

					bool result = await provider.Verify(hash, block.SignatureSet.BlockAccountSignature.Autograph, publicKey).ConfigureAwait(false);

					if(result == false) {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
					}

				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
			}

			if(key is SecretCryptographicKey secretCryptographicKey) {

				if(block.SignatureSet.BlockAccountSignature is GenesisBlockAccountSignature genesisBlockAccountSignature) {

					using(SignatureProviderBase provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = await provider.Verify(hash, block.SignatureSet.BlockAccountSignature.Autograph, genesisBlockAccountSignature.PublicKey).ConfigureAwait(false);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}

					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			if(key is IXmssCryptographicKey xmssCryptographicKey) {
				if(block.SignatureSet.BlockAccountSignature is XmssBlockAccountSignature xmssBlockAccountSignature) {

					using XMSSProvider provider = new XMSSProvider((Enums.KeyHashBits) xmssCryptographicKey.BitSize, xmssCryptographicKey.TreeHeight);

					provider.Initialize();

					bool result = await provider.Verify(hash, xmssBlockAccountSignature.Autograph, key.Key).ConfigureAwait(false);

					if(result == false) {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);

				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_KEY_CORRELATION_TYPE);
		}

		protected virtual async Task<ValidationResult> ValidateDigestSignature(SafeArrayHandle hash, IPublishedAccountSignature signature, IXmssmtCryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			using XMSSMTProvider provider = new XMSSMTProvider((Enums.KeyHashBits) key.BitSize, key.TreeHeight, key.TreeLayer);

			provider.Initialize();

			bool result = await provider.Verify(hash, signature.Autograph, key.Key).ConfigureAwait(false);

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
			if(ordinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID || ordinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSS_ID) {
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

		protected ValidationResult CreateValidationResult(ValidationResult.ValidationResults result, EventValidationErrorCode errorCode) {
			return new ValidationResult(result, errorCode);
		}

		protected ValidationResult CreateValidationResult(ValidationResult.ValidationResults result, List<EventValidationErrorCode> errorCodes) {
			return new ValidationResult(result, errorCodes);
		}

		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, BlockValidationErrorCode errorCode);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, List<BlockValidationErrorCode> errorCodes);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, EventValidationErrorCode errorCode);
		protected abstract BlockValidationResult CreateBlockValidationResult(ValidationResult.ValidationResults result, List<EventValidationErrorCode> errorCodes);

		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, MessageValidationErrorCode errorCode);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, List<MessageValidationErrorCode> errorCodes);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, EventValidationErrorCode errorCode);
		protected abstract MessageValidationResult CreateMessageValidationResult(ValidationResult.ValidationResults result, List<EventValidationErrorCode> errorCodes);

		protected abstract TransactionValidationResult CreateTrasactionValidationResult(ValidationResult.ValidationResults result);
		protected abstract TransactionValidationResult CreateTrasactionValidationResult(ValidationResult.ValidationResults result, TransactionValidationErrorCode errorCode);
		protected abstract TransactionValidationResult CreateTrasactionValidationResult(ValidationResult.ValidationResults result, List<TransactionValidationErrorCode> errorCodes);
		protected abstract TransactionValidationResult CreateTrasactionValidationResult(ValidationResult.ValidationResults result, EventValidationErrorCode errorCode);
		protected abstract TransactionValidationResult CreateTrasactionValidationResult(ValidationResult.ValidationResults result, List<EventValidationErrorCode> errorCodes);

		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, DigestValidationErrorCode errorCode);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, List<DigestValidationErrorCode> errorCodes);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, EventValidationErrorCode errorCode);
		protected abstract DigestValidationResult CreateDigestValidationResult(ValidationResult.ValidationResults result, List<EventValidationErrorCode> errorCodes);
	}
}