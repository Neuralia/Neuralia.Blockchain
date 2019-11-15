using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Connections {
	public class GeneralSettings : IBinarySerializable, ITreeHashable {

		public bool GossipEnabled { get; set; } = true;

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.GossipEnabled = rehydrator.ReadBool();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write( this.GossipEnabled);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodesList = new HashNodeList();

			nodesList.Add(this.GossipEnabled);

			return nodesList;
		}
		
	}
}