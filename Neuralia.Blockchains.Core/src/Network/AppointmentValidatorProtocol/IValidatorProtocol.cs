using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public interface IValidatorProtocol {
		void Initialize(BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate = null, int timeout = 10000);
		Task<bool> HandleServerExchange(ValidatorConnectionSet connectionSet, ByteArray operationBytes, CancellationToken? ct = null);

		Task<(SafeArrayHandle validatorCode, bool hasConnected)> RequestCodeTranslation(DateTime appointment, int index, SafeArrayHandle validatorCode, IPAddress address, int? port = null);
		Task<(int secretCodeL2, bool hasConnected)> TriggerSession(DateTime appointment, int index, int code, IPAddress address, int? port = null);
		Task<(bool completed, bool hasConnected)> RecordPuzzleCompleted(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, IPAddress address, int? port = null);
		Task<(bool completed, bool hasConnected)> RecordTHSCompleted(DateTime appointment, int index, SafeArrayHandle thsResults, IPAddress address, int? port = null);
	}
	
	public class ValidatorConnectionSet {
		public SocketAsyncEventArgs SocketAsyncEventArgs { get; set; }
		public Socket Socket { get; set; }
		public TcpValidatorServer.ValidatorConnectionInstance Token { get; set; }
	}

}