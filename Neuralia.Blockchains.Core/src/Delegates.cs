using System;
using System.Security;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core {
	public class Delegates : DelegatesBase {

		public delegate Task<object> ChainEventDelegate(CorrelationContext correlationContext, BlockchainSystemEventType eventType, BlockchainType chainType, params object[] parameters);

		/// <summary>
		///     a Contravariant action delegate. allows for casting of sub types
		/// </summary>
		/// <param name="sender"></param>
		/// <returns></returns>

		//https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/covariance-contravariance/variance-in-delegates
		//https://msdn.microsoft.com/en-us/library/dd997386(VS.100).aspx
		public delegate void CovariantAction<in T>(T sender);

		public delegate void RehydrationDelegate<T>(IDataRehydrator rehydrator, ref T entry)
			where T : IBinaryRehydratable;

		public delegate void RequestCopyKeyFileDelegate(CorrelationContext correlationContext, Guid accountUUid, string keyName, int attempt);

		public delegate void RequestCopyWalletFileDelegate(CorrelationContext correlationContext, int attempt);

		public delegate SecureString RequestKeyPassphraseDelegate(CorrelationContext correlationContext, Guid accountUUid, string keyName, int attempt);

		public delegate SecureString RequestPassphraseDelegate(CorrelationContext correlationContext, int attempt);
	}
}