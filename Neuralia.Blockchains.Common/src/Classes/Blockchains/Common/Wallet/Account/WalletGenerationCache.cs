using System;
using LiteDB;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account {

	public interface IWalletGenerationCache {
		[BsonId]
		string Key { get; set; }

		DateTime Timestamp { get; set; }
		SafeArrayHandle Event { get;set; }
		string Version { get; set; }
		WalletGenerationCache.DispatchEventTypes EventType { get; set; }
		string EventSubType { get; set; }
		string Step { get; set; }
		WalletGenerationCache.StepStatuses StepStatus { get; set; }
		string SubStep { get; set; }
		long GossipMessageHash { get; set; }
		DateTime? NextRetry { get; set; }
		DateTime Expiration { get; set; }
		int CorrelationId { get; set; }
		bool Signed { get; set; }
		bool Dispatched { get; set; }
	}

	/// <summary>
	///     Here we save transactions in full detail to maintain useful state
	/// </summary>
	public abstract class WalletGenerationCache : IWalletGenerationCache {

		public enum DispatchEventTypes: byte {
			Unknown = 0,
			Transaction = 1,
			Message = 2
		}

		public enum StepStatuses {
			New,
			Completed
		}
		
		[BsonId]
		public string Key { get; set; }

		public DateTime Timestamp { get; set; }
		public SafeArrayHandle Event { get; set;}
		public string Version { get; set; }
		public DispatchEventTypes EventType { get; set; } = DispatchEventTypes.Unknown;
		public string EventSubType { get; set; }
		public string Step { get; set; }
		public StepStatuses StepStatus { get; set; }
		public string SubStep { get; set; }
		public long GossipMessageHash { get; set; }
		/// <summary>
		/// when is the minimum time to retry sending
		/// </summary>
		public DateTime? NextRetry { get; set; }
		public DateTime Expiration { get; set; }
		public int CorrelationId { get; set; }
		public bool Signed { get; set; }
		public bool Dispatched { get; set; }
	}
}