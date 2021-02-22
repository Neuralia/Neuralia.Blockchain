using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.IpValidation.V1;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
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
				using var rehydrator = DataSerializationFactory.CreateRehydrator(buffer);
				var request = IpValidationFactory.RehydrateRequest(rehydrator);

				IMinerResponse response = null;
				try {
					if(!this.chainMiningRegistrationParameters.ContainsKey(request.Chain)) {
						throw new ApplicationException("We received a validation request for a chain we do not have as mining.");

						//TODO: log this, if it happens to often, block the IP. the validator will never abuse this.
					}

					parameters = this.chainMiningRegistrationParameters[request.Chain];

					using SafeArrayHandle passwordBytes = SafeArrayHandle.Create(16);
					TypeSerializer.Serialize(parameters.Password, passwordBytes.Span);

					using SafeArrayHandle saltBytes = SafeArrayHandle.Create(16);
					TypeSerializer.Serialize(parameters.SecretCode, saltBytes.Span);

					TypeSerializer.Deserialize(saltBytes.Span.Slice(0, sizeof(int)), out int iterations);

					(SafeArrayHandle nonce, SafeArrayHandle key) = CryptoUtil.GenerateKeyNonceSet(passwordBytes, saltBytes, CryptoUtil.GetIterations(iterations & 0xFF, 2000, 5000));
					XchachaEncryptor xchachaEncryptor = new XchachaEncryptor();

					var resultBytes = xchachaEncryptor.Decrypt(request.Secret, nonce, key);
					
					using SafeArrayHandle hashingBytes = SafeArrayHandle.Create(nonce.Length + resultBytes.Length);

					nonce.CopyTo(hashingBytes);
					resultBytes.CopyTo(hashingBytes, 0, nonce.Length, resultBytes.Length);

					if(request.Version != (1, 0)) {
						NLog.Default.Warning($"A validator sent us a verification protocol version of {request.Version} which we do not support!");
						
						throw new ApplicationException("Unsupported validator version");
					}
					response = new MinerResponse();
					// ok, seems this is the right secret, lets confirm our miner status
					response.ResponseCode = HashingUtils.XxHash32(hashingBytes);
					response.AccountId = parameters.AccountId;
					response.Response = ResponseType.Valid;

					foreach(var operation in response.Operations) {
						// none for now
						
						// try {
						// 	var answers = parameters.ChainMiningStatusProvider.AnswerQuestions(messages.request.SecondTierQuestion, messages.request.DigestQuestion, messages.request.FirstTierQuestion, parameters.ChainMiningStatusProvider.MiningTier);
						//
						// 	messages.response.SecondTierAnswer = answers.secondTierAnswer;
						// 	messages.response.DigestTierAnswer = answers.digestAnswer;
						// 	messages.response.FirstTierAnswer = answers.firstTierAnswer;
						// } catch(Exception ex) {
						// 	NLog.Default.Error(ex, "Failed to answer questions to validator request. this could be bad...");
						// }
						
						//
						// // ok, this is where we start the validation server so they can verify our validation port.
						// try {
						// 	this.EnableVerificationWindow();
						// } catch(Exception ex) {
						// 	NLog.Default.Error(ex, "Failed to start validation verification server...");
						//
						// 	throw;
						// }
					}
					
					if(connection.State != ConnectionState.Connected) {
						throw new ApplicationException("Not connected to ip validator for response");
					}
				} catch(Exception e) {
					// lets try to respond at the very least
					if(response != null) {
						response.Response = ResponseType.Invalid;
					}
				}

				if(response == null) {
					throw new ApplicationException("invalid response");
				}
				
				connection.SendBytes(response.Dehydrate());

				if(response.Response == ResponseType.Invalid) {
					throw new ApplicationException();
				}

			} catch(Exception e) {
				NLog.Default.Error(e, "Failed to respond to IP validation request");

				this.BanOther(connection, $"failed to authenticate verification service in {nameof(this.HandleIpValidatorRequest)}.");

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
			} catch(Exception e) {
				NLog.Default.Error(e, "Failed to update account mining cache expiration. this is not very important, the response seems to have worked.");
			}
		}

		private void BanOther(ITcpConnection connection, string details, IPMarshall.QuarantineReason reason = IPMarshall.QuarantineReason.ValidationFailed) {
			var endpoint = (IPEndPoint) connection.RemoteEndPoint;
			IPMarshall.ValidationInstance.Quarantine(endpoint.Address, reason, DateTimeEx.CurrentTime.AddDays(2).Subtract(TimeSpan.FromMinutes(5)), details);
		}

		protected override ServiceSet<IBlockchainEventsRehydrationFactory> CreateServiceSet() {
			return new BlockchainServiceSet(BlockchainTypes.Instance.None);
		}

		/// <summary>
		///     a special class to hold our published mining registration paramters so we can answer the IP Validators
		/// </summary>
		public class MiningRegistrationParameters {
			public Guid Password { get; set; }
			public Guid SecretCode { get; set; }
			public AccountId AccountId { get; set; }
			public AccountId DelegateAccountId { get; set; }
			public SafeArrayHandle Autograph { get; } = SafeArrayHandle.Create();
			public IChainMiningStatusProvider ChainMiningStatusProvider { get; set; }
		}
	}
}