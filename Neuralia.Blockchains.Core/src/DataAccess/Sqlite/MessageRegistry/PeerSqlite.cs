using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	public class PeerSqlite : IPeer {
		public List<MessagePeerSqlite> Messages { get; } = new List<MessagePeerSqlite>();

		[Key]
		public string PeerKey { get; set; }

		public IEnumerable<IMessagePeer> MessagesBase => this.Messages;
	}
}