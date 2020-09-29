
using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core {

	public class BlockchainSystemEventType : SimpleUShort<BlockchainSystemEventType> {

		public BlockchainSystemEventType() {
		}

		public BlockchainSystemEventType(ushort value) : base(value) {
		}

		public static implicit operator BlockchainSystemEventType(ushort d) {
			return new BlockchainSystemEventType(d);
		}

		public static bool operator ==(BlockchainSystemEventType a, BlockchainSystemEventType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(BlockchainSystemEventType a, BlockchainSystemEventType b) {
			return !(a == b);
		}
	}

	public class BlockchainSystemEventTypes : UShortConstantSet<BlockchainSystemEventType> {
		public readonly BlockchainSystemEventType AccountCreationEnded;
		public readonly BlockchainSystemEventType AccountCreationError;
		public readonly BlockchainSystemEventType AccountCreationMessage;

		
		public readonly BlockchainSystemEventType AccountCreationStarted;
		public readonly BlockchainSystemEventType AccountCreationStep;
		public readonly BlockchainSystemEventType AccountPublicationEnded;
		public readonly BlockchainSystemEventType AccountPublicationError;
		public readonly BlockchainSystemEventType AccountPublicationMessage;
		
		public readonly BlockchainSystemEventType POWBegin;
		public readonly BlockchainSystemEventType POWIteration;
		public readonly BlockchainSystemEventType POWSolution;

		public readonly BlockchainSystemEventType AccountPublicationStarted;
		public readonly BlockchainSystemEventType AccountPublicationStep;
		public readonly BlockchainSystemEventType AccountStatusUpdated;

		public readonly BlockchainSystemEventType Alert;
		public readonly BlockchainSystemEventType BlockchainSyncEnded;
		public readonly BlockchainSystemEventType BlockchainSyncError;

		public readonly BlockchainSystemEventType BlockchainSyncStarted;
		public readonly BlockchainSystemEventType BlockchainSyncUpdate;

		public readonly BlockchainSystemEventType BlockInserted;
		public readonly BlockchainSystemEventType BlockInterpreted;
		public readonly BlockchainSystemEventType ConnectableStatusChanged;

		public readonly BlockchainSystemEventType DefaultEvent = 0;
		public readonly BlockchainSystemEventType DigestInserted;
		public readonly BlockchainSystemEventType Error;

		public readonly BlockchainSystemEventType ImportantWalletUpdate;
		public readonly BlockchainSystemEventType KeyGenerationEnded;
		public readonly BlockchainSystemEventType KeyGenerationError;
		public readonly BlockchainSystemEventType KeyGenerationMessage;
		public readonly BlockchainSystemEventType KeyGenerationPercentageUpdate;

		public readonly BlockchainSystemEventType KeyGenerationStarted;

		/// <summary>
		///     used for log window messages
		/// </summary>
		public readonly BlockchainSystemEventType Message;

		public readonly BlockchainSystemEventType MiningElected;
		public readonly BlockchainSystemEventType MiningEnded;
		public readonly BlockchainSystemEventType MiningPrimeElected;
		public readonly BlockchainSystemEventType MiningPrimeElectedMissed;

		public readonly BlockchainSystemEventType MiningStarted;
		public readonly BlockchainSystemEventType MiningStatusChanged;

		public readonly BlockchainSystemEventType PeerTotalUpdated;
		public readonly BlockchainSystemEventType RequestCopyKeyFile;
		public readonly BlockchainSystemEventType RequestCopyWallet;
		public readonly BlockchainSystemEventType RequestKeyPassphrase;
		public readonly BlockchainSystemEventType RequestWalletPassphrase;

		/// <summary>
		///     this event is triggered when we find an element that we dont recognize. this signifies our node is surely out of
		///     date and should be upgraded.
		/// </summary>
		public readonly BlockchainSystemEventType RequireNodeUpdate;

		public readonly BlockchainSystemEventType TransactionConfirmed;

		public readonly BlockchainSystemEventType TransactionCreated;
		public readonly BlockchainSystemEventType TransactionError;

		public readonly BlockchainSystemEventType ElectionContextCached;
		public readonly BlockchainSystemEventType ElectionProcessingCompleted;
		public readonly BlockchainSystemEventType TransactionHistoryUpdated;
		public readonly BlockchainSystemEventType TransactionMessage;
		public readonly BlockchainSystemEventType TransactionReceived;
		public readonly BlockchainSystemEventType TransactionRefused;
		public readonly BlockchainSystemEventType TransactionSent;
		public readonly BlockchainSystemEventType WalletCreationEnded;
		public readonly BlockchainSystemEventType WalletCreationError;
		public readonly BlockchainSystemEventType WalletCreationMessage;

		public readonly BlockchainSystemEventType WalletCreationStarted;
		public readonly BlockchainSystemEventType WalletCreationStep;

		public readonly BlockchainSystemEventType WalletLoadingEnded;
		public readonly BlockchainSystemEventType WalletLoadingError;
		public readonly BlockchainSystemEventType WalletLoadingStarted;

		public readonly BlockchainSystemEventType WalletSyncEnded;
		public readonly BlockchainSystemEventType WalletSyncError;

		public readonly BlockchainSystemEventType WalletSyncStarted;
		public readonly BlockchainSystemEventType WalletSyncUpdate;
		
		
		public readonly BlockchainSystemEventType RequestShutdown;
		
		
		#region appointments & puzzles
			
			public readonly BlockchainSystemEventType AppointmentRequestSent;
			public readonly BlockchainSystemEventType AppointmentRequestConfirmed;
			public readonly BlockchainSystemEventType AppointmentContextCached;
			public readonly BlockchainSystemEventType AppointmentPuzzlePreparation;
			public readonly BlockchainSystemEventType AppointmentPuzzleBegin;
			public readonly BlockchainSystemEventType AppointmentPuzzleCompleted;
			public readonly BlockchainSystemEventType AppointmentVerificationRequestCompleted;
			public readonly BlockchainSystemEventType AppointmentVerificationCompleted;
			public readonly BlockchainSystemEventType InvalidPuzzleEngineVersion;
			
		#endregion
		

		static BlockchainSystemEventTypes() {
		}

		protected BlockchainSystemEventTypes() : base(1000) {

			this.WalletLoadingStarted = this.CreateBaseConstant();
			this.WalletLoadingEnded = this.CreateBaseConstant();
			this.WalletLoadingError = this.CreateBaseConstant();
			this.RequestWalletPassphrase = this.CreateBaseConstant();
			this.RequestKeyPassphrase = this.CreateBaseConstant();

			this.RequestCopyWallet = this.CreateBaseConstant();
			this.PeerTotalUpdated = this.CreateBaseConstant();

			this.WalletCreationStarted = this.CreateBaseConstant(100);
			this.WalletCreationEnded = this.CreateBaseConstant();
			this.WalletCreationMessage = this.CreateBaseConstant();
			this.WalletCreationStep = this.CreateBaseConstant();
			this.WalletCreationError = this.CreateBaseConstant();

			this.AccountCreationStarted = this.CreateBaseConstant();
			this.AccountCreationEnded = this.CreateBaseConstant();
			this.AccountCreationMessage = this.CreateBaseConstant();
			this.AccountCreationStep = this.CreateBaseConstant();
			this.AccountCreationError = this.CreateBaseConstant();

			this.KeyGenerationStarted = this.CreateBaseConstant();
			this.KeyGenerationEnded = this.CreateBaseConstant();
			this.KeyGenerationMessage = this.CreateBaseConstant();
			this.KeyGenerationPercentageUpdate = this.CreateBaseConstant();
			this.KeyGenerationError = this.CreateBaseConstant();

			this.AccountStatusUpdated = this.CreateBaseConstant();
			
			this.AccountPublicationStarted = this.CreateBaseConstant();
			this.AccountPublicationEnded = this.CreateBaseConstant();
			this.AccountPublicationMessage = this.CreateBaseConstant();
			this.AccountPublicationStep = this.CreateBaseConstant();
			this.AccountPublicationError = this.CreateBaseConstant();

			this.WalletSyncStarted = this.CreateBaseConstant();
			this.WalletSyncEnded = this.CreateBaseConstant();
			this.WalletSyncUpdate = this.CreateBaseConstant();
			this.WalletSyncError = this.CreateBaseConstant();

			this.TransactionSent = this.CreateBaseConstant();
			this.TransactionCreated = this.CreateBaseConstant();
			this.TransactionConfirmed = this.CreateBaseConstant();
			this.TransactionReceived = this.CreateBaseConstant();
			this.TransactionMessage = this.CreateBaseConstant();
			this.TransactionRefused = this.CreateBaseConstant();
			this.TransactionError = this.CreateBaseConstant();

			this.BlockchainSyncStarted = this.CreateBaseConstant();
			this.BlockchainSyncEnded = this.CreateBaseConstant();
			this.BlockchainSyncUpdate = this.CreateBaseConstant();
			this.BlockchainSyncError = this.CreateBaseConstant();

			this.MiningStarted = this.CreateBaseConstant();
			this.MiningEnded = this.CreateBaseConstant();
			this.MiningElected = this.CreateBaseConstant();
			this.MiningPrimeElected = this.CreateBaseConstant();
			this.MiningPrimeElectedMissed = this.CreateBaseConstant();
			this.MiningStatusChanged = this.CreateBaseConstant();

			this.BlockInserted = this.CreateBaseConstant();
			this.DigestInserted = this.CreateBaseConstant();

			this.BlockInterpreted = this.CreateBaseConstant();

			this.Message = this.CreateBaseConstant();
			this.Error = this.CreateBaseConstant();

			this.RequestCopyKeyFile = this.CreateBaseConstant();

			this.Alert = this.CreateBaseConstant();
			this.ConnectableStatusChanged = this.CreateBaseConstant();

			this.RequireNodeUpdate = this.CreateBaseConstant();

			this.ImportantWalletUpdate = this.CreateBaseConstant();

			this.TransactionHistoryUpdated = this.CreateBaseConstant();
			
			this.ElectionContextCached = this.CreateBaseConstant();
			this.ElectionProcessingCompleted = this.CreateBaseConstant();
			
			this.RequestShutdown = this.CreateBaseConstant();
			
			this.POWBegin = this.CreateBaseConstant();
			this.POWIteration = this.CreateBaseConstant();
			this.POWSolution = this.CreateBaseConstant();
			
			this.AppointmentRequestSent = this.CreateBaseConstant();
			this.AppointmentRequestConfirmed = this.CreateBaseConstant();
			this.AppointmentContextCached = this.CreateBaseConstant();
			
			this.AppointmentPuzzlePreparation = this.CreateBaseConstant();
			this.AppointmentPuzzleBegin = this.CreateBaseConstant();
			this.AppointmentPuzzleCompleted = this.CreateBaseConstant();
			
			this.AppointmentVerificationRequestCompleted = this.CreateBaseConstant();
			this.AppointmentVerificationCompleted = this.CreateBaseConstant();
			this.InvalidPuzzleEngineVersion = this.CreateBaseConstant();
		}

		public static BlockchainSystemEventTypes Instance { get; } = new BlockchainSystemEventTypes();
	}
}