using System;
using Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry;

namespace Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry {
	public interface IMessagePeer {
		DateTime Received { get; set; }

		/// <summary>
		///     how many times was the message returned to us after we were aware of it
		/// </summary>
		/// <returns></returns>
		int Echos { get; set; }

		long Hash { get; set; }
		IMessageEntry MessageBase { get; set; }
		MessagePeerSqlite.CommunicationDirection Direction { get; set; }
		string PeerKey { get; set; }
		IPeer PeerBase { get; set; }
	}
}