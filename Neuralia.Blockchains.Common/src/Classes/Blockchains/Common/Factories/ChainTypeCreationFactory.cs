using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories {
	public interface IChainTypeCreationFactory {
		IWalletAccount CreateNewWalletAccount();

		IValidatorProtocol CreateValidatorProtocol(BlockchainType blockchainType, Enums.AppointmentValidationProtocols Protocol);
		
		IWalletKey CreateNewWalletKey(CryptographicKeyType keyType);
		IXmssWalletKey CreateNewXmssWalletKey();
		IXmssMTWalletKey CreateNewXmssMTWalletKey();
		ISecretWalletKey CreateNewSecretWalletKey();
		ISecretComboWalletKey CreateNewSecretComboWalletKey();
		ISecretDoubleWalletKey CreateNewSecretDoubleWalletKey();
		ISecretPentaWalletKey CreateNewSecretPentaWalletKey();
		INTRUPrimeWalletKey CreateNewNTRUPrimeWalletKey();
		IMcElieceWalletKey CreateNewMcElieceWalletKey();

		IUserWallet CreateNewUserWallet();
		WalletKeyHistory CreateNewWalletKeyHistory();

		WalletAccountKeyLog CreateNewWalletAccountKeyLog();
		IWalletGenerationCache CreateNewWalletAccountGenerationCache();
		IWalletTransactionHistory CreateNewWalletAccountTransactionHistory();
		IWalletElectionsHistory CreateNewWalletElectionsHistoryEntry();
		WalletElectionCache CreateNewWalletAccountElectionCache();

		WalletAccountChainState CreateNewWalletAccountChainState();
		IWalletAccountChainStateKey CreateNewWalletAccountChainStateKey();

		IWalletStandardAccountSnapshot CreateNewWalletAccountSnapshot();
		IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot();

		IEventPoolProvider CreateBlockchainEventPoolProvider(IChainMiningStatusProvider miningStatusProvider);
	}

	public interface IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainTypeCreationFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		CENTRAL_COORDINATOR CentralCoordinator { get; }

		IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChainDataWriteProvider();
		
	}

	public abstract class ChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainTypeCreationFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public ChainTypeCreationFactory(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;
		}

		public CENTRAL_COORDINATOR CentralCoordinator { get; }

		public IValidatorProtocol CreateValidatorProtocol(BlockchainType blockchainType, Enums.AppointmentValidationProtocols Protocol) {
			if(Protocol == Enums.AppointmentValidationProtocols.Undefined || Protocol == Enums.AppointmentValidationProtocols.Standard) {
				return new ValidatorProtocol1(blockchainType);
			}
			else if(Protocol == Enums.AppointmentValidationProtocols.Backup) {
				return new ValidatorRESTProtocol1(blockchainType, GlobalSettings.ApplicationSettings.ValidatorHttpPort);
			}

			throw new ArgumentException();
		}

		public IWalletKey CreateNewWalletKey(CryptographicKeyType keyType) {
			IWalletKey key = null;

			if(keyType == CryptographicKeyTypes.Instance.XMSS) {
				key = this.CreateNewXmssWalletKey();
			}

			if(keyType == CryptographicKeyTypes.Instance.XMSSMT) {
				key = this.CreateNewXmssMTWalletKey();
			}
			
			if(keyType == CryptographicKeyTypes.Instance.NTRUPrime) {
				key = this.CreateNewNTRUPrimeWalletKey();
			}
			
			if(keyType == CryptographicKeyTypes.Instance.MCELIECE) {
				key = this.CreateNewMcElieceWalletKey();
			}

			if(keyType == CryptographicKeyTypes.Instance.Secret) {
				key = this.CreateNewSecretWalletKey();
			}

			if(keyType == CryptographicKeyTypes.Instance.SecretCombo) {
				key = this.CreateNewSecretComboWalletKey();
			}

			if(keyType == CryptographicKeyTypes.Instance.SecretDouble) {
				key = this.CreateNewSecretDoubleWalletKey();
			}

			if(keyType == CryptographicKeyTypes.Instance.SecretPenta) {
				key = this.CreateNewSecretPentaWalletKey();
			}
			if(keyType == CryptographicKeyTypes.Instance.TripleXMSS) {
				key = this.CreateNewTripleXmssWalletKey();
			}

			if(key == null) {
				throw new ApplicationException("Unsupported key type");
			}
			
			return key;
		}

		public abstract IWalletAccount CreateNewWalletAccount();
		public abstract IXmssWalletKey CreateNewXmssWalletKey();
		public abstract IXmssMTWalletKey CreateNewXmssMTWalletKey();
		public abstract ISecretWalletKey CreateNewSecretWalletKey();
		public abstract ISecretComboWalletKey CreateNewSecretComboWalletKey();
		public abstract ISecretDoubleWalletKey CreateNewSecretDoubleWalletKey();
		public abstract ISecretPentaWalletKey CreateNewSecretPentaWalletKey();
		public abstract ITripleXmssWalletKey CreateNewTripleXmssWalletKey();
		public abstract INTRUPrimeWalletKey CreateNewNTRUPrimeWalletKey();
		public abstract IMcElieceWalletKey CreateNewMcElieceWalletKey();

		public abstract IUserWallet CreateNewUserWallet();
		public abstract WalletKeyHistory CreateNewWalletKeyHistory();

		public abstract WalletAccountKeyLog CreateNewWalletAccountKeyLog();
		public abstract WalletAccountChainState CreateNewWalletAccountChainState();
		public abstract IWalletAccountChainStateKey CreateNewWalletAccountChainStateKey();
		public abstract IWalletGenerationCache CreateNewWalletAccountGenerationCache();
		public abstract IWalletTransactionHistory CreateNewWalletAccountTransactionHistory();
		public abstract IWalletElectionsHistory CreateNewWalletElectionsHistoryEntry();
		public abstract WalletElectionCache CreateNewWalletAccountElectionCache();

		public abstract IWalletStandardAccountSnapshot CreateNewWalletAccountSnapshot();
		public abstract IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot();

		public abstract IEventPoolProvider CreateBlockchainEventPoolProvider(IChainMiningStatusProvider miningStatusProvider);

		public abstract IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChainDataWriteProvider();
	}
}