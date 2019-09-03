using System;
using System.Collections.Generic;
using System.Linq;
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
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Tasks.Base;
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
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers {

	public interface IValidationManager : IManagerBase {
		IRoutedTask ValidateBlock(IDehydratedBlock dehydratedBlock, Action<ValidationResult> completedResultCallback);
		IRoutedTask ValidateBlock(IBlock block, Action<ValidationResult> completedResultCallback);
		IRoutedTask ValidateTransaction(ITransactionEnvelope transactionEnvelope, Action<ValidationResult> completedResultCallback);
		IRoutedTask ValidateBlockchainMessage(IMessageEnvelope transactionEnvelope, Action<ValidationResult> completedResultCallback);
		ValidationResult ValidateDigest(IBlockchainDigest digest, bool verifyFiles);

		IRoutedTask ValidateEnvelopedContent(IEnvelope envelope, Action<ValidationResult> completedResultCallback);
	}

	public interface IValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IManagerBase<ValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IValidationManager
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class ValidationManager {
	}

	public abstract class ValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ManagerBase<ValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IValidationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		/// <summary>
		///     How many cache entries to keep
		/// </summary>
		protected const int DEFAULT_CACHE_COUNT = 5;

		private readonly IGuidService guidService;

		private readonly Sha3SakuraTree hasher = new Sha3SakuraTree();

		private readonly ITimeService timeService;

		public ValidationManager(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator, GlobalSettings.ApplicationSettings.GetChainConfiguration(centralCoordinator.ChainId).MaxValidationParallelCount) {
			this.guidService = centralCoordinator.BlockchainServiceSet.GuidService;
			this.timeService = centralCoordinator.BlockchainServiceSet.TimeService;
		}

		public virtual IRoutedTask ValidateEnvelopedContent(IEnvelope envelope, Action<ValidationResult> completedResultCallback) {

			if(envelope is IBlockEnvelope blockEnvelope) {
				if(GlobalSettings.ApplicationSettings.MobileMode) {
					throw new ApplicationException("Mobile apps can not validate blocks");
				}

				return this.ValidateBlock(blockEnvelope.Contents, completedResultCallback);
			}

			if(envelope is IMessageEnvelope messageEnvelope) {
				return this.ValidateBlockchainMessage(messageEnvelope, completedResultCallback);
			}

			if(envelope is ITransactionEnvelope transactionEnvelope) {
				return this.ValidateTransaction(transactionEnvelope, completedResultCallback);
			}

			throw new ApplicationException("Invalid envelope type");

			return null;
		}

		public IRoutedTask ValidateBlock(IDehydratedBlock dehydratedBlock, Action<ValidationResult> completedResultCallback) {

			// lets make sure its rehydrated, we need it fully now

			dehydratedBlock.RehydrateBlock(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase, true);

			return this.ValidateBlock(dehydratedBlock.RehydratedBlock, completedResultCallback);
		}

		public virtual IRoutedTask ValidateBlock(IBlock block, Action<ValidationResult> completedResultCallback) {

			ValidationResult result = null;

			if(block is IGenesisBlock genesisBlock) {
				result = this.ValidateGenesisBlock(genesisBlock, block.Hash);

				completedResultCallback(result);
			} else if(block is ISimpleBlock simpleBlock) {
				long previousId = block.BlockId.Value - 1;

				void PerformBlockValidation(IByteArray previousBlockHash) {
					result = this.ValidateBlock(simpleBlock, block.Hash, previousBlockHash);

					completedResultCallback(result);
				}

				PerformBlockValidation((ByteArray) this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.LastBlockHash);
			} else {
				throw new ApplicationException("Invalid block type");
			}

			return null;
		}

		public IRoutedTask ValidateBlockchainMessage(IMessageEnvelope messageEnvelope, Action<ValidationResult> completedResultCallback) {

			// lets make sure its rehydrated, we need it fully now

			messageEnvelope.Contents.RehydrateMessage(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

			// ok,l first lets compare the hashes
			IBlockchainMessage message = messageEnvelope.Contents.RehydratedMessage;

			//first check the time to ensure we are within the acceptable range
			if(!this.timeService.WithinAcceptableRange(message.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception)) {
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

				return null;
			}

			bool hashValid = messageEnvelope.Hash.Equals(HashingUtils.GenerateHash(message));

			if(hashValid != true) {
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.HASH_INVALID));

				return null;
			}

			// if the key is ahead of where we are and we are still syncing, we can use the embeded key to make a summary validation, enough to forward a gossip message
			if(GlobalSettings.ApplicationSettings.MobileMode || ((messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced)) {

				MessageValidationResult result = new MessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

				// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
				if(messageEnvelope.Signature.AccountSignature.PublicKey?.Key != null) {

					// ok, we can try to validate it using the included key. it does not mean the mssage is absolutely valid, but there may be a certain validity to it.
					ValidationResult includedResults = this.ValidateBlockchainMessageSingleSignature(messageEnvelope.Hash, messageEnvelope.Signature.AccountSignature, messageEnvelope.Signature.AccountSignature.PublicKey);

					if(includedResults == ValidationResult.ValidationResults.Valid) {

						result = this.CreateMessageValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
					}
				}

				// we are not sure, but it passed this test at least	
				completedResultCallback(result);

				return null;
			}

			if(GlobalSettings.ApplicationSettings.MobileMode) {
				// mobile mode can not go any further
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE));

				return null;
			}

			if((messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

				// this doesnt work for us, we can't validate this
				completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

				return null;
			}

			if(messageEnvelope.Signature.AccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED));

				return null;
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {

				byte keyOrdinal = messageEnvelope.Signature.AccountSignature.KeyAddress.OrdinalId;

				if(this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FastKeyEnabled(keyOrdinal)) {
					// ok, we can take the fast route!
					var keyBytes = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadAccountKeyFromIndex(messageEnvelope.Signature.AccountSignature.KeyAddress.AccountId, keyOrdinal);

					if(keyBytes.HasValue && (keyBytes.Value.keyBytes != null) && keyBytes.Value.keyBytes.HasData) {

						if((messageEnvelope.Signature.AccountSignature.PublicKey?.Key != null) && messageEnvelope.Signature.AccountSignature.PublicKey.Key.HasData) {
							// ok, this message has an embeded public key. lets confirm its the same that we pulled up
							if(!messageEnvelope.Signature.AccountSignature.PublicKey.Key.Equals(keyBytes.Value.keyBytes)) {
								// ok, we have a discrepansy. they embeded a key that does not match the public record. 
								//TODO: we should log the peer for bad acting here
								completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

								return null;
							}
						}

						IXmssCryptographicKey xmssKey = new XmssCryptographicKey();

						xmssKey.Id = keyOrdinal;
						xmssKey.Key = keyBytes.Value.keyBytes;

						xmssKey.TreeHeight = keyBytes.Value.treeheight;
						xmssKey.BitSize = keyBytes.Value.hashBits;

						// ok we got a key, lets go forward
						ValidationResult result = this.ValidateBlockchainMessageSingleSignature(messageEnvelope.Hash, messageEnvelope.Signature.AccountSignature, xmssKey);

						completedResultCallback(result);

						return null;
					}
				}
			}

			// now we must get our key. this is asynchrounous as it is time expensive. we will be back
			var task = this.GetAccountKey(messageEnvelope.Signature.AccountSignature.KeyAddress);

			task.SetCompleted((results, taskRoutingContext) => {
				if(results.Success) {
					ICryptographicKey key = task.Results;

					if(messageEnvelope.Signature.AccountSignature.PublicKey?.Key != null) {
						// ok, this message has an embeded public key. lets confirm its the same that we pulled up
						if(!messageEnvelope.Signature.AccountSignature.PublicKey.Key.Equals(key.Key)) {
							// ok, we have a discrepansy. they embeded a key that does not match the public record. 
							//TODO: we should log the peer for bad acting here
							completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

							return;
						}
					}

					// thats it :)
					ValidationResult result = this.ValidateBlockchainMessageSingleSignature(messageEnvelope.Hash, messageEnvelope.Signature.AccountSignature, key);

					completedResultCallback(result);
				} else {
					completedResultCallback(this.CreateMessageValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

					//TODO: what to do here?
					Log.Fatal(results.Exception, "Failed to validate message.");

					// this is very critical
					results.Rethrow();
				}
			});

			return task;
		}

		public IRoutedTask ValidateTransaction(ITransactionEnvelope transactionEnvelope, Action<ValidationResult> completedResultCallback) {

			// lets make sure its rehydrated, we need it fully now

			transactionEnvelope.Contents.RehydrateTransaction(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

			//first check the time to ensure we are within the acceptable range
			if(!this.timeService.WithinAcceptableRange(transactionEnvelope.Contents.RehydratedTransaction.TransactionId.Timestamp.Value, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception)) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.NOT_WITHIN_ACCEPTABLE_TIME_RANGE));

				return null;
			}

			// ok,l first lets compare the hashes
			ITransaction transaction = transactionEnvelope.Contents.RehydratedTransaction;

			bool hashValid = transactionEnvelope.Hash.Equals(HashingUtils.GenerateHash(transaction));

			if(hashValid != true) {
				completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.HASH_INVALID));

				return null;
			}

			// if there is a certificate id provided, lets check it
			bool? accreditationCertificateValid = null;

			if(transactionEnvelope.AccreditationCertificates.Any()) {

				accreditationCertificateValid = this.CentralCoordinator.ChainComponentProvider.AccreditationCertificateProviderBase.IsAnyTransactionCertificateValid(transactionEnvelope.AccreditationCertificates, transaction.TransactionId, Enums.CertificateApplicationTypes.Envelope);
			}

			// perform basic validations
			ValidationResult result = this.PerformBasicTransactionValidation(transaction, transactionEnvelope, accreditationCertificateValid);

			if((result == ValidationResult.ValidationResults.Valid) && transaction is IStandardPresentationTransaction presentationTransaction) {

				result = this.ValidatePresentationTransaction(transactionEnvelope, presentationTransaction);

				completedResultCallback(result);

				return null;
			}

			if((result == ValidationResult.ValidationResults.Valid) && transaction is IJointPresentationTransaction jointPresentationTransaction) {
				// lets do a special validation first, but it will go through the usual after
				result = this.ValidateJointPresentationTransaction(transactionEnvelope, jointPresentationTransaction);
			}

			if(result != ValidationResult.ValidationResults.Valid) {
				completedResultCallback(result);

				return null;
			}

			IRoutedTask routedTask = null;

			if(transactionEnvelope.Signature is ISingleEnvelopeSignature singleEnvelopeSignature) {

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
					if(GlobalSettings.ApplicationSettings.MobileMode || ((publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced)) {

						TransactionValidationResult embdedKeyResult = new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED);

						// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
						if(publishedAccountSignature.PublicKey?.Key != null) {

							// ok, we can try to validate it using the included key. it does not mean the mssage is absolutely valid, but there may be a certain validity to it.
							ValidationResult includedResults = this.ValidateTransationSingleSignature(transactionEnvelope.Hash, publishedAccountSignature, publishedAccountSignature.PublicKey);

							if(includedResults == ValidationResult.ValidationResults.Valid) {

								// we are not sure, but it passed this test at least	
								embdedKeyResult = this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
							}
						}

						// we are not sure, but it passed this test at least	
						completedResultCallback(embdedKeyResult);

						return null;
					}

					if((publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

						// this doesnt work for us, we can't validate this
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

						return null;
					}
				}

				if(GlobalSettings.ApplicationSettings.MobileMode) {
					// mobile mode can not go any further
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.MOBILE_CANNOT_VALIDATE));

					return null;
				}

				if(publishedAccountSignature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) {
					completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.KEY_NOT_YET_SYNCED));

					return null;
				}

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {
					// ok, we can take the fast route!
					byte keyOrdinal = keyAddress.OrdinalId;

					if(this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FastKeyEnabled(keyOrdinal)) {
						var keyBytes = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadAccountKeyFromIndex(transaction.TransactionId.Account, keyOrdinal);

						if((keyBytes?.keyBytes != null) && keyBytes.Value.keyBytes.HasData && transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature2) {

							if(publishedAccountSignature?.PublicKey?.Key != null) {
								// ok, this message has an embeded public key. lets confirm its the same that we pulled up
								if(!publishedAccountSignature.PublicKey.Key.Equals(keyBytes.Value.keyBytes)) {
									// ok, we have a discrepansy. they embeded a key that does not match the public record. 
									//TODO: we should log the peer for bad acting here
									completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

									return null;
								}
							}

							IXmssCryptographicKey xmssKey = new XmssCryptographicKey();

							xmssKey.Id = keyOrdinal;
							xmssKey.Key = keyBytes.Value.keyBytes;
							xmssKey.TreeHeight = keyBytes.Value.treeheight;
							xmssKey.BitSize = keyBytes.Value.hashBits;

							// ok we got a key, lets go forward
							result = this.ValidateTransationSingleSignature(transactionEnvelope.Hash, publishedEnvelopeSignature2.AccountSignature, xmssKey);

							completedResultCallback(result);

							return null;
						}
					}
				}

				// now we must get our key. this is asynchrounous as it is time expensive. we will be back
				var task = this.GetAccountKey(keyAddress);

				task.SetCompleted((results, taskRoutingContext) => {
					if(results.Success) {
						if(result == ValidationResult.ValidationResults.Valid) {
							ICryptographicKey key = task.Results;

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
									result = this.ValidateSecretSignature(transactionEnvelope.Hash, secretEnvelopeSignature.AccountSignature, secretCryptographicKey);
								}
							} else if(transactionEnvelope.Signature is IPublishedEnvelopeSignature publishedEnvelopeSignature2) {
								result = this.ValidateTransationSingleSignature(transactionEnvelope.Hash, publishedEnvelopeSignature2.AccountSignature, key);
							}
						}

						completedResultCallback(result);
					} else {
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

						//TODO: what to do here?
						Log.Fatal(results.Exception, "Failed to validate transaction.");

						// this is very critical
						results.Rethrow();
					}
				});

				routedTask = task;
			} else if(transactionEnvelope.Signature is IJointEnvelopeSignature jointEnvelopeSignature) {

				if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.EnableFastKeyIndex) {
					// ok, we can take the fast route!
					//TODO: this is a bit all or nothing here. Some keys may be available as fast, others may not. mix the schemes optimally
					if(jointEnvelopeSignature.AccountSignatures.All(s => this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.FastKeyEnabled(s.KeyAddress.OrdinalId))) {
						var keys = new Dictionary<AccountId, ICryptographicKey>();

						bool usesEmbededKey = false;

						foreach(IPublishedAccountSignature signature in jointEnvelopeSignature.AccountSignatures) {
							var keyBytes = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadAccountKeyFromIndex(signature.KeyAddress.AccountId, signature.KeyAddress.OrdinalId);

							if(keyBytes.HasValue && (keyBytes.Value.keyBytes != null) && keyBytes.Value.keyBytes.HasData) {

								if((signature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (signature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainSynced) {

									// this doesnt work for us, we can't validate this
									completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.IMPOSSIBLE_BLOCK_DECLARATION_ID));

									return null;
								}

								// if there is an embedded public key, wew can try using it
								// if the key is ahead of where we are and we are still syncing, we can use the embeded key to make a summary validation, enough to forward a gossip message
								if((signature.KeyAddress.AnnouncementBlockId.Value > this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight) && (signature.KeyAddress.AnnouncementBlockId.Value <= this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.PublicBlockHeight) && this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.IsChainDesynced) {

									// ok, if we get here, the message uses a key we most probably dont have yet. this is a tricky case.
									if(signature.PublicKey?.Key != null) {

										usesEmbededKey = true;
										keys.Add(signature.KeyAddress.DeclarationTransactionId.Account, signature.PublicKey);

									}
								} else {

									IXmssCryptographicKey xmssKey = new XmssCryptographicKey();

									xmssKey.Id = signature.KeyAddress.OrdinalId;
									xmssKey.Key = keyBytes.Value.keyBytes;
									xmssKey.TreeHeight = keyBytes.Value.treeheight;
									xmssKey.BitSize = keyBytes.Value.hashBits;

									keys.Add(signature.KeyAddress.DeclarationTransactionId.Account, xmssKey);
								}
							}

							if(keys.Any()) {
								result = this.ValidateTransationMultipleSignatures(transactionEnvelope.Hash, jointEnvelopeSignature.AccountSignatures, keys);

								// if we used any embeded key, we can not fully trust the results
								if((result == ValidationResult.ValidationResults.Valid) && usesEmbededKey) {
									result = this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.EmbededKeyValid);
								}

								completedResultCallback(result);

								return null;
							}
						}
					}
				}

				var task = this.GetAccountKeys(jointEnvelopeSignature.AccountSignatures.Select(s => s.KeyAddress).ToList());

				task.SetCompleted((results, taskRoutingContext) => {
					if(results.Success) {
						//.ToDictionary(t => t.Key, t => t.Value.Keyset.Keys[jointEnvelopeSignature.AccountSignatures.Single(s => s.KeyAddress.DeclarationTransactionId.Account == t.Key).KeyAddress.OrdinalId])
						var keys = task.Results;

						// validate any embeded key to ensure if they were provided, they were right
						foreach(var key in keys) {

							IPublishedAccountSignature publicSignature = jointEnvelopeSignature.AccountSignatures.SingleOrDefault(s => s.KeyAddress.AccountId == key.Key);

							if(publicSignature?.PublicKey?.Key != null) {
								// ok, this message has an embeded public key. lets confirm its the same that we pulled up
								if(!publicSignature.PublicKey.Key.Equals(key.Key)) {
									// ok, we have a discrepansy. they embeded a key that does not match the public record. 
									//TODO: we should log the peer for bad acting here
									completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.ENVELOPE_EMBEDED_PUBLIC_KEY_INVALID));

									return;
								}
							}
						}

						// thats it :)
						result = this.ValidateTransationMultipleSignatures(transactionEnvelope.Hash, jointEnvelopeSignature.AccountSignatures, keys);

						completedResultCallback(result);
					} else {
						completedResultCallback(this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED));

						//TODO: what to do here?
						Log.Fatal(results.Exception, "Failed to validate transction.");

						// this is very critical
						results.Rethrow();
					}
				});

				routedTask = task;
			}

			return routedTask;
		}

		public ValidationResult ValidateDigest(IBlockchainDigest digest, bool verifyFiles) {

			// first, we validate the hash itself against the online double hash file
			if(!this.CentralCoordinator.ChainSettings.SkipDigestHashVerification) {

				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

				string digestHashesPath = this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.DigestHashesPath;
				FileExtensions.EnsureDirectoryStructure(digestHashesPath, this.CentralCoordinator.FileSystem);

				(IByteArray sha2, IByteArray sha3) genesis = fetchingService.FetchDigestHash(digestHashesPath, digest.DigestId);

				bool hashVerifyResult = BlockchainDoubleHasher.VerifyDigestHash(digest, genesis.sha2, genesis.sha3);

				genesis.sha2.Return();
				genesis.sha3.Return();

				if(!hashVerifyResult) {
					return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.FAILED_DIGEST_HASH_VALIDATION);
				}
			}

			// lets make sure its rehydrated, we need it fully now

			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<ValidatingDigestChannelSet>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {

				serializationTask.Results = serializationService.CreateValidationDigestChannelSet(digest.DigestId, digest.DigestDescriptor);
			});

			//note: this must remain sync absolutely! do not change!!
			this.DispatchTaskSync(serializationTask);
			/////////////////////////////////////////////////////////

			//NOTE: we can do the below in the validation thread and NOT the serialization thread because we mostly only read and access a digest that is not yet installed, and thus not otherwise used
			ValidatingDigestChannelSet validatingDigestChannelSet = serializationTask.Results;

			Sha3SakuraTree hasher = new Sha3SakuraTree();

			// ok, lets validate the files.
			HashNodeList topNodes = new HashNodeList();

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

				for(uint i = 1; i <= slices; i++) {
					// perform the actual hash
					var sliceHashes = verifyFiles ? validatingDigestChannelSet.Channels[channel.Key].HashChannel((int) i) : null;

					foreach(var indexSet in channel.Value.DigestChannelIndexDescriptors) {
						foreach(var fileset in indexSet.Value.Files) {

							BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor fileDescriptor = channel.Value.DigestChannelIndexDescriptors[indexSet.Key].Files[fileset.Key];
							IByteArray descriptorHash = fileDescriptor.DigestChannelIndexFilePartDescriptors[i].Hash;

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

				// now the rest of the structure

				HashNodeList channelNodes = new HashNodeList();

				foreach(var index in channel.Value.DigestChannelIndexDescriptors.OrderBy(f => f.Key)) {
					HashNodeList indexNodes = new HashNodeList();

					foreach(var file in index.Value.Files.OrderBy(f => f.Key)) {
						IByteArray fileHash = hasher.Hash(cascadingHashSets[(index.Key, file.Key)]);

						BlockchainDigestChannelDescriptor.DigestChannelIndexDescriptor.DigestChannelIndexFileDescriptor fileDescriptor = channel.Value.DigestChannelIndexDescriptors[index.Key].Files[file.Key];

						if(!fileDescriptor.Hash.Equals(fileHash)) {
							// optional files will pass no matter what
							if(!fileDescriptor.IsOptional) {
								return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_SLICE_HASH);
							}
						}

						// add the descriptor hash, in case it was optional and did not match
						indexNodes.Add(fileDescriptor.Hash);
					}

					IByteArray indexHash = hasher.Hash(indexNodes);

					if(!channel.Value.DigestChannelIndexDescriptors[index.Key].Hash.Equals(indexHash)) {
						return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_CHANNEL_INDEX_HASH);
					}

					channelNodes.Add(indexHash);
				}

				IByteArray channelHash = hasher.Hash(channelNodes);

				if(!channel.Value.Hash.Equals(channelHash)) {
					return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_CHANNEL_HASH);
				}

				topNodes.Add(channelHash);
			}

			IByteArray topHash = hasher.Hash(topNodes);

			if(!digest.DigestDescriptor.Hash.Equals(topHash)) {
				return this.CreateDigestValidationResult(ValidationResult.ValidationResults.Invalid, DigestValidationErrorCodes.Instance.INVALID_DIGEST_DESCRIPTOR_HASH);
			}

			// we did it, our files match!!  now the digest itself...

			// finally, at the top of the pyramid, lets compare the hashes
			(IByteArray sha2, IByteArray sha3) digestHashes = HashingUtils.ExtractCombinedDualHash(digest.Hash);
			(IByteArray sha2, IByteArray sha3) rebuiltHashes = HashingUtils.GenerateDualHash(digest);

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

			if((moderatorKey.Key == null) || moderatorKey.Key.IsEmpty) {
				throw new ApplicationException("Moderator key was not found in the chain state.");
			}

			// thats it :)
			ValidationResult result = this.ValidateDigestSignature(digest.Hash, digest.Signature, moderatorKey);

			// we did it, this is a valid digest!
			return result;
		}

		protected virtual ValidationResult PerformBasicTransactionValidation(ITransaction transaction, ITransactionEnvelope envelope, bool? accreditationCertificateValid) {

			bool validCertificate = accreditationCertificateValid.HasValue && accreditationCertificateValid.Value;

			// some transaction types can not be more than one a second
			if(!validCertificate && transaction is IRateLimitedTransaction && (transaction.TransactionId.Scope != 0)) {
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
		protected ValidationResult ValidateBlock(ISimpleBlock block, IByteArray hash, IByteArray previousBlockHash) {

			if(block is IGenesisBlock genesisBlock) {
				return this.ValidateGenesisBlock(genesisBlock, hash, previousBlockHash);
			}

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight != (block.BlockId.Value - 1)) {
				// thats too bad, we are not ready to validate this block. we must let it go
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.CantValidate, BlockValidationErrorCodes.Instance.LAST_BLOCK_HEIGHT_INVALID);
			}

			bool hashValid = hash.Equals(BlockchainHashingUtils.GenerateBlockHash(block, previousBlockHash));

			if(hashValid == false) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
			}

			// ensure that a valid key is being used
			if(!this.ValidateBlockKeyTree(block.SignatureSet.ModeratorKey)) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_DIGEST_KEY);
			}

			// ok, check the signature
			// first thing, get the key from our chain state
			ICryptographicKey moderatorKey = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey(block.SignatureSet.ModeratorKey);

			if((moderatorKey.Key == null) || moderatorKey.Key.IsEmpty) {
				throw new ApplicationException("Moderator key was not found in the chain state.");
			}

			BlockSignatureSet.BlockSignatureTypes signatureType = block.SignatureSet.AccountSignatureType;

			ICryptographicKey key = null;

			if(signatureType == BlockSignatureSet.BlockSignatureTypes.XmssMT) {

				// simply use it as is
				key = moderatorKey;
			} else if(signatureType == BlockSignatureSet.BlockSignatureTypes.SecretSequential) {
				if(moderatorKey is SecretDoubleCryptographicKey secretDoubleCryptographicModeratorKey) {
					// For blocks, we will transform the hash of the key into a secret key

					key = secretDoubleCryptographicModeratorKey;
				} else {
					throw new ApplicationException("Invalid block key.");
				}
			} else if(signatureType == BlockSignatureSet.BlockSignatureTypes.SuperSecret) {
				// ok, here we used a super key
				key = (SecretPentaCryptographicKey) moderatorKey;
			}

			// thats it :)
			return this.ValidateBlockSignature(hash, block, key);
		}

		protected ValidationResult ValidateGenesisBlock(IGenesisBlock block, IByteArray hash, IByteArray previousBlockHash) {

			if(previousBlockHash == null) {
				previousBlockHash = new ByteArray();
			}

			if(previousBlockHash.HasData) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_HASH_SET);
			}

			bool hashValid = hash.Equals(BlockchainHashingUtils.GenerateBlockHash(block, previousBlockHash));

			if(hashValid != true) {
				return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.HASH_INVALID);
			}

			// ok, check the signature

			// for the genesisModeratorAccountPresentation block, we will transform the key into a secret key
			SecretCryptographicKey secretCryptographicKey = new SecretCryptographicKey();
			secretCryptographicKey.SecurityCategory = QTESLASecurityCategory.SecurityCategories.PROVABLY_SECURE_III;
			secretCryptographicKey.Key = ((GenesisBlockAccountSignature) block.SignatureSet.BlockAccountSignature).PublicKey;

			return this.ValidateBlockSignature(hash, block, secretCryptographicKey);

		}

		protected virtual ValidationResult ValidateGenesisBlock(IGenesisBlock genesisBlock, IByteArray hash) {
			// ok, at this point, the signer is who he/she says he/she is. now we confirm the transaction signature
			//for a genesisModeratorAccountPresentation transaction, thats all we verify

			//lets compare the hashes we fetch from the official website
			if(!this.CentralCoordinator.ChainSettings.SkipGenesisHashVerification) {

				try {
					//TODO: here we validate the hash from http file here
					IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;

					(IByteArray sha2, IByteArray sha3) genesis = fetchingService.FetchGenesisHash(this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.GenesisFolderPath, genesisBlock.Name);

					if(genesis == default) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_SATURN_HASH_VERIFICATION_FAILED);
					}

					bool result = BlockchainDoubleHasher.VerifyGenesisHash(genesisBlock, genesis.sha2, genesis.sha3);

					genesis.sha2.Return();
					genesis.sha3.Return();

					if(!result) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_SATURN_HASH_VERIFICATION_FAILED);
					}
				} catch(Exception ex) {
					Log.Error(ex, "Failed to query and verify genesis verification Hash.");

					return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.GENESIS_SATURN_HASH_VERIFICATION_FAILED);
				}
			}

			return this.ValidateGenesisBlock(genesisBlock, hash, new ByteArray());
		}

		/// <summary>
		///     Load a single key from the blockchain files
		/// </summary>
		/// <param name="keyAddress"></param>
		/// <returns></returns>
		protected SerializationTask<ISerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, ICryptographicKey, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GetAccountKey(KeyAddress keyAddress) {
			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<ICryptographicKey>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {
				serializationTask.Results = serializationService.LoadFullKey(keyAddress);
			});

			return serializationTask;
		}

		/// <summary>
		///     Load multiple keys form the blockchain files
		/// </summary>
		/// <param name="keyAddresses"></param>
		/// <returns></returns>
		protected SerializationTask<ISerializationManager<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, Dictionary<AccountId, ICryptographicKey>, CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> GetAccountKeys(List<KeyAddress> keyAddresses) {
			var serializationTask = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.TaskFactoryBase.CreateSerializationTask<Dictionary<AccountId, ICryptographicKey>>();

			serializationTask.SetAction((serializationService, taskRoutingContext) => {

				serializationTask.Results = serializationService.LoadFullKeys(keyAddresses);
			});

			return serializationTask;
		}

		protected virtual ValidationResult ValidatePresentationTransaction(ITransactionEnvelope envelope, IStandardPresentationTransaction transaction) {

			// ok, let's check the POW
			//TODO: this should be done asynchronously. its too time expensive. return a routed task and continue on the other side.
			ValidationResult result = this.ValidateProvedTransaction(envelope.Hash, transaction);

			if(result != ValidationResult.ValidationResults.Valid) {

				Log.Warning("Presentation transaction failed POW verification");

				return result;
			}

			if(envelope.Signature is IPresentationEnvelopeSignature presentationEnvelopeSignature) {

				// for presentation transactions, the key is in the signature.
				QTeslaCryptographicKey secretCryptographicKey = new QTeslaCryptographicKey();
				secretCryptographicKey.SecurityCategory = presentationEnvelopeSignature.AccountSignature.SecurityCategory;
				secretCryptographicKey.Key = presentationEnvelopeSignature.AccountSignature.PublicKey;

				result = this.ValidateSingleSignature(envelope.Hash, presentationEnvelopeSignature.AccountSignature, secretCryptographicKey);
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

		protected ValidationResult ValidateProvedTransaction(IByteArray hash, IProved provedTransaction) {
			if(provedTransaction.PowSolutions.Count > GlobalsService.POW_MAX_SOLUTIONS) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_POW_SOLUTIONS_COUNT);
			}

			using(AesSearchPow pow = new AesSearchPow()) {
				if(pow.Verify(hash, provedTransaction.PowNonce, provedTransaction.PowDifficulty, provedTransaction.PowSolutions, Enums.ThreadMode.Single) == false) {
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_POW_SOLUTION);
				}
			}

			return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Valid);
		}

		/// <summary>
		///     Validate a group of signatures. any fails, all fail.
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual ValidationResult ValidateMultipleSignatures(IByteArray hash, List<IPublishedAccountSignature> signatures, Dictionary<AccountId, ICryptographicKey> keys) {
			// validate the secret nonce with the published key, if it matches the promise.

			foreach(IPublishedAccountSignature signature in signatures) {

				AccountId accountId = signature.KeyAddress.DeclarationTransactionId.Account;

				if(!keys.ContainsKey(accountId)) {
					return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_JOINT_KEY_ACCOUNT);
				}

				ICryptographicKey key = keys[accountId];

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
				ValidationResult result = this.ValidateSingleSignature(hash, signature, key);

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
		protected virtual ValidationResult ValidateSecretSignature(IByteArray hash, IPromisedSecretAccountSignature signature, ISecretDoubleCryptographicKey key) {
			// validate the secret nonce with the published key, if it matches the promise.

			(IByteArray sha2, IByteArray sha3) hashedKey = HashingUtils.HashSecretKey(signature.PromisedPublicKey.ToExactByteArray());

			// make sure they match as promised
			if(!hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
			}

			return this.ValidateSingleSignature(hash, signature, key);
		}

		/// <summary>
		///     Validate a secret key signature
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="signature"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual ValidationResult ValidateSecretComboSignature(IByteArray hash, IPromisedSecretComboAccountSignature signature, ISecretComboCryptographicKey key) {
			// validate the secret nonce with the published key, if it matches the promise.

			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(signature.Autograph);

			IByteArray signature1 = rehydrator.ReadNonNullableArray();
			IByteArray signature2 = rehydrator.ReadNonNullableArray();

			(IByteArray sha2, IByteArray sha3, int nonceHash) hashedKey = HashingUtils.HashSecretComboKey(signature.PromisedPublicKey.ToExactByteArray(), signature.PromisedNonce1, signature.PromisedNonce2);

			// make sure they match as promised
			if((key.NonceHash != hashedKey.nonceHash) || !hashedKey.sha2.Equals(key.NextKeyHashSha2) || !hashedKey.sha3.Equals(key.NextKeyHashSha3)) {
				return this.CreateTrasactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_SECRET_KEY_PROMISSED_HASH_VALIDATION);
			}

			return this.ValidateSingleSignature(hash, signature, key);
		}

		protected virtual ValidationResult ValidateBlockchainMessageSingleSignature(IByteArray hash, IAccountSignature signature, ICryptographicKey key) {

			if(key.Id == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!((bitSize == Enums.KeyHashBits.SHA2_256) || (bitSize == Enums.KeyHashBits.SHA3_256))) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
				}
			} else {
				return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, MessageValidationErrorCodes.Instance.INVALID_MESSAGE_XMSS_KEY_TYPE);
			}

			return this.ValidateSingleSignature(hash, signature, key);
		}

		protected virtual ValidationResult ValidateTransationSingleSignature(IByteArray hash, IAccountSignature signature, ICryptographicKey key) {

			ValidationResult result = this.ValidateTransationKey(key);

			if(result.Invalid) {
				return result;
			}

			return this.ValidateSingleSignature(hash, signature, key);
		}

		protected virtual ValidationResult ValidateTransationMultipleSignatures(IByteArray hash, List<IPublishedAccountSignature> signatures, Dictionary<AccountId, ICryptographicKey> keys) {

			foreach(ICryptographicKey key in keys.Values) {
				ValidationResult result = this.ValidateTransationKey(key);

				if(result.Invalid) {
					return result;
				}
			}

			return this.ValidateMultipleSignatures(hash, signatures, keys);
		}

		protected virtual ValidationResult ValidateTransationKey(ICryptographicKey key) {
			if(key.Id == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!((bitSize == Enums.KeyHashBits.SHA2_512) || (bitSize == Enums.KeyHashBits.SHA3_512))) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_TRANSACTION_XMSS_KEY_TYPE);
				}
			} else if(key.Id == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
				if(key is IXmssCryptographicKey xmssCryptographicKey) {
					Enums.KeyHashBits bitSize = (Enums.KeyHashBits) xmssCryptographicKey.BitSize;

					if(!((bitSize == Enums.KeyHashBits.SHA2_512) || (bitSize == Enums.KeyHashBits.SHA3_512))) {
						return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_BIT_SIZE);
					}
				} else {
					return new TransactionValidationResult(ValidationResult.ValidationResults.Invalid, TransactionValidationErrorCodes.Instance.INVALID_CHANGE_XMSS_KEY_TYPE);
				}
			} else if(key.Id == GlobalsService.SUPER_KEY_ORDINAL_ID) {
				if(key is ISecretDoubleCryptographicKey secretCryptographicKey) {
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
		protected virtual ValidationResult ValidateSingleSignature(IByteArray hash, IAccountSignature signature, ICryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			SignatureProviderBase provider = null;

			IByteArray publicKey = key.Key;

			switch(key) {
				case IXmssmtCryptographicKey xmssmtCryptographicKey:
					using(provider = new XMSSMTProvider((Enums.KeyHashBits) xmssmtCryptographicKey.BitSize, xmssmtCryptographicKey.TreeHeight, xmssmtCryptographicKey.TreeLayer)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature.Autograph, publicKey);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					break;

				case IXmssCryptographicKey xmssCryptographicKey:
					using(provider = new XMSSProvider((Enums.KeyHashBits) xmssCryptographicKey.BitSize, xmssCryptographicKey.TreeHeight)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature.Autograph, publicKey);

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

							bool result = provider.Verify(hash, signature.Autograph, publicKey);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}
						}
					} else if(signature is IPromisedSecretAccountSignature secretAccountSignature) {
						// ok, for secret accounts, we verified that it matched the promise, so we can used the provided public key
						publicKey = secretAccountSignature.PromisedPublicKey;

						IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(signature.Autograph);

						IByteArray signature1 = rehydrator.ReadNonNullableArray();
						IByteArray signature2 = rehydrator.ReadNonNullableArray();

						using(provider = new QTeslaProvider(secretCryptographicKey.SecurityCategory)) {
							provider.Initialize();

							bool result = provider.Verify(hash, signature1, publicKey);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}

							// second signature
							result = provider.Verify(hash, signature2, secretCryptographicKey.SecondKey.Key);

							if(result == false) {
								return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
							}
						}
					} else {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, EventValidationErrorCodes.Instance.SECRET_KEY_NO_SECRET_ACCOUNT_SIGNATURE);
					}

					break;

				case QTeslaCryptographicKey qTeslaCryptographicKey:
					provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory);

					using(provider = new QTeslaProvider(qTeslaCryptographicKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature.Autograph, publicKey);

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
		protected virtual ValidationResult ValidateBlockSignature(IByteArray hash, IBlock block, ICryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			if(block.SignatureSet.BlockAccountSignature.IsHashPublished && !this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.SkipPeriodicBlockHashVerification) {
				IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;
				IByteArray publishedHash = fetchingService.FetchBlockPublicHash(block.BlockId.Value);

				if(!hash.Equals(publishedHash)) {
					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.FAILED_PUBLISHED_HASH_VALIDATION);
				}
			}

			//TODO: this needs major cleaning...

			if(key is SecretPentaCryptographicKey secretPentaCryptographicKey) {

				if(block.SignatureSet.BlockAccountSignature is SuperSecretBlockAccountSignature secretAccountSignature) {

					SecretPentaCryptographicKey moderatorKey = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<SecretPentaCryptographicKey>(secretAccountSignature.KeyAddress.OrdinalId);

					(IByteArray sha2, IByteArray sha3, int nonceHash) hashed = HashingUtils.HashSecretComboKey(secretAccountSignature.PromisedPublicKey.ToExactByteArray(), secretAccountSignature.PromisedNonce1, secretAccountSignature.PromisedNonce2);

					if((hashed.nonceHash != moderatorKey.NonceHash) || !moderatorKey.NextKeyHashSha2.Equals(hashed.sha2) || !moderatorKey.NextKeyHashSha3.Equals(hashed.sha3)) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED);
					}

					IDataRehydrator autorgaphRehydrator = DataSerializationFactory.CreateRehydrator((ByteArray) block.SignatureSet.BlockAccountSignature.Autograph);

					IByteArray signature1 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature1, secretAccountSignature.PromisedPublicKey);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					IByteArray signature2 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.SecondKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature2, moderatorKey.SecondKey.Key);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					IByteArray signature3 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.ThirdKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature3, moderatorKey.ThirdKey.Key);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					IByteArray signature4 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.FourthKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature4, moderatorKey.FourthKey.Key);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					IByteArray signature5 = autorgaphRehydrator.ReadNonNullableArray();

					using(XMSSMTProvider provider = new XMSSMTProvider((Enums.KeyHashBits) moderatorKey.FifthKey.BitSize, moderatorKey.FifthKey.TreeHeight, moderatorKey.FifthKey.TreeLayer)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature5, key.Key);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					// ok, when we got here, its doing really well. but we still need to validate the confirmation Id. let's do this
					IFileFetchService fetchingService = this.CentralCoordinator.BlockchainServiceSet.FileFetchService;
					var remoteConfirmationUuid = fetchingService.FetchSuperkeyConfirmationUuid(block.BlockId.Value);

					if(!remoteConfirmationUuid.HasValue || (remoteConfirmationUuid.Value != secretAccountSignature.ConfirmationUuid)) {
						return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);

				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			if(key is SecretDoubleCryptographicKey secretDoubleCryptographicKey) {

				IByteArray publicKey = null;

				if(block.SignatureSet.BlockAccountSignature is SecretBlockAccountSignature secretAccountSignature) {

					SecretDoubleCryptographicKey moderatorKey = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<SecretDoubleCryptographicKey>(GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID);

					(IByteArray sha2, IByteArray sha3, int nonceHash) hashed = HashingUtils.HashSecretComboKey(secretAccountSignature.PromisedPublicKey.ToExactByteArray(), secretAccountSignature.PromisedNonce1, secretAccountSignature.PromisedNonce2);

					if((hashed.nonceHash != moderatorKey.NonceHash) || !moderatorKey.NextKeyHashSha2.Equals(hashed.sha2) || !moderatorKey.NextKeyHashSha3.Equals(hashed.sha3)) {
						return this.CreateBlockValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SECRET_KEY_PROMISSED_HASH_VALIDATION_FAILED);
					}

					IDataRehydrator autorgaphRehydrator = DataSerializationFactory.CreateRehydrator(block.SignatureSet.BlockAccountSignature.Autograph);

					IByteArray signature1 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature1, secretAccountSignature.PromisedPublicKey);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}
					}

					IByteArray signature2 = autorgaphRehydrator.ReadNonNullableArray();

					using(SignatureProviderBase provider = new QTeslaProvider(moderatorKey.SecondKey.SecurityCategory)) {
						provider.Initialize();

						bool result = provider.Verify(hash, signature2, moderatorKey.SecondKey.Key);

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

					bool result = provider.Verify(hash, block.SignatureSet.BlockAccountSignature.Autograph, publicKey);

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

						bool result = provider.Verify(hash, block.SignatureSet.BlockAccountSignature.Autograph, genesisBlockAccountSignature.PublicKey);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}

					}

					return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			if(key is IXmssmtCryptographicKey xmssmtCryptographicKey) {
				if(block.SignatureSet.BlockAccountSignature is XmssBlockAccountSignature xmssBlockAccountSignature) {

					using(XMSSMTProvider provider = new XMSSMTProvider((Enums.KeyHashBits) xmssmtCryptographicKey.BitSize, xmssmtCryptographicKey.TreeHeight, xmssmtCryptographicKey.TreeLayer)) {
						provider.Initialize();

						bool result = provider.Verify(hash, xmssBlockAccountSignature.Autograph, key.Key);

						if(result == false) {
							return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
						}

						return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
					}
				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_SIGNATURE_TYPE);
			}

			return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.INVALID_BLOCK_KEY_CORRELATION_TYPE);
		}

		protected virtual ValidationResult ValidateDigestSignature(IByteArray hash, IPublishedAccountSignature signature, IXmssmtCryptographicKey key) {
			// ok, now lets confirm the signature. make sure the hash is authentic and not tempered with

			using(XMSSMTProvider provider = new XMSSMTProvider((Enums.KeyHashBits) key.BitSize, key.TreeHeight, key.TreeLayer)) {
				provider.Initialize();

				bool result = provider.Verify(hash, signature.Autograph, key.Key);

				if(result == false) {
					return this.CreateValidationResult(ValidationResult.ValidationResults.Invalid, BlockValidationErrorCodes.Instance.SIGNATURE_VERIFICATION_FAILED);
				}

				return this.CreateValidationResult(ValidationResult.ValidationResults.Valid);
			}
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
			if((ordinal == GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID) || (ordinal == GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID)) {
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