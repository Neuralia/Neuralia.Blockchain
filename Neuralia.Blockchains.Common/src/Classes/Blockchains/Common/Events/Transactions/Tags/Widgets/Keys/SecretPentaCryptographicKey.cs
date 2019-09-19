using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys {

	public interface ISecretPentaCryptographicKey : ISecretDoubleCryptographicKey {

		QTeslaCryptographicKey ThirdKey { get; set; }
		QTeslaCryptographicKey FourthKey { get; set; }
		XmssmtCryptographicKey FifthKey { get; set; }
	}

	public class SecretPentaCryptographicKey : SecretDoubleCryptographicKey, ISecretPentaCryptographicKey {

		public QTeslaCryptographicKey ThirdKey { get; set; } = new QTeslaCryptographicKey();
		public QTeslaCryptographicKey FourthKey { get; set; } = new QTeslaCryptographicKey();
		public XmssmtCryptographicKey FifthKey { get; set; } = new XmssmtCryptographicKey();

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.ThirdKey.Dehydrate(dehydrator);
			this.FourthKey.Dehydrate(dehydrator);
			this.FifthKey.Dehydrate(dehydrator);
		}

		public override void Rehydrate(byte id, IDataRehydrator rehydrator) {
			base.Rehydrate(id, rehydrator);

			this.ThirdKey.Rehydrate(rehydrator);
			this.FourthKey.Rehydrate(rehydrator);
			this.FifthKey.Rehydrate(rehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.ThirdKey);
			nodeList.Add(this.FourthKey);
			nodeList.Add(this.FifthKey);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("ThirdKey", this.ThirdKey);
			jsonDeserializer.SetProperty("FourthKey", this.FourthKey);
			jsonDeserializer.SetProperty("FifthKey", this.FifthKey);
		}

		protected override void SetType() {
			this.Type = Enums.KeyTypes.SecretPenta;
		}
		
		protected override void DisposeAll(bool disposing) {
			base.DisposeAll(disposing);
			
			this.ThirdKey?.Dispose();
			this.FourthKey?.Dispose();
			this.FifthKey?.Dispose();
		}
	}
}