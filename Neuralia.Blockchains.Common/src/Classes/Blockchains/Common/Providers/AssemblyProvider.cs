using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.General.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Debugging;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;
using Newtonsoft.Json;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {
	public interface IAssemblyProvider {
	}

	public interface IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAssemblyProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		//		IKeyChangeTransaction GenerateKeyChangeBlock(int newKeyOrdinal, string keyChangeName);
		void DebugSerializeBlock(string filepath, ITransaction transaction);
		ITransactionEnvelope GenerateDebugTransaction();

		IStandardPresentationTransaction GeneratePresentationTransaction(SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, long? correlationId = null);
		ITransactionEnvelope GeneratePresentationEnvelope(IStandardPresentationTransaction presentationTransaction, SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, long? correlationId = null);

		ITransactionEnvelope GenerateKeyChangeTransaction(byte newKeyOrdinal, string keyChangeName, CorrelationContext correlationContext);
		IMessageEnvelope GenerateDebugMessage();
		IMessageEnvelope GenerateOnChainElectionsRegistrationMessage(AccountId electedAccountId, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo);

		ITransactionEnvelope GenerateTransaction(ITransaction transaction, string keyName, EnvelopeSignatureType signatureType, Action customProcessing = null, Action<ITransactionEnvelope, ITransaction> finalizationProcessing = null);

		List<IMessageEnvelope> PrepareElectionMessageEnvelopes(List<IElectionCandidacyMessage> messages);
		void PrepareTransactionBasics(ITransaction transaction);
	}

	/// <summary>
	///     this is where we do the transaction creation heavy lifting. Strong
	///     transaction Engineers required ;)
	/// </summary>
	public abstract class AssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
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

		public virtual IStandardPresentationTransaction GeneratePresentationTransaction(SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, long? correlationId = null) {
			try {
				IStandardPresentationTransaction standardPresentation = this.CreateNewPresentationTransaction();

				this.GenerateRawTransaction(standardPresentation, () => {

					// we also publish the hash of our backup key, in case we ever need it

					this.CentralCoordinator.PostSystemEvent(accountPublicationStepSet.CreatingPresentationTransaction, correlationContext);

					// now lets publish our keys

					this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.EnsureWalletLoaded();

					// This is a VERY special case. presentation is the only transaction where we have no account ID on the chain.
					// so, we will overwrite the empty accountId and publish our hash of our internal account id, and the mods will assign us a public id
					standardPresentation.TransactionId.Account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetAccountUuidHash();
					standardPresentation.CorrelationId = correlationId;

					// now we publish our keys
					IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();

					using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountUuid, GlobalsService.TRANSACTION_KEY_NAME)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.TRANSACTION_KEY_NAME}' key");
						}

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId.SimpleTransactionId;
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);

						standardPresentation.TransactionCryptographicKey.Id = key.KeyAddress.OrdinalId;
						standardPresentation.TransactionCryptographicKey.BitSize = (byte) key.HashBits;
						standardPresentation.TransactionCryptographicKey.TreeHeight = (byte) key.TreeHeight;
						standardPresentation.TransactionCryptographicKey.Key = ByteArray.CreateFrom(key.PublicKey);
					}

					using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountUuid, GlobalsService.MESSAGE_KEY_NAME)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.MESSAGE_KEY_NAME}' key");
						}

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId.SimpleTransactionId;
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);

						standardPresentation.MessageCryptographicKey.Id = key.KeyAddress.OrdinalId;
						standardPresentation.MessageCryptographicKey.BitSize = (byte) key.HashBits;
						standardPresentation.MessageCryptographicKey.TreeHeight = (byte) key.TreeHeight;
						standardPresentation.MessageCryptographicKey.Key = ByteArray.CreateFrom(key.PublicKey);
					}

					using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountUuid, GlobalsService.CHANGE_KEY_NAME)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.CHANGE_KEY_NAME}' key");
						}

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId.SimpleTransactionId;
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);

						standardPresentation.ChangeCryptographicKey.Id = key.KeyAddress.OrdinalId;
						standardPresentation.ChangeCryptographicKey.BitSize = (byte) key.HashBits;
						standardPresentation.ChangeCryptographicKey.TreeHeight = (byte) key.TreeHeight;
						standardPresentation.ChangeCryptographicKey.Key = ByteArray.CreateFrom(key.PublicKey);
					}

					using(ISecretWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<ISecretWalletKey>(account.AccountUuid, GlobalsService.SUPER_KEY_NAME)) {

						if(key == null) {
							throw new ApplicationException($"Failed to load '{GlobalsService.SUPER_KEY_NAME}' key");
						}

						// we are declaring this key in this block, so lets update our key
						key.KeyAddress.DeclarationTransactionId = standardPresentation.TransactionId.SimpleTransactionId;
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateKey(key);

						standardPresentation.SuperCryptographicKey.Id = key.KeyAddress.OrdinalId;
						standardPresentation.SuperCryptographicKey.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) key.SecurityCategory;

						(standardPresentation.SuperCryptographicKey.NextKeyHashSha2, standardPresentation.SuperCryptographicKey.NextKeyHashSha3) = BlockchainHashingUtils.HashSecretKey(key);
					}

				});

				// this is a very special case where we hash before we create the envelope
				IByteArray hash = HashingUtils.GenerateHash(standardPresentation);

				try {
					using(AesSearchPow pow = new AesSearchPow()) {

						this.CentralCoordinator.PostSystemEvent(accountPublicationStepSet.PerformingPOW, correlationContext);

						try {
							this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.PauseNetwork();

							(var solutions, int nonce) = pow.PerformPow(hash, GlobalsService.POW_DIFFICULTY, (currentNonce, difficulty) => {

								this.CentralCoordinator.PostSystemEvent(accountPublicationStepSet.PerformingPOWIteration(currentNonce, difficulty), correlationContext);
								Thread.Sleep(500);
							});
							
							standardPresentation.PowSolutions = solutions.Take(GlobalsService.POW_MAX_SOLUTIONS).ToList(); // take the top ones
							standardPresentation.PowNonce = nonce;
							standardPresentation.PowDifficulty = GlobalsService.POW_DIFFICULTY;

							this.CentralCoordinator.PostSystemEvent(accountPublicationStepSet.FoundPOWSolution(nonce, standardPresentation.PowDifficulty, standardPresentation.PowSolutions), correlationContext);
						} finally {
							this.CentralCoordinator.ChainComponentProvider.ChainNetworkingProviderBase.RestoreNetwork();
						}
					}
				} catch(Exception ex) {
					throw new ApplicationException("Failed to generate presentation transaction proof of work", ex);
				}

				return standardPresentation;
			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium presentation transaction", ex);
			}
		}

		public virtual ITransactionEnvelope GeneratePresentationEnvelope(IStandardPresentationTransaction presentationTransaction, SystemEventGenerator.AccountPublicationStepSet accountPublicationStepSet, CorrelationContext correlationContext, long? correlationId = null) {
			try {

				ITransactionEnvelope envelope = this.PrepareTransactionEnvelope(presentationTransaction, GlobalsService.SUPER_KEY_NAME, EnvelopeSignatureTypes.Instance.Presentation);

				// lets get it our transaction now
				envelope.Contents = presentationTransaction.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

				return envelope;
			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium presentation transaction", ex);
			}
		}

		public virtual ITransactionEnvelope GenerateKeyChangeTransaction(byte newKeyOrdinal, string keyChangeName, CorrelationContext correlationContext) {
			try {
				IStandardAccountKeyChangeTransaction standardAccountKeyChange = this.CreateNewKeyChangeTransaction(newKeyOrdinal);

				string keyMasterKeyName = GlobalsService.CHANGE_KEY_NAME;
				EnvelopeSignatureType signatureType = EnvelopeSignatureTypes.Instance.Published;

				if(standardAccountKeyChange.IsChangingChangeKey || standardAccountKeyChange.IsChangingSuperKey) {
					keyMasterKeyName = GlobalsService.SUPER_KEY_NAME;
					signatureType = EnvelopeSignatureTypes.Instance.SingleSecret;
				}

				ITransactionEnvelope envelope = this.GenerateTransaction(standardAccountKeyChange, keyMasterKeyName, signatureType, () => {

					// now lets publish our keys

					// now we publish our keys
					IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();
					BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

					if((newKeyOrdinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) || (newKeyOrdinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) || (newKeyOrdinal == GlobalsService.CHANGE_KEY_ORDINAL_ID)) {

						void SetTrxDetails(IXmssWalletKey nextKey, bool isKeySet) {
							if(nextKey == null) {
								throw new ApplicationException($"Failed to create new xmss '{keyChangeName}' key");
							}

							// we are declaring this key in this transaction, so lets update our key
							nextKey.KeyAddress.DeclarationTransactionId = standardAccountKeyChange.TransactionId.SimpleTransactionId;
							nextKey.KeyAddress.OrdinalId = newKeyOrdinal;

							// lets set it as our next one
							if(isKeySet) {
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(account.AccountUuid, nextKey);
							} else {
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetNextKey(account.AccountUuid, nextKey);
							}

							this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet();

							// lets publish its public details
							standardAccountKeyChange.XmssNewCryptographicKey.TreeHeight = (byte) nextKey.TreeHeight;
							standardAccountKeyChange.XmssNewCryptographicKey.BitSize = (byte) nextKey.HashBits;
							standardAccountKeyChange.XmssNewCryptographicKey.Key = (ByteArray) nextKey.PublicKey;
						}

						if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsNextKeySet(account.AccountUuid, keyChangeName)) {
							using(IXmssWalletKey newKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateXmssKey(keyChangeName)) {
								SetTrxDetails(newKey, false);
							}
						} else {
							using(IXmssWalletKey newKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(account.AccountUuid, keyChangeName)) {
								SetTrxDetails((IXmssWalletKey) newKey.NextKey, true);
							}
						}
					} else if(newKeyOrdinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
						// nothing to do since we are chaning the secret key no matter what
					}

					if(standardAccountKeyChange.IsChangingChangeKey) {

						void SetTrxDetails(ISecretWalletKey nextKey, bool isKeySet) {
							if(nextKey == null) {
								throw new ApplicationException($"Failed to create next '{GlobalsService.SUPER_KEY_NAME}' key");
							}

							// we are declaring this key in this transaction, so lets update our key
							nextKey.KeyAddress.DeclarationTransactionId = standardAccountKeyChange.TransactionId.SimpleTransactionId;
							nextKey.KeyAddress.OrdinalId = GlobalsService.SUPER_KEY_ORDINAL_ID;

							// lets set it as our next one
							if(isKeySet) {
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateNextKey(account.AccountUuid, nextKey);
							} else {
								this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SetNextKey(account.AccountUuid, nextKey);
							}

							this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SaveWallet();

							// lets publish its public details
							standardAccountKeyChange.NextSuperCryptographicKey.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) nextKey.SecurityCategory;
							(standardAccountKeyChange.NextSuperCryptographicKey.NextKeyHashSha2, standardAccountKeyChange.NextSuperCryptographicKey.NextKeyHashSha3) = BlockchainHashingUtils.HashSecretKey(nextKey);

						}

						// we use our secret backup key, so we create the next one
						if(!this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.IsNextKeySet(account.AccountUuid, GlobalsService.SUPER_KEY_NAME)) {
							using(ISecretWalletKey newKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreateSuperKey()) {
								SetTrxDetails(newKey, false);
							}
						} else {
							using(ISecretWalletKey newKey = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<ISecretWalletKey>(account.AccountUuid, GlobalsService.SUPER_KEY_NAME)) {
								SetTrxDetails((ISecretWalletKey) newKey.NextKey, true);
							}
						}

					}

				});

				return envelope;
			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium key change transaction", ex);
			}
		}

		public IMessageEnvelope GenerateOnChainElectionsRegistrationMessage(AccountId electedAccountId, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo) {
			try {

				IElectionsRegistrationMessage registrationMessage = this.CreateNewMinerRegistrationMessage();

				registrationMessage.AccountId = electedAccountId;

				// now, we encrypt our data for the moderator to see
				ICryptographicKey key = this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.GetModeratorKey<ICryptographicKey>(GlobalsService.MODERATOR_COMMUNICATIONS_KEY_ID);

				if(key is NtruCryptographicKey ntruCryptographicKey) {
					NtruEncryptor encryptor = new NtruEncryptor();
					registrationMessage.EncryptedMessage = encryptor.Encrypt(electionsCandidateRegistrationInfo.Dehydrate(), ntruCryptographicKey.Key);
				} else if(key is McElieceCryptographicKey mcElieceCryptographicKey) {
					McElieceEncryptor encryptor = new McElieceEncryptor();
					registrationMessage.EncryptedMessage = encryptor.Encrypt(electionsCandidateRegistrationInfo.Dehydrate(), mcElieceCryptographicKey.Key, mcElieceCryptographicKey.McElieceCipherMode);
				}

				IMessageEnvelope envelope = this.GenerateBlockchainMessage(registrationMessage);

				return envelope;

			} catch(Exception ex) {
				throw new ApplicationException("failed to generate neuralium key change transaction", ex);
			}
		}

		public List<IMessageEnvelope> PrepareElectionMessageEnvelopes(List<IElectionCandidacyMessage> messages) {

			var envelopes = new List<IMessageEnvelope>();

			foreach(IElectionCandidacyMessage message in messages) {
				envelopes.Add(this.GenerateBlockchainMessage(message));
			}

			return envelopes;
		}

		public void DebugSerializeBlock(string filepath, ITransaction transaction) {
			File.WriteAllText(filepath, JsonConvert.SerializeObject(transaction, Formatting.Indented, new ByteConverter()));
		}

		/// <summary>
		///     Generate a generic transaction. This version will save the wallet after generating
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="customProcessing">The custom processing to prepare the inner transaction</param>
		/// <param name="finalizationProcessing">
		///     Final processing done once the transaction is ready
		///     and signed
		/// </param>
		public ITransactionEnvelope GenerateTransaction(ITransaction transaction, string keyName, EnvelopeSignatureType signatureType, Action customProcessing = null, Action<ITransactionEnvelope, ITransaction> finalizationProcessing = null) {

			this.GenerateRawTransaction(transaction, customProcessing);

			return this.PrepareTransactionEnvelope(transaction, keyName, signatureType, finalizationProcessing);
		}

		public abstract ITransactionEnvelope GenerateDebugTransaction();

		public abstract IMessageEnvelope GenerateDebugMessage();

		/// <summary>
		///     prepare the transaction basics header data. This version is the one to use for most
		///     transaction types
		/// </summary>
		/// <param name="transaction"></param>
		public virtual void PrepareTransactionBasics(ITransaction transaction) {

			if(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.BlockHeight == 0) {
				throw new ApplicationException("Genesis block was never synced");
			}

			IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount();

			AccountId accountId = account.PublicAccountId;

			if(account.Status == Enums.PublicationStatus.New) {
				// new accounts dont have a public accountId. lets use the hash
				accountId = account.AccountUuidHash;
			}

			this.PrepareTransactionBasics(transaction, accountId, this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		protected void GenerateRawTransaction(ITransaction transaction, Action customProcessing = null) {

			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount().Status;

			if(status != Enums.PublicationStatus.Published) {
				if(transaction is IStandardPresentationTransaction) {
					if(status == Enums.PublicationStatus.Dispatched) {
						throw new ApplicationException("Our account has been dispatched, but is not yet confirmed. We can not present ourselves again on the blockchain.");
					}
				} else {
					throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
				}
			}

			this.PrepareTransactionBasics(transaction);

			// allow for the custom transaction processing
			customProcessing?.Invoke();
		}

		public ITransactionEnvelope PrepareTransactionEnvelope(ITransaction transaction, string keyName, EnvelopeSignatureType signatureType, Action<ITransactionEnvelope, ITransaction> finalizationProcessing = null) {

			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount().Status;

			if(status != Enums.PublicationStatus.Published) {
				if(transaction is IStandardPresentationTransaction) {
					if(status == Enums.PublicationStatus.Dispatched) {
						throw new ApplicationException("Our account has been dispatched, but is not yet confirmed. We can not present ourselves again on the blockchain.");
					}
				} else {
					throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
				}
			}

			if((transaction == null) || (transaction.TransactionId.Account == null)) {
				throw new ApplicationException("The presentation transaction must be created before we can generate the envelope.");

			}

			ITransactionEnvelope envelope = this.PrepareTransactionEnvelope(transaction, keyName, signatureType);

			finalizationProcessing?.Invoke(envelope, transaction);

			// lets get it our transaction now
			envelope.Contents = transaction.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

			return envelope;
		}

		protected virtual void PrepareTransactionBasics(ITransaction transaction, AccountId accountId, DateTime chainInception) {

			transaction.TransactionId = new TransactionIdExtended(this.guidService.CreateTransactionId(accountId, chainInception));

		}

		protected virtual void PrepareMessageBasics(IBlockchainMessage message) {

			message.Timestamp = this.timeService.GetChainDateTimeOffset(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
		}

		/// <summary>
		///     perform all the operations for the signature of the transaction
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="Key"></param>
		/// <param name="NextKey"></param>
		protected virtual ITransactionEnvelope PrepareTransactionEnvelope(ITransaction transaction, string keyName, EnvelopeSignatureType signatureType) {
			try {

				ITransactionEnvelope transactionEnvelope = this.CreateNewTransactionEnvelope(signatureType);

				// we will wait for the results, this is a VERY important event

				// the first step. we set the extended transaction id key use index for our XMSS key with our current state
				if(transactionEnvelope.Signature is PresentationEnvelopeSignature) {
					transaction.TransactionId.KeyUseIndex = null;
				} else if(transactionEnvelope.Signature is SecretEnvelopeSignature) {
					transaction.TransactionId.KeyUseIndex = null;
				} else {
					using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName)) {

						transaction.TransactionId.KeyUseIndex = new KeyUseIndexSet(key.KeySequenceId, key.KeyUseIndex, key.KeyAddress.OrdinalId);
					}
				}

				// now as the last step in the building, we hash the entire transaction to get the sakura tree root

				transactionEnvelope.Hash = HashingUtils.GenerateHash(transaction);

				void SignTransaction(IWalletKey key) {
					// hash the finalized transaction

					Log.Verbose("Singing transaction...");

					IByteArray signature = null;

					if(key is IXmssWalletKey xmssWalletKey) {
						signature = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignTransactionXmss(transactionEnvelope.Hash, xmssWalletKey);
					} else {
						signature = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignTransaction(transactionEnvelope.Hash, key);
					}

					Log.Verbose("Transaction successfully signed.");

					Action<IAccountSignature> setSignature = sig => {
						if(sig is IPublishedAccountSignature publishedAccountSignature) {

							publishedAccountSignature.KeyAddress = key.KeyAddress.Copy();

							// now we set our public key in case anybody would need it. mostly syncing nodes that are not yet up to date
							if(key is ISecretWalletKey secretWalletKey2) {
								SecretCryptographicKey publicKey = new SecretCryptographicKey();
								publicKey.Id = publishedAccountSignature.KeyAddress.OrdinalId;
								publicKey.Key = new ByteArray(secretWalletKey2.PublicKey.Length);
								publicKey.Key.CopyFrom(secretWalletKey2.PublicKey);
								(publicKey.NextKeyHashSha2, publicKey.NextKeyHashSha3) = BlockchainHashingUtils.HashSecretKey(secretWalletKey2);
								publicKey.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) secretWalletKey2.SecurityCategory;

								publishedAccountSignature.PublicKey = publicKey;
							} else if(key is IXmssWalletKey xmssWalletKey2) {
								XmssCryptographicKey publicKey = new XmssCryptographicKey();
								publicKey.Id = publishedAccountSignature.KeyAddress.OrdinalId;
								publicKey.Key = new ByteArray(xmssWalletKey2.PublicKey.Length);
								publicKey.Key.CopyFrom(xmssWalletKey2.PublicKey);
								publicKey.BitSize = (byte) xmssWalletKey2.HashBits;
								publicKey.TreeHeight = (byte) xmssWalletKey2.TreeHeight;
								publishedAccountSignature.PublicKey = publicKey;
							}
						}

						// and sign the whole thing with our key
						sig.Autograph = signature;

						if(sig is IPromisedSecretAccountSignature secretSig) {
							if(key is ISecretWalletKey secretWalletKey) {
								// a secret key publishes only the hash
								secretSig.PromisedPublicKey = (ByteArray) secretWalletKey.PublicKey;

							} else {
								throw new ApplicationException("Wallet key is not of secret type.");
							}
						}

						if(sig is IPromisedSecretComboAccountSignature secretComboSig) {
							if(key is ISecretDoubleWalletKey secretDoubleWalletKey) {
								// a secret key publishes only the hash
								secretComboSig.PromisedPublicKey = (ByteArray) secretDoubleWalletKey.PublicKey;
								secretComboSig.PromisedNonce1 = secretDoubleWalletKey.PromisedNonce1;
								secretComboSig.PromisedNonce2 = secretDoubleWalletKey.PromisedNonce2;

							}

							if(key is ISecretComboWalletKey secretComboWalletKey) {
								// a secret key publishes only the hash
								secretComboSig.PromisedPublicKey = (ByteArray) secretComboWalletKey.PublicKey;
								secretComboSig.PromisedNonce1 = secretComboWalletKey.PromisedNonce1;
								secretComboSig.PromisedNonce2 = secretComboWalletKey.PromisedNonce2;

							} else {
								throw new ApplicationException("Wallet key is not of secret type.");
							}
						} else if(sig is IFirstAccountKey firstSig) {
							if(key is IQTeslaWalletKey secretWalletKey) {
								// a first time signature will publish its public key, since there is nothing to refer to
								firstSig.PublicKey = (ByteArray) secretWalletKey.PublicKey;
								firstSig.SecurityCategory = (QTESLASecurityCategory.SecurityCategories) secretWalletKey.SecurityCategory;
							} else {
								throw new ApplicationException("Wallet key is not of secret type.");
							}
						}

					};

					if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Published) {
						setSignature(((IPublishedEnvelopeSignature) transactionEnvelope.Signature).AccountSignature);
					} else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.SingleSecret) {
						setSignature(((ISecretEnvelopeSignature) transactionEnvelope.Signature).AccountSignature);
					} else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Presentation) {
						setSignature(((IPresentationEnvelopeSignature) transactionEnvelope.Signature).AccountSignature);
					} else if(transactionEnvelope.Signature.Version == EnvelopeSignatureTypes.Instance.Joint) {
						// add the first signature
						IPublishedAccountSignature accountSignature = new PublishedAccountSignature();

						setSignature(accountSignature);

						((IJointEnvelopeSignature) transactionEnvelope.Signature).AccountSignatures.Add(accountSignature);
					}
				}

				if(transactionEnvelope.Signature is PresentationEnvelopeSignature presentationEnvelopeSignature) {
					// for the presentation, we create a new key. since its a first one signing itself with no attaches, we can make it weaker
					using(IQTeslaWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.CreatePresentationQTeslaKey(keyName)) {

						SignTransaction(key);
					}
				} else if(transactionEnvelope.Signature is SecretEnvelopeSignature secretEnvelopeSignature) {
					using(ISecretWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<ISecretWalletKey>(keyName)) {

						// important, we dont reuse a secret twice! we must wait until the transaction is confirmed or rejected
						if(key.NextKey == null) {
							throw new ApplicationException("The secret key has already been used and not yet confirmed. we can not use it again until the transaction is confirmed");
						}

						SignTransaction(key);

						// ok, we signed this transaction, so lets add it to our keyLog since our key has changed in the wallet already
						IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(key.AccountUuid);
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertKeyLogTransactionEntry(account, transaction.TransactionId, key.KeyAddress.OrdinalId);
					}
				} else if(transactionEnvelope.Signature is SecretComboEnvelopeSignature secretComboEnvelopeSignature) {
					using(ISecretComboWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<ISecretComboWalletKey>(keyName)) {

						// important, we dont reuse a secret twice! we must wait until the transaction is confirmed or rejected
						if(key.NextKey == null) {
							throw new ApplicationException("The secret key has already been used and not yet confirmed. we can not use it again until the transaction is confirmed");
						}

						SignTransaction(key);

						// ok, we signed this transaction, so lets add it to our keyLog since our key has changed in the wallet already
						IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(key.AccountUuid);
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertKeyLogTransactionEntry(account, transaction.TransactionId, key.KeyAddress.OrdinalId);
					}
				} else {
					using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName)) {

						SignTransaction(key);

						// increment the key use index
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.UpdateLocalChainStateKeyHeight(key);

						// ok, we signed this transaction, so lets add it to our keyLog since our key has changed in the wallet already
						IWalletAccount account = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetWalletAccount(key.AccountUuid);
						this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.InsertKeyLogTransactionEntry(account, transaction.TransactionId, key.KeyAddress.OrdinalId);
					}
				}

				return transactionEnvelope;

			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic transaction signature", ex);
			}
		}

		protected virtual IMessageEnvelope PrepareMessageEnvelope(IBlockchainMessage message) {
			try {

				IMessageEnvelope messageEnvelope = this.CreateNewMessageEnvelope();
				messageEnvelope.Contents = message.Dehydrate(this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.ActiveBlockchainChannels);

				// now as the last step in the building, we hash the entire transaction to get the sakura tree root

				// load our key, and use it to set what we need to
				using(IXmssWalletKey key = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(GlobalsService.MESSAGE_KEY_NAME)) {

					// hash the finalized transaction
					messageEnvelope.Hash = HashingUtils.GenerateHash(message);

					Log.Verbose("Singing message...");

					messageEnvelope.Signature.AccountSignature.KeyAddress = key.KeyAddress.Copy();

					XmssCryptographicKey publicKey = new XmssCryptographicKey();
					publicKey.Key = new ByteArray(key.PublicKey.Length);
					publicKey.Key.CopyFrom(key.PublicKey);
					publicKey.BitSize = (byte) key.HashBits;
					publicKey.TreeHeight = (byte) key.TreeHeight;
					messageEnvelope.Signature.AccountSignature.PublicKey = publicKey;

					// and sign the whole thing with our key
					messageEnvelope.Signature.AccountSignature.Autograph = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.SignMessageXmss(messageEnvelope.Hash, key);

					Log.Verbose("Message successfully signed.");
				}

				return messageEnvelope;

			} catch(Exception ex) {
				throw new ApplicationException("Failed to prepare basic message signature", ex);
			}
		}

		protected IMessageEnvelope GenerateBlockchainMessage(IBlockchainMessage message) {

			// first, ensure that our account has been published. otherwise, we can't use it
			Enums.PublicationStatus status = this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.GetActiveAccount().Status;

			if(status != Enums.PublicationStatus.Published) {
				throw new ApplicationException("Our Account has not yet been published and confirmed. we can not create transactions yet with it.");
			}

			this.PrepareMessageBasics(message);

			IMessageEnvelope envelope = this.PrepareMessageEnvelope(message);

			return envelope;
		}

		protected abstract IStandardPresentationTransaction CreateNewPresentationTransaction();
		protected abstract IStandardAccountKeyChangeTransaction CreateNewKeyChangeTransaction(byte ordinalId);
		protected abstract ITransaction CreateNewDebugTransaction();

		//		protected abstract IKeyChangeTransaction CreateNewChangeKeyTransactionBloc();

		protected ITransactionEnvelope CreateNewTransactionEnvelope(EnvelopeSignatureType signatureType) {
			ITransactionEnvelope envelope = this.CreateNewTransactionEnvelope();

			if(signatureType == EnvelopeSignatureTypes.Instance.Published) {
				envelope.Signature = new PublishedEnvelopeSignature();

			} else if(signatureType == EnvelopeSignatureTypes.Instance.SingleSecret) {
				envelope.Signature = new SecretEnvelopeSignature();

			} else if(signatureType == EnvelopeSignatureTypes.Instance.Presentation) {
				envelope.Signature = new PresentationEnvelopeSignature();

			} else if(signatureType == EnvelopeSignatureTypes.Instance.Joint) {
				envelope.Signature = new JointEnvelopeSignature();

			}

			return envelope;
		}

		protected abstract ITransactionEnvelope CreateNewTransactionEnvelope();

		protected abstract IMessageEnvelope CreateNewMessageEnvelope();

		protected abstract IElectionsRegistrationMessage CreateNewMinerRegistrationMessage();
	}

}