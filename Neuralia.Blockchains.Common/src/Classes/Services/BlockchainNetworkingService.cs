using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
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

		public BlockchainNetworkingService(IBlockchainGuidService guidService, IHttpService httpService, IFileFetchService fileFetchService, IDataAccessService dataAccessService, IBlockchainInstantiationService instantiationService, IGlobalsService globalsService, IBlockchainTimeService timeService) : base(guidService, httpService, fileFetchService, dataAccessService, instantiationService, globalsService, timeService) {
		}

		public Dictionary<BlockchainType, MiningRegistrationParameters> ChainMiningRegistrationParameters => this.chainMiningRegistrationParameters;

		public new BlockchainServiceSet ServiceSet => (BlockchainServiceSet) base.ServiceSet;

		/// <summary>
		///     This is a very special use case where an IP Validator is contacting us. We need to respond as quickly a possible,
		///     so its all done here in top priority
		/// </summary>
		/// <param name="buffer"></param>
		protected override void HandleIpValidatorRequest(IByteArray buffer, ITcpConnection connection) {

			try {
				(byte version, IValidatorRequest request, IMinerResponse response) messages = IpValidationFactory.RehydrateRequest(buffer);

				try {
					if(!this.chainMiningRegistrationParameters.ContainsKey(messages.request.Chain)) {
						throw new ApplicationException("We received a validation request for a chain we do not have as mining.");

						//TODO: log this, if it happens to often, block the IP. the validator will never abuse this.
					}

					MiningRegistrationParameters parameters = this.chainMiningRegistrationParameters[messages.request.Chain];

					// validate the request
					if(!messages.request.Password.Equals(parameters.Password)) {
						throw new ApplicationException("We received a validation request we an invalid secret.");

						//TODO: log this, if it happens to often, block the IP. the validator will never abuse this.
					}

					// ok, seems this is the right secret, lets confirm our miner status
					messages.response.AccountId = parameters.AccountId;
					messages.response.Response = ResponseType.Valid;

					if(connection.State != ConnectionState.Connected) {
						throw new ApplicationException("Not connected to ip validator for response");
					}
				} catch(Exception e) {
					// lets trye to respond at the very least
					messages.response.Response = ResponseType.Invalid;
				}

				connection.SendBytes(messages.response.Dehydrate());
			} catch(Exception e) {
				Log.Error(e, "Failed to respond to IP validation request");

				throw;
			} finally {
				// we always finish here
				connection.Close();
			}
		}

		protected override ServiceSet<IBlockchainEventsRehydrationFactory> CreateServiceSet() {
			return new BlockchainServiceSet(BlockchainTypes.Instance.None);
		}

		/// <summary>
		///     a special class to hold our published mining registration paramters so we can answer the IP Validators
		/// </summary>
		public class MiningRegistrationParameters {
			public long Password { get; set; }
			public AccountId AccountId { get; set; }
			public AccountId DelegateAccountId { get; set; }
		}
	}
}