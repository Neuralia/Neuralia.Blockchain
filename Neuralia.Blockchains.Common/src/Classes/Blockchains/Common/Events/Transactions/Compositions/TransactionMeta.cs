using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Compositions {
	public class TransactionMeta : ISerializableCombo {
		public IdKeyUseIndexSet KeyUseIndex { get; set; }
		public IdKeyUseIndexSet KeyUseLock { get; set; }
		
		public Dictionary<AccountId, (IdKeyUseIndexSet keyUseIndex, IdKeyUseIndexSet keyIndexLock)> MultiSigKeyUseIndices { get; } = new Dictionary<AccountId, (IdKeyUseIndexSet keyUseIndex, IdKeyUseIndexSet keyIndexLock)>();

		public void Rehydrate(IDataRehydrator rehydrator) {
			bool any = rehydrator.ReadBool();

			if(any) {
				this.KeyUseIndex = new IdKeyUseIndexSet();
				this.KeyUseIndex.Rehydrate(rehydrator);
			}
			
			any = rehydrator.ReadBool();

			if(any) {
				this.KeyUseLock = new IdKeyUseIndexSet();
				this.KeyUseLock.Rehydrate(rehydrator);
			}

			this.MultiSigKeyUseIndices.Clear();
			any = rehydrator.ReadBool();

			if(any) {
				AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId> rparameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

				rparameters.RehydrateExtraData = (entry, offset, index, totalIndex, rh) => {

					IdKeyUseIndexSet keyUseIndex = new IdKeyUseIndexSet();
					keyUseIndex.Rehydrate(rh);
					IdKeyUseIndexSet keyUseLock = new IdKeyUseIndexSet();
					keyUseLock.Rehydrate(rh);
					this.MultiSigKeyUseIndices.Add(entry, (keyUseIndex, keyUseLock));
				};

				List<AccountId> results = AccountIdGroupSerializer.Rehydrate(rehydrator, true, rparameters);
			}

		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			bool any = this.KeyUseIndex != null;

			dehydrator.Write(any);

			if(any) {
				this.KeyUseIndex.Dehydrate(dehydrator);
			}
			
			any = this.KeyUseLock != null;

			dehydrator.Write(any);

			if(any) {
				this.KeyUseLock.Dehydrate(dehydrator);
			}

			any = this.MultiSigKeyUseIndices.Any();
			dehydrator.Write(any);

			if(any) {
				AccountIdGroupSerializer.AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId> dparameters = new AccountIdGroupSerializer.AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId>();

				dparameters.DehydrateExtraData = (entry, accountId, offset, index, totalIndex, dh) => {

					this.MultiSigKeyUseIndices[accountId].keyUseIndex.Dehydrate(dehydrator);
					this.MultiSigKeyUseIndices[accountId].keyIndexLock.Dehydrate(dehydrator);
				};

				AccountIdGroupSerializer.Dehydrate(this.MultiSigKeyUseIndices.Keys.ToList(), dehydrator, true, dparameters);
			}
		}

		public HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();
			
			nodeList.Add(this.KeyUseIndex);
			nodeList.Add(this.KeyUseLock);

			return nodeList;
		}
		
		public HashNodeList GetStructuresArrayMultiSig(AccountId accountId) {
			HashNodeList nodeList = this.GetStructuresArray();

			if(this.MultiSigKeyUseIndices.ContainsKey(accountId)) {
				nodeList.Add(this.MultiSigKeyUseIndices[accountId].keyUseIndex);
				nodeList.Add(this.MultiSigKeyUseIndices[accountId].keyIndexLock);
			}

			return nodeList;
		}
		
		public HashNodeList GetStructuresArrayMultiSig() {
			HashNodeList nodeList = this.GetStructuresArray();

			nodeList.Add(this.MultiSigKeyUseIndices.Count);

			foreach((AccountId key, (KeyUseIndexSet keyUseIndex, KeyUseIndexSet keyIndexLock)) in this.MultiSigKeyUseIndices) {
				nodeList.Add(key);
				nodeList.Add(keyUseIndex);
				nodeList.Add(keyIndexLock);
			}

			return nodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			
			jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex);
			jsonDeserializer.SetProperty("KeyUseLock", this.KeyUseLock);

			if(MultiSigKeyUseIndices.Any()) {
				jsonDeserializer.SetArray("MultiSigKeyUseIndices", this.MultiSigKeyUseIndices.OrderBy(k => k.Key), (deserializer, serializable) => {

					deserializer.WriteObject(s => {
						s.SetProperty("AccountId", serializable.Key);
						
						jsonDeserializer.SetProperty("KeyUseIndex", this.KeyUseIndex);
						jsonDeserializer.SetProperty("KeyUseLock", this.KeyUseLock);
					});
				});
			}
		}
	}
}