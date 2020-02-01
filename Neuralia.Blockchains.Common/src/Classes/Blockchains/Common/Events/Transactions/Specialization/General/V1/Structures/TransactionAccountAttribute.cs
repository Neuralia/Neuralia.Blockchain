using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures {
	public interface ITransactionAccountAttribute : ISerializableCombo, IAccountAttribute {
	}

	public abstract class TransactionAccountAttribute : AccountAttribute, ITransactionAccountAttribute {

		public enum Actions : byte {
			Add = 1,
			Update = 2,
			Remove = 3
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			AdaptiveLong1_9 certificate = new AdaptiveLong1_9();
			certificate.Rehydrate(rehydrator);
			this.CorrelationId = (uint)certificate.Value;
					
			certificate = new AdaptiveLong1_9();
			certificate.Rehydrate(rehydrator);
			this.AttributeType = (AccountAttributeType)certificate.Value;

			var array = rehydrator.ReadArray();

			this.Context = null;
			
			if(array?.HasData??false) {
				this.Context = array.ToExactByteArrayCopy();
			}

			this.Start = rehydrator.ReadNullableDateTime();
			this.Expiration = rehydrator.ReadNullableDateTime();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			
			
			AdaptiveLong1_9 certificate = new AdaptiveLong1_9();
			certificate.Value =	this.CorrelationId;
			certificate.Dehydrate(dehydrator);
					
			certificate.Value =	this.AttributeType.Value;
			certificate.Dehydrate(dehydrator);
			
			dehydrator.Write(this.Context);

			dehydrator.Write(this.Start);
			dehydrator.Write(this.Expiration);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.CorrelationId);
			hashNodeList.Add(this.AttributeType);
			hashNodeList.Add(this.Context);

			hashNodeList.Add(this.Start);
			hashNodeList.Add(this.Expiration);

			return hashNodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("FeatureType", this.AttributeType);
			jsonDeserializer.SetProperty("CorrelationId", this.CorrelationId);
			jsonDeserializer.SetProperty("Context", this.Context);
			
			jsonDeserializer.SetProperty("Start", this.Start);
			jsonDeserializer.SetProperty("Expiration", this.Expiration);
		}
	}
}