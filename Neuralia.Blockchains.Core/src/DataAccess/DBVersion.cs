using System;

namespace Neuralia.Blockchains.Core.DataAccess {
	public class DBVersion {
		
		public int Id { get; set; } = 0;
		public int Major { get; set; } = 0;
		public int Minor { get; set; } = 0;
		public int Revision { get; set; } = 0;
		
		public DateTime LastUpdate { get; set; }
	}
}