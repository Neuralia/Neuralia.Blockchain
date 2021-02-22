using System.Collections.Generic;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types {
	public class WalletKeyIndexCache : IBinarySerializable {

		public class WalletKeyIndexCacheEntry : IBinarySerializable{
			public IdKeyUseIndexSet KeyIndex { get; set; } = new IdKeyUseIndexSet();
			public TransactionTimestamp Timestamp { get; set; } = new TransactionTimestamp();

			public void Rehydrate(IDataRehydrator rehydrator) {
				this.KeyIndex.Rehydrate(rehydrator);
				this.Timestamp.Rehydrate(rehydrator);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				this.KeyIndex.Dehydrate(dehydrator);
				this.Timestamp.Dehydrate(dehydrator);
			}
		}

		public Dictionary<byte, WalletKeyIndexCacheEntry> keys = new Dictionary<byte, WalletKeyIndexCacheEntry>();

		public void Rehydrate(IDataRehydrator rehydrator) {
			keys.Clear();

			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Rehydrate(rehydrator);

			int count = tool.Value;

			for(int i = 0; i < count; i++) {
				WalletKeyIndexCacheEntry entry = new WalletKeyIndexCacheEntry();
				
				entry.Rehydrate(rehydrator);
				
				keys.Add(entry.KeyIndex.Ordinal, entry);
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Value = this.keys.Count;
			tool.Dehydrate(dehydrator);

			foreach(var key in keys.Values) {
				key.Dehydrate(dehydrator);
			}
		}
	}
}