using System;
using System.ComponentModel.DataAnnotations;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {

	public class MessagePeerSqlite : IMessagePeer {
		public enum CommunicationDirection : byte {
			Unknown = 0,
			Received = 1,
			Sent = 2
		}

		public MessageEntrySqlite Message { get; set; }
		public PeerSqlite Peer { get; set; }

		[Required]
		public DateTime Received { get; set; }

		/// <summary>
		///     how many times was the message returned to us after we were aware of it
		/// </summary>
		/// <returns></returns>
		[Required]
		public int Echos { get; set; }

		public long Hash { get; set; }

		public IMessageEntry MessageBase {
			get => this.Message;
			set => this.Message = (MessageEntrySqlite) value;
		}

		[Required]
		public CommunicationDirection Direction { get; set; } = CommunicationDirection.Unknown;

		public string PeerKey { get; set; }

		public IPeer PeerBase {
			get => this.Peer;
			set => this.Peer = (PeerSqlite) value;
		}
	}
}