using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	[SuppressMessage("ReSharper", "All")]
	public class MessageEntrySqlite : IMessageEntry {
		//private set is used to fix the "missing parameter name: frameworkName" error in EF Core.
		public List<MessagePeerSqlite> Peers { get; private set; } = new List<MessagePeerSqlite>();
		//this method is to make sure ReSharper does not remove the private set of Peers.
		public void SetPeers(List<MessagePeerSqlite> peers) => this.Peers = peers;

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