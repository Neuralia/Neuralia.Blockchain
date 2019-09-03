using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Connections {

	/// <summary>
	///     class to hold the settingsBase that each chain we have supports
	/// </summary>
	public sealed class ChainSettings : IBinarySerializable, ITreeHashable {

		public AppSettingsBase.BlockSavingModes BlockSavingMode { get; set; }

		public void Rehydrate(IDataRehydrator rehydrator) {
			this.BlockSavingMode = (AppSettingsBase.BlockSavingModes) rehydrator.ReadByte();
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write((byte) this.BlockSavingMode);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodesList = new HashNodeList();

			nodesList.Add((byte) this.BlockSavingMode);

			return nodesList;
		}
	}
}