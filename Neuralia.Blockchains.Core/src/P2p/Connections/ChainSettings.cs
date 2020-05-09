using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Types;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Connections {

	/// <summary>
	///     class to hold the settingsBase that each chain we have supports
	/// </summary>
	public sealed class ChainSettings : IBinarySerializable, ITreeHashable {

		public ChainSettings() {

		}

		public ChainSettings(Enums.ChainSharingTypes chainSharingTypes) {
			this.ShareType = chainSharingTypes;
		}

		public ChainSettings(NodeShareType shareType) {
			this.ShareType = shareType;
		}

		public NodeShareType ShareType { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.ShareType = rehydrator.ReadByte();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.ShareType);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodesList = new HashNodeList();

			nodesList.Add((byte) this.ShareType);

			return nodesList;
		}
	}
}