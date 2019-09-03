using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories {
	public interface IChainTypeCreationFactory {
		IWalletAccount CreateNewWalletAccount();

		IWalletKey CreateNewWalletKey(Enums.KeyTypes keyType);
		IXmssWalletKey CreateNewXmssWalletKey();
		IXmssMTWalletKey CreateNewXmssMTWalletKey();
		IQTeslaWalletKey CreateNewQTeslaWalletKey();
		ISecretWalletKey CreateNewSecretWalletKey();
		ISecretComboWalletKey CreateNewSecretComboWalletKey();
		ISecretDoubleWalletKey CreateNewSecretDoubleWalletKey();
		ISecretPentaWalletKey CreateNewSecretPentaWalletKey();
		INtruWalletKey CreateNewNtruWalletKey();
		IMcElieceWalletKey CreateNewMcElieceWalletKey();

		IUserWallet CreateNewUserWallet();
		WalletKeyHistory CreateNewWalletKeyHistory();

		WalletAccountKeyLog CreateNewWalletAccountKeyLog();
		IWalletTransactionCache CreateNewWalletAccountTransactionCache();
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

		public IWalletKey CreateNewWalletKey(Enums.KeyTypes keyType) {
			IWalletKey key = null;

			if(keyType == Enums.KeyTypes.XMSS) {
				key = this.CreateNewXmssWalletKey();
			}

			if(keyType == Enums.KeyTypes.XMSSMT) {
				key = this.CreateNewXmssMTWalletKey();
			}

			if(keyType == Enums.KeyTypes.QTESLA) {
				key = this.CreateNewQTeslaWalletKey();
			}

			if(keyType == Enums.KeyTypes.Secret) {
				key = this.CreateNewSecretWalletKey();
			}

			if(keyType == Enums.KeyTypes.SecretCombo) {
				key = this.CreateNewSecretComboWalletKey();
			}

			if(keyType == Enums.KeyTypes.SecretDouble) {
				key = this.CreateNewSecretDoubleWalletKey();
			}

			if(keyType == Enums.KeyTypes.SecretPenta) {
				key = this.CreateNewSecretPentaWalletKey();
			}

			if(keyType == Enums.KeyTypes.NTRU) {
				key = this.CreateNewNtruWalletKey();
			}

			if(keyType == Enums.KeyTypes.MCELIECE) {
				key = this.CreateNewMcElieceWalletKey();
			}

			if(key == null) {
				throw new ApplicationException("Unsupported key type");
			}

			key.KeyType = keyType;

			return key;
		}

		public abstract IWalletAccount CreateNewWalletAccount();
		public abstract IXmssWalletKey CreateNewXmssWalletKey();
		public abstract IXmssMTWalletKey CreateNewXmssMTWalletKey();
		public abstract IQTeslaWalletKey CreateNewQTeslaWalletKey();
		public abstract ISecretWalletKey CreateNewSecretWalletKey();
		public abstract ISecretComboWalletKey CreateNewSecretComboWalletKey();
		public abstract ISecretDoubleWalletKey CreateNewSecretDoubleWalletKey();
		public abstract ISecretPentaWalletKey CreateNewSecretPentaWalletKey();
		public abstract INtruWalletKey CreateNewNtruWalletKey();
		public abstract IMcElieceWalletKey CreateNewMcElieceWalletKey();

		public abstract IUserWallet CreateNewUserWallet();
		public abstract WalletKeyHistory CreateNewWalletKeyHistory();

		public abstract WalletAccountKeyLog CreateNewWalletAccountKeyLog();
		public abstract WalletAccountChainState CreateNewWalletAccountChainState();
		public abstract IWalletAccountChainStateKey CreateNewWalletAccountChainStateKey();
		public abstract IWalletTransactionCache CreateNewWalletAccountTransactionCache();
		public abstract IWalletTransactionHistory CreateNewWalletAccountTransactionHistory();
		public abstract IWalletElectionsHistory CreateNewWalletElectionsHistoryEntry();
		public abstract WalletElectionCache CreateNewWalletAccountElectionCache();

		public abstract IWalletStandardAccountSnapshot CreateNewWalletAccountSnapshot();
		public abstract IWalletJointAccountSnapshot CreateNewWalletJointAccountSnapshot();

		public abstract IEventPoolProvider CreateBlockchainEventPoolProvider(IChainMiningStatusProvider miningStatusProvider);

		public abstract IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChainDataWriteProvider();
	}
}