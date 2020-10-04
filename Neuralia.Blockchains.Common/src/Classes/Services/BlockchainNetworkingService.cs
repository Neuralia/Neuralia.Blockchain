using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Services {

	public interface IBlockchainNetworkingService<R> : INetworkingService<R>
		where R : IRehydrationFactory {

		Dictionary<BlockchainType, BlockchainNetworkingService.MiningRegistrationParameters> ChainMiningRegistrationParameters { get; }

		new BlockchainServiceSet ServiceSet { get; }
	}

	public interface IBlockchainNetworkingService : IBlockchainNetworkingService<IBlockchainEventsRehydrationFactory> {
	}

	public class BlockchainNetworkingService : NetworkingService<IBlockchainEventsRehydrationFactory>, IBlockchainNetworkingService {

		protected readonly Dictionary<BlockchainType, MiningRegistrationParameters> chainMiningRegistrationParameters = new Dictionary<BlockchainType, MiningRegistrationParameters>();

		public BlockchainNetworkingService(IBlockchainGuidService guidService, IHttpService httpService, IFileFetchService fileFetchService, IDataAccessService dataAccessService, IBlockchainInstantiationService instantiationService, IGlobalsService globalsService, IBlockchainTimeService timeService, IPortMappingService portMappingService) : base(guidService, httpService, fileFetchService, dataAccessService, instantiationService, globalsService, timeService, portMappingService) {
		}

		public Dictionary<BlockchainType, MiningRegistrationParameters> ChainMiningRegistrationParameters => this.chainMiningRegistrationParameters;

		public new BlockchainServiceSet ServiceSet => (BlockchainServiceSet) base.ServiceSet;

		/// <summary>
		///     This is a very special use case where an IP Validator is contacting us. We need to respond as quickly a possible,
		///     so its all done here in top priority
		/// </summary>
		/// <param name="buffer"></param>
		protected override async Task HandleIpValidatorRequest(SafeArrayHandle buffer, ITcpConnection connection) {

			MiningRegistrationParameters parameters = null;
			try {
				(byte version, IValidatorRequest request, IMinerResponse response) messages = IpValidationFactory.RehydrateRequest(buffer);

				try {
					if(!this.chainMiningRegistrationParameters.ContainsKey(messages.request.Chain)) {
						throw new ApplicationException("We received a validation request for a chain we do not have as mining.");

						//TODO: log this, if it happens to often, block the IP. the validator will never abuse this.
					}

					parameters = this.chainMiningRegistrationParameters[messages.request.Chain];

					
					// validate the request
					if(!messages.request.Password.Equals(parameters.Password)) {
						throw new ApplicationException("We received a validation request we an invalid secret.");

						//TODO: log this, if it happens to often, block the IP. the validator will never abuse this.
					}

					// ok, this is where we start the validation server so they can verify our validation port. we can star this in parallel
					var task = Task.Run(() => EnableVerificationWindow());
					
					// ok, seems this is the right secret, lets confirm our miner status
					messages.response.AccountId = parameters.AccountId;
					messages.response.Response = ResponseType.Valid;

					try {
						var answers = parameters.ChainMiningStatusProvider.AnswerQuestions(messages.request.SecondTierQuestion, messages.request.DigestQuestion, messages.request.FirstTierQuestion, parameters.ChainMiningStatusProvider.MiningTier);

						messages.response.SecondTierAnswer = answers.secondTierAnswer;
						messages.response.DigestTierAnswer = answers.digestAnswer;
						messages.response.FirstTierAnswer  = answers.firstTierAnswer;
					} catch(Exception ex) {
						NLog.Default.Error(ex, "Failed to answer questions to validator request. this could be bad...");
					}

					if(connection.State != ConnectionState.Connected) {
						throw new ApplicationException("Not connected to ip validator for response");
					}
				} catch(Exception e) {
					// lets try to respond at the very least
					messages.response.Response = ResponseType.Invalid;
				}

				connection.SendBytes(messages.response.Dehydrate());
				
			} catch(Exception e) {
				NLog.Default.Error(e, "Failed to respond to IP validation request");

				throw;
			} finally {
				// we always finish here
				connection.Close();
			}

			try {
				if(parameters != null) {
					LockContext lockContext = null;
					await parameters.ChainMiningStatusProvider.UpdateAccountMiningCacheExpiration(lockContext).ConfigureAwait(false);
				}
			}
			catch(Exception e) {
				NLog.Default.Error(e, "Failed to update account mining cache expiration. this is not very important, the response seems to have worked.");
			}
		}

		protected override ServiceSet<IBlockchainEventsRehydrationFactory> CreateServiceSet() {
			return new BlockchainServiceSet(BlockchainTypes.Instance.None);
		}

		/// <summary>
		///     a special class to hold our published mining registration paramters so we can answer the IP Validators
		/// </summary>
		public class MiningRegistrationParameters {
			public long                       Password                  { get; set; }
			public AccountId                  AccountId                 { get; set; }
			public AccountId                  DelegateAccountId         { get; set; }
			public SafeArrayHandle            Autograph                 { get; } = new SafeArrayHandle();
			public IChainMiningStatusProvider ChainMiningStatusProvider { get; set; }
		}
	}
}