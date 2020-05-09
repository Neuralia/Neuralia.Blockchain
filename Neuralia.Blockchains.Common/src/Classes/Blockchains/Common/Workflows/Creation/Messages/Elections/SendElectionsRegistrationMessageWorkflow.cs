using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Messages.Elections {
	public interface ISendElectionsRegistrationMessageWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IGenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	/// <summary>
	///     Prepare and dispatch a miner registration message on the blockchain as a gossip message
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class SendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER> : GenerateNewMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, ASSEMBLY_PROVIDER>, ISendElectionsRegistrationMessageWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where ASSEMBLY_PROVIDER : IAssemblyProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		protected readonly AccountId candidateAccountID;

		protected readonly ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo;
		protected readonly Enums.MiningTiers miningTier;
		protected readonly AppSettingsBase.ContactMethods registrationMethod;

		public SendElectionsRegistrationMessageWorkflow(AccountId candidateAccountID, Enums.MiningTiers miningTier, ElectionsCandidateRegistrationInfo electionsCandidateRegistrationInfo, AppSettingsBase.ContactMethods registrationMethod, CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext) : base(centralCoordinator, correlationContext) {
			this.electionsCandidateRegistrationInfo = electionsCandidateRegistrationInfo;
			this.registrationMethod = registrationMethod;
			this.candidateAccountID = candidateAccountID;
			this.miningTier = miningTier;
		}

		protected override Task<IMessageEnvelope> AssembleEvent(LockContext lockContext) {
			if(this.registrationMethod == AppSettingsBase.ContactMethods.Gossip) {

				return this.centralCoordinator.ChainComponentProvider.AssemblyProviderBase.GenerateOnChainElectionsRegistrationMessage(this.candidateAccountID, this.miningTier, this.electionsCandidateRegistrationInfo, lockContext);
			}

			throw new ApplicationException("Invalid message type");
		}
	}
}