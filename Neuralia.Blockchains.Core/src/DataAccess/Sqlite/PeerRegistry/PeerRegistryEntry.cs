using System.ComponentModel.DataAnnotations;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.PeerRegistry {
	public class PeerRegistryEntry {

		[Key]
		public string Ip { get; set; }
	}
}