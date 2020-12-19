using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Autographs {
	public class TripleXmssAutograph : AutographBase {

		public SafeArrayHandle FirstAutograph { get; set; }
		public SafeArrayHandle SecondAutograph { get; set; }
		public SafeArrayHandle ThirdAutograph { get; set; }

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			this.FirstAutograph = (SafeArrayHandle) rehydrator.ReadArray();
			this.SecondAutograph = (SafeArrayHandle) rehydrator.ReadArray();
			this.ThirdAutograph = (SafeArrayHandle) rehydrator.ReadArray();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.FirstAutograph);
			dehydrator.Write(this.SecondAutograph);
			dehydrator.Write(this.ThirdAutograph);
		}
	}
}