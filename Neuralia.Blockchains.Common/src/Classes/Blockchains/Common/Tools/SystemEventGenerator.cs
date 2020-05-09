using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Validation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public class SystemEventGenerator {

		//TODO: this event system was done very quickly and requires a good refactor.

		protected SystemEventGenerator() {

		}

		public BlockchainSystemEventType EventType { get; protected set; }

		public object[] Parameters { get; protected set; }

		public static SystemEventGenerator CreateErrorMessage(BlockchainSystemEventType eventType, string message) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = eventType;

			generator.Parameters = new object[] {message = message};

			return generator;
		}

		public static SystemEventGenerator WalletLoadingStartedEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletLoadingStarted;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator WalletLoadingEndedEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletLoadingEnded;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator WalletLoadingErrorEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletLoadingError;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator WalletCreationStartedEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletCreationStarted;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator WalletCreationStepEvent(string stepName, CreationStepSet creationStepSet) {
			return WalletCreationStepEvent(stepName, creationStepSet.CurrentIncrementStep(), creationStepSet.Total);
		}

		public static SystemEventGenerator WalletCreationStepEvent(string stepName, int stepIndex, int stepTotal) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletCreationStep;

			generator.Parameters = new object[] {new {stepName, stepIndex, stepTotal}};

			return generator;
		}

		public static SystemEventGenerator WalletCreationEndedEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletCreationEnded;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator WalletCreationErrorEvent(string message, string exception) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletCreationError;

			generator.Parameters = new object[] {new {message, exception}};

			return generator;
		}

		// accounts

		public static SystemEventGenerator AccountCreationStartedEvent() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.AccountCreationStarted;

			generator.Parameters = new object[] { };

			return generator;
		}

		public static SystemEventGenerator AccountCreationStepEvent(string stepName, int stepIndex, int stepTotal) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.AccountCreationStep;

			generator.Parameters = new object[] {new {stepName, stepIndex, stepTotal}};

			return generator;
		}

		public static SystemEventGenerator AccountCreationStepEvent(string stepName, CreationStepSet creationStepSet) {
			return AccountCreationStepEvent(stepName, creationStepSet.CurrentIncrementStep(), creationStepSet.Total);
		}

		public static SystemEventGenerator AccountCreationEndedEvent(Guid accountUuid) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.AccountCreationEnded;

			generator.Parameters = new object[] {new {accountUuid}};

			return generator;
		}

		public static SystemEventGenerator AccountPublicationStepEvent(string stepName, int stepIndex, int stepTotal) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.AccountPublicationStep;

			generator.Parameters = new object[] {new {stepName, stepIndex, stepTotal}};

			return generator;
		}

		public static SystemEventGenerator KeyGenerationPercentageEvent(string keyName, int percentage) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.KeyGenerationPercentageUpdate;

			generator.Parameters = new object[] {keyName, percentage};

			return generator;
		}

		public static SystemEventGenerator BlockchainSyncStarted(long blockId, long publicBlockHeight) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.BlockchainSyncStarted;
			generator.Parameters = new object[] {blockId, publicBlockHeight, publicBlockHeight == 0 ? 0 : (decimal) blockId / publicBlockHeight};

			return generator;
		}

		public static SystemEventGenerator BlockchainSyncEnded(long blockId, long publicBlockHeight) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.BlockchainSyncEnded;
			generator.Parameters = new object[] {blockId, publicBlockHeight, publicBlockHeight == 0 ? 0 : (decimal) blockId / publicBlockHeight};

			return generator;
		}

		public static SystemEventGenerator BlockchainSyncUpdate(long blockId, long publicBlockHeight, string estimatedTimeRemaining) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.BlockchainSyncUpdate;
			generator.Parameters = new object[] {blockId, publicBlockHeight, publicBlockHeight == 0 ? 0 : (decimal) blockId / publicBlockHeight, estimatedTimeRemaining};

			return generator;
		}

		public static SystemEventGenerator WalletSyncStarted(long blockId, long blockHeight) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletSyncStarted;
			generator.Parameters = new object[] {blockId, blockHeight, (decimal) blockId / Math.Max(blockHeight, 1)};

			return generator;
		}

		public static SystemEventGenerator WalletSyncEnded(long blockId, long blockHeight) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletSyncEnded;
			generator.Parameters = new object[] {blockId, blockHeight, (decimal) blockId / Math.Max(blockHeight, 1)};

			return generator;
		}

		public static SystemEventGenerator WalletSyncStepEvent(long blockId, long blockHeight, string estimatedTimeRemaining) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.WalletSyncUpdate;
			generator.Parameters = new object[] {blockId, blockHeight, (decimal) blockId / Math.Max(blockHeight, 1), estimatedTimeRemaining};

			return generator;
		}

		public static SystemEventGenerator MiningElected(long electionBlockId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.MiningElected;
			generator.Parameters = new object[] {electionBlockId, (byte) ChainMiningProvider.MiningEventLevel.Level2};

			return generator;
		}

		public static SystemEventGenerator MiningPrimeElected(long electionBlockId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.MiningPrimeElected;
			generator.Parameters = new object[] {electionBlockId, (byte) ChainMiningProvider.MiningEventLevel.Level1};

			return generator;
		}

		public static SystemEventGenerator MininPrimeElectedMissed(long publicationBlockId, long electionBlockId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.MiningPrimeElectedMissed;
			generator.Parameters = new object[] {publicationBlockId, electionBlockId, (byte) ChainMiningProvider.MiningEventLevel.Level2};

			return generator;
		}

		public static SystemEventGenerator MiningEnded(Enums.MiningStatus status) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.MiningEnded;
			generator.Parameters = new object[] {(int) status};

			return generator;
		}

		public static SystemEventGenerator MiningStatusChanged(bool mining, Enums.MiningStatus status) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.MiningStatusChanged;
			generator.Parameters = new object[] {mining, (int) status};

			return generator;
		}

		public static SystemEventGenerator BlockInserted(long blockId, DateTime timestamp, string hash, long publicBlockId, int lifespan) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.BlockInserted;
			generator.Parameters = new object[] {blockId, timestamp, hash, publicBlockId, lifespan};

			return generator;
		}

		public static SystemEventGenerator BlockInterpreted(long blockId, DateTime timestamp, string hash, long publicBlockId, int lifespan) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.BlockInterpreted;
			generator.Parameters = new object[] {blockId, timestamp, hash, publicBlockId, lifespan};

			return generator;
		}

		public static SystemEventGenerator DigestInserted(int digestId, DateTime timestamp, string hash) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.DigestInserted;
			generator.Parameters = new object[] {digestId, timestamp, hash};

			return generator;
		}

		public static SystemEventGenerator ElectionContextCached(BlockchainType chainId, long blockId, long maturityId, long difficulty) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.ElectionContextCached;
			generator.Parameters = new object[] {chainId.Value, blockId, maturityId, difficulty};

			return generator;
		}
		
		public static SystemEventGenerator ElectionProcessingCompleted(BlockchainType chainId, long blockId, int electionResultCount) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.ElectionProcessingCompleted;
			generator.Parameters = new object[] {chainId.Value, blockId, electionResultCount};

			return generator;
		}
		
		
		public static SystemEventGenerator RaiseAlert(int messageCode) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.Alert;
			generator.Parameters = new object[] {messageCode};

			return generator;
		}

		public static SystemEventGenerator ConnectableChanged(bool connectable) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.ConnectableStatusChanged;
			generator.Parameters = new object[] {connectable};

			return generator;
		}

		public static SystemEventGenerator RequireNodeUpdate(ushort chainId, string chainName) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.RequireNodeUpdate;
			generator.Parameters = new object[] {chainId, chainName};

			return generator;
		}
		
		public static SystemEventGenerator RequestShutdown() {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.RequestShutdown;
			generator.Parameters = new object[] {};

			return generator;
		}

		public static SystemEventGenerator TransactionHistoryUpdated(BlockchainType chainId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionHistoryUpdated;
			generator.Parameters = new object[] {chainId.Value};

			return generator;
		}

		public class CreationStepSet {

			public CreationStepSet(int total) {
				this.Total = total;
			}

			public int CurrentStep { get; private set; } = 1;
			public int Total { get; }

			public int CurrentIncrementStep() {
				int step = this.CurrentStep;
				this.CurrentStep += 1;

				return step;
			}
		}

		public class WalletCreationStepSet : CreationStepSet {

			public WalletCreationStepSet() : base(6) {

			}

			public SystemEventGenerator AccountCreationStartedStep => WalletCreationStepEvent("Account Creation Started", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator AccountCreationEndedStep => WalletCreationStepEvent("Account Creation Stated", this.CurrentIncrementStep(), this.Total);

			public SystemEventGenerator CreatingFiles => WalletCreationStepEvent("Creating wallet files", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator SavingWallet => WalletCreationStepEvent("Saving wallet", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator CreatingAccountKeys => WalletCreationStepEvent("Creating account keys", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator AccountKeysCreated => WalletCreationStepEvent("Account keys created", this.CurrentIncrementStep(), this.Total);
		}

		public class AccountCreationStepSet : CreationStepSet {

			public AccountCreationStepSet() : base(5) {

			}

			public SystemEventGenerator CreatingFiles => AccountCreationStepEvent("Creating account files", this.CurrentIncrementStep(), this.Total);

			public SystemEventGenerator CreatingTransactionKey => AccountCreationStepEvent("Creating transaction key", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator CreatingMessageKey => AccountCreationStepEvent("Creating message key", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator CreatingChangeKey => AccountCreationStepEvent("Creating change key", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator CreatingSuperKey => AccountCreationStepEvent("Creating super key", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator KeysCreated => AccountCreationStepEvent("keys created", this.CurrentIncrementStep(), this.Total);
		}

		public class AccountPublicationStepSet : CreationStepSet {

			public AccountPublicationStepSet() : base(5) {

			}

			public SystemEventGenerator CreatingPresentationTransaction => AccountPublicationStepEvent("Creating Presentation Transaction", this.CurrentIncrementStep(), this.Total);
			public SystemEventGenerator PerformingPOW => AccountPublicationStepEvent("Performing Proof of work", this.CurrentIncrementStep(), this.Total);

			public SystemEventGenerator PerformingPOWIteration(int nonce, int difficulty) {
				SystemEventGenerator generator = new SystemEventGenerator();

				generator.EventType = BlockchainSystemEventTypes.Instance.AccountPublicationPOWNonceIteration;
				generator.Parameters = new object[] {nonce, difficulty};

				return generator;
			}

			public SystemEventGenerator FoundPOWSolution(int nonce, int difficulty, List<int> powSolutions) {
				SystemEventGenerator generator = new SystemEventGenerator();

				generator.EventType = BlockchainSystemEventTypes.Instance.AccountPublicationPOWNonceFound;
				generator.Parameters = new object[] {nonce, difficulty, powSolutions};

				return generator;
			}
		}

	#region Transactions

		public static SystemEventGenerator TransactionConfirmed(TransactionId transactionId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionConfirmed;
			generator.Parameters = new object[] {new {transactionId = transactionId != null ? transactionId.ToString() : ""}};

			return generator;
		}

		public static SystemEventGenerator TransactionRefused(TransactionId transactionId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionRefused;
			generator.Parameters = new object[] {new {transactionId = transactionId != null ? transactionId.ToString() : ""}};

			return generator;
		}

		public static SystemEventGenerator TransactionCreated(TransactionId transactionId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionCreated;
			generator.Parameters = new object[] {transactionId != null ? transactionId.ToString() : ""};

			return generator;
		}

		public static SystemEventGenerator TransactionSent(TransactionId transactionId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionSent;
			generator.Parameters = new object[] {transactionId != null ? transactionId.ToString() : ""};

			return generator;
		}

		public static SystemEventGenerator TransactionReceived(List<AccountId> impactedLocalPublishedAccounts, List<Guid> impactedLocalPublishedAccountsUuids, List<AccountId> impactedLocalDispatchedAccounts, List<Guid> impactedLocalDispatchedAccountsUuids, TransactionId transactionId) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionReceived;

			generator.Parameters = new object[] {transactionId != null ? transactionId.ToString() : "", /*transactionId*/ impactedLocalPublishedAccounts.Select(a => a.ToString()).ToArray(), /*impactedLocalPublishedAccounts*/ impactedLocalPublishedAccountsUuids.ToArray(), /*impactedLocalPublishedAccountsUuids*/ impactedLocalPublishedAccounts.Select(a => a.ToString()).ToArray(), /*impactedLocalDispatchedAccounts*/ impactedLocalPublishedAccounts.ToArray() /*impactedLocalDispatchedAccountsUuids*/};

			return generator;
		}

		public static SystemEventGenerator TransactionError(TransactionId transactionId, ValidationResult validationResult) {
			SystemEventGenerator generator = new SystemEventGenerator();

			generator.EventType = BlockchainSystemEventTypes.Instance.TransactionError;
			generator.Parameters = new object[] {transactionId != null ? transactionId.ToString() : "", (validationResult?.ErrorCodes!=null?validationResult.ErrorCodes.Select(e => e.Value).ToList():new List<ushort>())};

			return generator;
		}

	#endregion

	}
}