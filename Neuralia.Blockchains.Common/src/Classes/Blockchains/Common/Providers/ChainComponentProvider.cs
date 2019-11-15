using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		IWalletProviderProxy WalletProviderBase { get; }
		IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> AssemblyProviderBase { get; }
		IChainStateProvider ChainStateProviderBase { get; }
		IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainFactoryProviderBase { get; }
		IChainConfigurationProvider ChainConfigurationProviderBase { get; }
		IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainValidationProviderBase { get; }
		IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainMiningProviderBase { get; }
		IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataLoadProviderBase { get; }
		IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataWriteProviderBase { get; }
		IAccreditationCertificateProvider AccreditationCertificateProviderBase { get; }
		IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainNetworkingProviderBase { get; }

		IAccountSnapshotsProvider AccountSnapshotsProviderBase { get; }
		IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> InterpretationProviderBase { get; }

		ICardUtils CardUtils { get; }
	}

	public interface IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, out WALLET_PROVIDER_PROXY, out ASSEMBLY_PROVIDER, out MAIN_FACTORY_PROVIDER, out CHAIN_STATE_PROVIDER, out CHAIN_CONFIGURATION_PROVIDER, out CHAIN_VALIDATION_PROVIDER, out CHAIN_MINING_PROVIDER, out CHAIN_LOADING_PROVIDER, out CHAIN_WRITING_PROVIDER, out ACCREDITATION_CERTIFICATE_PROVIDER, out ACCOUNT_SNAPSHOTS_PROVIDER, out CHAIN_NETWORKING_PROVIDER, out INTERPRETATION_PROVIDER> : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WALLET_PROVIDER_PROXY : IWalletProviderProxy
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where MAIN_FACTORY_PROVIDER : IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_STATE_PROVIDER : IChainStateProvider
		where CHAIN_CONFIGURATION_PROVIDER : IChainConfigurationProvider
		where CHAIN_VALIDATION_PROVIDER : IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_MINING_PROVIDER : IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_LOADING_PROVIDER : IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_WRITING_PROVIDER : CHAIN_LOADING_PROVIDER, IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCREDITATION_CERTIFICATE_PROVIDER : IAccreditationCertificateProvider
		where ACCOUNT_SNAPSHOTS_PROVIDER : IAccountSnapshotsProvider
		where CHAIN_NETWORKING_PROVIDER : IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where INTERPRETATION_PROVIDER : IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		WALLET_PROVIDER_PROXY WalletProvider { get; }
		ASSEMBLY_PROVIDER AssemblyProvider { get; }
		MAIN_FACTORY_PROVIDER ChainFactoryProvider { get; }
		CHAIN_STATE_PROVIDER ChainStateProvider { get; }
		CHAIN_CONFIGURATION_PROVIDER ChainConfigurationProvider { get; }
		CHAIN_VALIDATION_PROVIDER ChainValidationProvider { get; }
		CHAIN_MINING_PROVIDER ChainMiningProvider { get; }
		CHAIN_LOADING_PROVIDER ChainDataLoadProvider { get; }
		CHAIN_WRITING_PROVIDER ChainDataWriteProvider { get; }
		ACCREDITATION_CERTIFICATE_PROVIDER AccreditationCertificateProvider { get; }

		ACCOUNT_SNAPSHOTS_PROVIDER AccountSnapshotsProvider { get; }

		CHAIN_NETWORKING_PROVIDER ChainNetworkingProvider { get; }

		INTERPRETATION_PROVIDER InterpretationProvider { get; }
	}

	/// <summary>
	///     The main bucket to store all components used by the chain
	/// </summary>
	public abstract class ChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER_PROXY, ASSEMBLY_PROVIDER, MAIN_FACTORY_PROVIDER, CHAIN_STATE_PROVIDER, CHAIN_CONFIGURATION_PROVIDER, CHAIN_VALIDATION_PROVIDER, CHAIN_MINING_PROVIDER, CHAIN_LOADING_PROVIDER, CHAIN_WRITING_PROVIDER, ACCREDITATION_CERTIFICATE_PROVIDER, ACCOUNT_SNAPSHOTS_PROVIDER, CHAIN_NETWORKING_PROVIDER, INTERPRETATION_PROVIDER> : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER_PROXY, ASSEMBLY_PROVIDER, MAIN_FACTORY_PROVIDER, CHAIN_STATE_PROVIDER, CHAIN_CONFIGURATION_PROVIDER, CHAIN_VALIDATION_PROVIDER, CHAIN_MINING_PROVIDER, CHAIN_LOADING_PROVIDER, CHAIN_WRITING_PROVIDER, ACCREDITATION_CERTIFICATE_PROVIDER, ACCOUNT_SNAPSHOTS_PROVIDER, CHAIN_NETWORKING_PROVIDER, INTERPRETATION_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where WALLET_PROVIDER_PROXY : IWalletProviderProxy
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where MAIN_FACTORY_PROVIDER : IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_STATE_PROVIDER : IChainStateProvider
		where CHAIN_CONFIGURATION_PROVIDER : IChainConfigurationProvider
		where CHAIN_VALIDATION_PROVIDER : IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_MINING_PROVIDER : IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_LOADING_PROVIDER : IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_WRITING_PROVIDER : CHAIN_LOADING_PROVIDER, IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ACCREDITATION_CERTIFICATE_PROVIDER : IAccreditationCertificateProvider
		where ACCOUNT_SNAPSHOTS_PROVIDER : IAccountSnapshotsProvider
		where CHAIN_NETWORKING_PROVIDER : IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where INTERPRETATION_PROVIDER : IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		public ChainComponentProvider(WALLET_PROVIDER_PROXY walletProviderProxy, ASSEMBLY_PROVIDER assemblyProvider, MAIN_FACTORY_PROVIDER chainFactoryProvider, CHAIN_STATE_PROVIDER chainStateProvider, CHAIN_CONFIGURATION_PROVIDER chainConfigurationProvider, CHAIN_VALIDATION_PROVIDER chainValidationProvider, CHAIN_MINING_PROVIDER chainMiningProvider, CHAIN_LOADING_PROVIDER chainDataLoadProvider, ACCREDITATION_CERTIFICATE_PROVIDER accreditationCertificateProvider, ACCOUNT_SNAPSHOTS_PROVIDER accountSnapshotsProvider, CHAIN_NETWORKING_PROVIDER chainNetworkingProvider, INTERPRETATION_PROVIDER interpretationProvider) {
			this.WalletProvider = walletProviderProxy;
			this.AssemblyProvider = assemblyProvider;
			this.ChainFactoryProvider = chainFactoryProvider;
			this.ChainStateProvider = chainStateProvider;
			this.ChainConfigurationProvider = chainConfigurationProvider;
			this.ChainValidationProvider = chainValidationProvider;
			this.ChainMiningProvider = chainMiningProvider;
			this.ChainDataLoadProvider = chainDataLoadProvider;
			this.AccreditationCertificateProvider = accreditationCertificateProvider;
			this.AccountSnapshotsProvider = accountSnapshotsProvider;
			this.ChainNetworkingProvider = chainNetworkingProvider;
			this.InterpretationProvider = interpretationProvider;
		}

		public IWalletProviderProxy WalletProviderBase => this.WalletProvider;

		public IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> AssemblyProviderBase => this.AssemblyProvider;

		public IChainFactoryProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainFactoryProviderBase => this.ChainFactoryProvider;

		public IChainStateProvider ChainStateProviderBase {
			get => this.ChainStateProvider;
			set => throw new NotImplementedException();
		}

		public WALLET_PROVIDER_PROXY WalletProvider { get; }

		public IChainConfigurationProvider ChainConfigurationProviderBase => this.ChainConfigurationProvider;

		public IChainValidationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainValidationProviderBase => this.ChainValidationProvider;
		public IChainMiningProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainMiningProviderBase => this.ChainMiningProvider;
		

		public IChainNetworkingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainNetworkingProviderBase => this.ChainNetworkingProvider;

		public IChainDataLoadProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataLoadProviderBase => this.ChainDataLoadProvider;
		public IChainDataWriteProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> ChainDataWriteProviderBase => this.ChainDataWriteProvider;

		public IAccreditationCertificateProvider AccreditationCertificateProviderBase => this.AccreditationCertificateProvider;

		public IAccountSnapshotsProvider AccountSnapshotsProviderBase => this.AccountSnapshotsProvider;

		public IInterpretationProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> InterpretationProviderBase => this.InterpretationProvider;

		public ASSEMBLY_PROVIDER AssemblyProvider { get; }

		public MAIN_FACTORY_PROVIDER ChainFactoryProvider { get; }

		public CHAIN_STATE_PROVIDER ChainStateProvider { get; }

		public CHAIN_VALIDATION_PROVIDER ChainValidationProvider { get; }
		public CHAIN_MINING_PROVIDER ChainMiningProvider { get; }

		public CHAIN_CONFIGURATION_PROVIDER ChainConfigurationProvider { get; }

		public CHAIN_LOADING_PROVIDER ChainDataLoadProvider { get; }
		public CHAIN_WRITING_PROVIDER ChainDataWriteProvider => (CHAIN_WRITING_PROVIDER)this.ChainDataLoadProvider;

		public ACCREDITATION_CERTIFICATE_PROVIDER AccreditationCertificateProvider { get; }

		public ACCOUNT_SNAPSHOTS_PROVIDER AccountSnapshotsProvider { get; }

		public CHAIN_NETWORKING_PROVIDER ChainNetworkingProvider { get; }

		public INTERPRETATION_PROVIDER InterpretationProvider { get; }

		public abstract ICardUtils CardUtils { get; }
	}
}