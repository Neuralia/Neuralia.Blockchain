using Neuralia.Blockchains.Core.Cryptography.THS.V1;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	public class THSState {
		public byte[] Hash { get; set; }
		public THSProcessState thsState  { get; set; } = new THSProcessState();
	}
}