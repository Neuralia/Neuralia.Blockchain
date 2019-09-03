using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	public class MessageEntrySqlite : IMessageEntry {
		public List<MessagePeerSqlite> Peers { get; } = new List<MessagePeerSqlite>();

		[Key]
		public long Hash { get; set; }

		[Required]
		public DateTime Received { get; set; }

		[Required]
		public bool Valid { get; set; } = false;

		/// <summary>
		///     is it our own message?
		/// </summary>
		[Required]
		public bool Local { get; set; } = false;

		/// <summary>
		///     how many times was the message returned to us after we were aware of it
		/// </summary>
		/// <returns></returns>
		[Required]
		public int Echos { get; set; }

		public IEnumerable<IMessagePeer> PeersBase => this.Peers;
	}
}