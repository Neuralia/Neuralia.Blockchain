using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization.General.V1.Structures {
	public interface ITransactionAccountFeature : ISerializableCombo, IAccountFeature {
		TransactionAccountFeature.Actions Action { get; set; }
	}

	public abstract class TransactionAccountFeature : ITransactionAccountFeature {

		public enum Actions : byte {
			Add = 1,
			Update = 2,
			Remove = 3
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.FeatureType = rehydrator.ReadByte();
			this.CertificateId = rehydrator.ReadNullableInt();
			this.Data = rehydrator.ReadNonNullableArray().ToExactByteArray();
			this.Action = (Actions) rehydrator.ReadByte();

			this.Options = rehydrator.ReadNullableInt();
			this.Start = rehydrator.ReadNullableDateTime();
			this.End = rehydrator.ReadNullableDateTime();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.FeatureType);
			dehydrator.Write(this.CertificateId);
			dehydrator.WriteNonNullable(this.Data);
			dehydrator.Write((byte) this.Action);

			dehydrator.Write(this.Options);
			dehydrator.Write(this.Start);
			dehydrator.Write(this.End);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.FeatureType);
			hashNodeList.Add(this.CertificateId);
			hashNodeList.Add(this.Data);
			hashNodeList.Add(this.Action);

			hashNodeList.Add(this.Options);
			hashNodeList.Add(this.Start);
			hashNodeList.Add(this.End);

			return hashNodeList;
		}

		public void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("FeatureType", this.FeatureType);
			jsonDeserializer.SetProperty("CertificateId", this.CertificateId);
			jsonDeserializer.SetProperty("Data", this.Data);
			jsonDeserializer.SetProperty("Action", this.Action);

			jsonDeserializer.SetProperty("Options", this.Options);
			jsonDeserializer.SetProperty("Start", this.Start);
			jsonDeserializer.SetProperty("End", this.End);
		}

		public Actions Action { get; set; }
		public ushort FeatureType { get; set; }
		public int? CertificateId { get; set; }
		public int? Options { get; set; }
		public byte[] Data { get; set; }
		public DateTime? Start { get; set; }
		public DateTime? End { get; set; }
	}
}