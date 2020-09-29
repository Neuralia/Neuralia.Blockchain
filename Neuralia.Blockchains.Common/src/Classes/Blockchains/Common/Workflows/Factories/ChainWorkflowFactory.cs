using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AppointmentRegistry;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Elections;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.P2p.Messages.Base;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Factories {

	public interface IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IWorkflowFactory
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		ICreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreatePresentationTransactionChainWorkflow(CorrelationContext correlationContext, string accountCode, byte expiration = 0);
		ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChangeKeyTransactionWorkflow(byte changingKeyOrdinal, string note, CorrelationContext correlationContext, byte expiration = 0);

		ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendElectionsCandidateRegistrationMessageWorkflow(AccountId candidateAccountId, Enums.MiningTiers miningTier, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, AppSettingsBase.ContactMethods registrationMethod, CorrelationContext correlationContext);

		ILoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateLoadWalletWorkflow(CorrelationContext correlationContext, string passphrase = null);
		
		ISendAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendAppointmentRequestMessageWorkflow(int preferredRegion, CorrelationContext correlationContext);
		ISendInitiationAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendInitiationAppointmentRequestMessageWorkflow(int preferredRegion, CorrelationContext correlationContext);
		IPuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateAppointmentPuzzleExecutionWorkflow(CorrelationContext correlationContext);
		ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendAppointmentVerificationResultsMessageWorkflow(List<IAppointmentRequesterResult> entries, Dictionary<long, bool> verificationResults, CorrelationContext correlationContext);

	}

	public abstract class ChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : WorkflowFactory<IBlockchainEventsRehydrationFactory>, IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
		protected readonly CENTRAL_COORDINATOR centralCoordinator;
		private IChainWorkflowFactory<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> chainWorkflowFactoryImplementation;

		public ChainWorkflowFactory(CENTRAL_COORDINATOR centralCoordinator) : base(centralCoordinator.BlockchainServiceSet) {
			this.centralCoordinator = centralCoordinator;
		}

		public abstract ICreatePresentationTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreatePresentationTransactionChainWorkflow(CorrelationContext correlationContext, string accountCode, byte expiration = 0);

		public abstract ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateChangeKeyTransactionWorkflow(byte changingKeyOrdinal, string note, CorrelationContext correlationContext, byte expiration = 0);

		// message workflows
		public abstract ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendElectionsCandidateRegistrationMessageWorkflow(AccountId candidateAccountId, Enums.MiningTiers miningTier, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, AppSettingsBase.ContactMethods registrationMethod, CorrelationContext correlationContext);
		public abstract ILoadWalletWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateLoadWalletWorkflow(CorrelationContext correlationContext, string passphrase = null);
		public abstract ISendAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendAppointmentRequestMessageWorkflow(int preferredRegion, CorrelationContext correlationContext);
		public abstract ISendInitiationAppointmentRequestMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendInitiationAppointmentRequestMessageWorkflow(int preferredRegion, CorrelationContext correlationContext);
		public abstract IPuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateAppointmentPuzzleExecutionWorkflow(CorrelationContext correlationContext);
		public abstract ISendAppointmentVerificationResultsMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> CreateSendAppointmentVerificationResultsMessageWorkflow(List<IAppointmentRequesterResult> entries, Dictionary<long, bool> verificationResults, CorrelationContext correlationContext);
	}
}