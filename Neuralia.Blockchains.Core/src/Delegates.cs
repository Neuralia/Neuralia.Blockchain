using System.Security;
using System.Threading.Tasks;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core {
	public class Delegates : DelegatesBase {

		public delegate Task ChainEventDelegate(CorrelationContext? correlationContext, BlockchainSystemEventType eventType, BlockchainType chainType, params object[] parameters);

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

		public delegate Task RequestCopyKeyFileDelegate(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext);

		public delegate Task RequestCopyWalletFileDelegate(CorrelationContext correlationContext, int attempt, LockContext lockContext);

		public delegate Task<SecureString> RequestKeyPassphraseDelegate(CorrelationContext correlationContext, string accountCode, string keyName, int attempt, LockContext lockContext);

		public delegate Task<(SecureString passphrase, bool keysToo)> RequestPassphraseDelegate(CorrelationContext correlationContext, int attempt, LockContext lockContext);
	}
}