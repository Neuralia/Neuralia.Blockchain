using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools {

	public interface IConnectionSet {
	}

	public interface IConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IConnectionSet
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply {
	}

	public static class ConnectionSet {
		public class ConnectionStrikeset {

			public static readonly TimeSpan RejectionTimeout = TimeSpan.FromSeconds(20);
			public static readonly TimeSpan RejectionGracePeriod = TimeSpan.FromSeconds(1);

			private DateTime? lastRejection = null;

			public int Strikes { get; set; } = 0;
			public const int LIMIT = 3;

			public class RejectionSet {

				public readonly DateTime Timestamp = DateTime.UtcNow;
			}

			public enum RejectionReason {
				NoNextBlock,
				NoConnection,
				SendDataError,
				InvalidResponse,
				NoAnswer,
				CannotHelp,
				Banned
			}

			public DateTime lastCheck;

			public readonly PeerConnection PeerConnection;
			public readonly RejectionReason rejectionReason;

			public ConnectionStrikeset(PeerConnection peerConnectionn, RejectionReason rejectionReason) {
				this.PeerConnection = peerConnectionn;
				this.lastCheck = DateTime.UtcNow;
				this.rejectionReason = rejectionReason;

				this.AddRejection();
			}

			public bool AddRejection() {
				this.ReleaseObsoleteRejections();

				// we only add a new rejection if we are not inside the grace period. This is to avoid rejecting for a short moment of trouble
				if(this.lastRejection == null) {
					this.lastRejection = DateTime.UtcNow;
					this.rejections.Add(new RejectionSet());
				}

				return this.IsFinished;
			}

			public readonly List<RejectionSet> rejections = new List<RejectionSet>();

			public bool IsClear => !this.rejections.Any();
			public bool IsFinished => this.rejections.Count >= LIMIT;

			public void ReleaseObsoleteRejections() {

				// reset the grace period if we passed it
				if(this.lastRejection != null && this.lastRejection.Value + RejectionGracePeriod < DateTime.UtcNow) {
					this.lastRejection = null;
				}

				this.rejections.RemoveAll(r => r.Timestamp + RejectionTimeout < DateTime.UtcNow);
			}

			public override bool Equals(object obj) {
				if(obj is ConnectionStrikeset rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}

		public class BlockedConnection {

			public enum BanReason {
				Evil,
				CantHelp
			}

			public DateTime Timestamp { get; }

			public readonly PeerConnection PeerConnection;
			public readonly BanReason Reason;

			public BlockedConnection(PeerConnection peerConnectionn, BanReason reason) {
				this.PeerConnection = peerConnectionn;
				this.Timestamp = DateTime.UtcNow;
				this.Reason = reason;
			}

			public override bool Equals(object obj) {
				if(obj is BlockedConnection rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}
		
		public class SleepingConnection {

			public DateTime Timestamp { get; }
			
			public DateTime EndStamp { get; }

			public readonly PeerConnection PeerConnection;
			public readonly TimeSpan TimeSpan;

			public SleepingConnection(PeerConnection peerConnectionn, TimeSpan timeSpan) {
				this.PeerConnection = peerConnectionn;
				this.Timestamp = DateTime.UtcNow;
				this.TimeSpan = timeSpan;

				this.EndStamp = this.Timestamp + this.TimeSpan;
			}

			public bool IsSleeping => this.EndStamp > DateTime.UtcNow;
			
			public override bool Equals(object obj) {
				if(obj is SleepingConnection rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}
	}

	/// <summary>
	///     Various operations to handle our syncing connections
	/// </summary>
	/// TODO: we keep all connections and veil them as rejected or banned. it can be slow to rebuild the veil every request. maybe its better to copy accross various state arrays instead?
	/// <typeparam name="CHAIN_SYNC_TRIGGER"></typeparam>
	/// <typeparam name="SERVER_TRIGGER_REPLY"></typeparam>
	public class ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, IDisposableExtended
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply {

		//TODO: make this something like 5 or 10 minutes
		protected const int REJECTED_TIMEOUT = 60 * 1;

		public const int BAN_LIMIT = 3;

		/// <summary>
		/// connections that are temporarily sleeping (and thus excluded)
		/// </summary>
		protected readonly Dictionary<Guid, ConnectionSet.SleepingConnection> sleeping = new Dictionary<Guid, ConnectionSet.SleepingConnection>();
		
		
		/// <summary>
		/// connections that are removed from the sync set. may be permanent or temporary
		/// </summary>
		protected readonly Dictionary<Guid, ConnectionSet.BlockedConnection> banned = new Dictionary<Guid, ConnectionSet.BlockedConnection>();

		/// <summary>
		///     the strike state of various connections
		/// </summary>
		protected readonly Dictionary<Guid, ConnectionSet.ConnectionStrikeset> strikes = new Dictionary<Guid, ConnectionSet.ConnectionStrikeset>();

		/// <summary>
		/// all the connections that we hold
		/// </summary>
		protected readonly ConcurrentDictionary<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, int> connections = new ConcurrentDictionary<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, int>();

		/// <summary>
		/// a counter of banning events. more than the limit is permabanned
		/// </summary>
		protected readonly Dictionary<Guid, int> bannedStrikes = new Dictionary<Guid, int>();

		/// <summary>
		///     How much time in seconds do we exclude a rejected transactin before we give them another try.
		/// </summary>
		private readonly object locker = new object();

		public ConnectionSet(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> other) {
			this.Set(other);
		}

		public ConnectionSet() {

		}

		public bool HasActiveConnections => this.GetActiveConnections().Any();
		public bool HasSyncingConnections => this.GetSyncingConnections().Any();
		public int SyncingConnectionsCount => this.GetSyncingConnections().Count;

		/// <summary>
		///     a special method that allows us to triangulate the difference in the original with the current, to apply them to
		///     the other too before we merge them
		/// </summary>
		/// <param name="other"></param>
		/// <param name="original"></param>
		public void Merge(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> newConnections) {
			// ok, lets sync the two.  

			lock(this.locker) {
				var temp = new ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>(this);

				foreach(var entry in newConnections.strikes) {
					temp.AddConnectionStrike(entry.Value.PeerConnection, entry.Value.rejectionReason);
				}

				foreach(var entry in newConnections.banned) {

					temp.AddBannedConnection(entry.Value.PeerConnection, entry.Value.Reason);
				}

				foreach(var entry in newConnections.sleeping) {
					this.SleepActiveConnection(entry.Value.PeerConnection, entry.Value);
				}
				
				this.AddValidConnections(newConnections.connections);

				this.Set(temp);
			}
		}

		public void Set(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> other) {
			// ok, lets sync the two.  

			if(other == null) {
				return;
			}
			
			lock(this.locker) {
				this.connections.Clear();

				this.strikes.Clear();
				this.banned.Clear();
				this.sleeping.Clear();

				if(!Equals(other, this)) {
					foreach(var entry in other.connections.Keys.ToArray().Distinct()) {
						this.connections.AddSafe(entry, 1);
					}
				}

				foreach((var _, ConnectionSet.ConnectionStrikeset value) in other.strikes) {
					this.AddConnectionStrike(value.PeerConnection, value.rejectionReason);
				}

				foreach((var _, ConnectionSet.BlockedConnection value) in other.banned) {
					this.AddBannedConnection(value.PeerConnection, value.Reason);
				}
				
				foreach((var _, ConnectionSet.SleepingConnection value) in other.sleeping) {
					this.SleepActiveConnection(value.PeerConnection, value);
				}
			}
		}

		public void Set(List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> activeChainConnections) {
			// ok, lets sync the two.  

			lock(this.locker) {
				this.connections.Clear();

				this.strikes.Clear();
				this.banned.Clear();
				this.sleeping.Clear();

				foreach(var entry in activeChainConnections.ToArray()) {
					this.connections.AddSafe(entry, 1);
				}
			}
		}
		
		/// <summary>
		///     can be called by another thread
		/// </summary>
		/// <param name="connections"></param>
		public void AddValidConnections(List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> connections) {

			lock(this.locker) {
				foreach(var entry in connections) {

					if(this.connections.Keys.All(p => p.PeerConnection.ClientUuid != entry.PeerConnection.ClientUuid)) {
						this.connections.AddSafe(entry, 1);
					}
				}
			}
		}

		/// <summary>
		///     can be called by another thread
		/// </summary>
		/// <param name="connections"></param>
		public void AddValidConnections(ConcurrentDictionary<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>, int> connections) {

			lock(this.locker) {
				foreach(var entry in connections.Keys) {

					if(this.connections.Keys.All(p => p.PeerConnection.ClientUuid != entry.PeerConnection.ClientUuid)) {
						this.connections.AddSafe(entry, 1);
					}
				}
			}
		}

		public void AddConnectionStrike(PeerConnection peerConnectionn, ConnectionSet.ConnectionStrikeset.RejectionReason rejectionReason) {

			ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> entry = null;

			lock(this.locker) {
				entry = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == peerConnectionn.ClientUuid);

				if(this.banned.ContainsKey(peerConnectionn.ClientUuid)) {
					return;
				}

				if(rejectionReason == ConnectionSet.ConnectionStrikeset.RejectionReason.Banned) {
					// this is automatic banning
					this.AddBannedConnection(peerConnectionn, ConnectionSet.BlockedConnection.BanReason.Evil);

					return;
				}

				if(rejectionReason == ConnectionSet.ConnectionStrikeset.RejectionReason.NoNextBlock || rejectionReason == ConnectionSet.ConnectionStrikeset.RejectionReason.CannotHelp) {
					// these are special cases where the peer is not malevolent, but we will freeze them for a little while

					this.AddBannedConnection(peerConnectionn, ConnectionSet.BlockedConnection.BanReason.CantHelp);

					return;
				}

				if(this.strikes.ContainsKey(peerConnectionn.ClientUuid)) {
					if(this.strikes[peerConnectionn.ClientUuid].AddRejection()) {
						// thats it, this one is gone
						this.strikes.Remove(peerConnectionn.ClientUuid);

						this.AddBannedConnection(peerConnectionn, ConnectionSet.BlockedConnection.BanReason.Evil);
					}

					return;
				}

				this.strikes.Add(peerConnectionn.ClientUuid, new ConnectionSet.ConnectionStrikeset(peerConnectionn, rejectionReason));
			}
		}

		public void AddConnectionStrike(Guid clientUuid, ConnectionSet.ConnectionStrikeset.RejectionReason rejectionReason) {
			lock(this.locker) {
				this.AddConnectionStrike(this.connections.Keys.Single(c => c.PeerConnection.ClientUuid == clientUuid).PeerConnection, rejectionReason);
			}
		}

		private void AddBannedStrike(Guid uuid) {
			if(this.bannedStrikes.ContainsKey(uuid)) {
				this.bannedStrikes[uuid]++;
			} else {
				this.bannedStrikes.Add(uuid, 1);
			}
		}
		
		public void AddBannedConnection(Guid clientUuid, ConnectionSet.BlockedConnection.BanReason reason) {
			lock(this.locker) {
				this.AddBannedConnection(this.connections.Keys.Single(c => c.PeerConnection.ClientUuid == clientUuid).PeerConnection, reason);
			}
		}

		public void AddBannedConnection(PeerConnection peerConnectionn, ConnectionSet.BlockedConnection.BanReason reason) {
			// remove any rejected if we are banning them. they are out

			ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> entry = null;

			lock(this.locker) {
				this.AddBannedStrike(peerConnectionn.ClientUuid);

				if(this.strikes.ContainsKey(peerConnectionn.ClientUuid)) {
					this.strikes.Remove(peerConnectionn.ClientUuid);
				}

				if(!this.banned.ContainsKey(peerConnectionn.ClientUuid)) {
					entry = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == peerConnectionn.ClientUuid);

					if(entry != null) {
						entry.Syncing = false;
					}

					this.banned.Add(peerConnectionn.ClientUuid, new ConnectionSet.BlockedConnection(peerConnectionn, reason));
				}
			}
		}

		private void ClearSleeping() {
			foreach(var entry in this.sleeping.Where(s => !s.Value.IsSleeping)) {
				this.sleeping.Remove(entry.Key);
			}
		}
		public void SleepActiveConnection(PeerConnection peerConnectionn, TimeSpan timeSpan) {

			this.SleepActiveConnection(peerConnectionn, new ConnectionSet.SleepingConnection(peerConnectionn, timeSpan));
		}
		
		public void SleepActiveConnection(PeerConnection peerConnectionn, ConnectionSet.SleepingConnection sleepingConnection) {

			this.ClearSleeping();
			
			if(!this.sleeping.ContainsKey(peerConnectionn.ClientUuid)) {
				this.sleeping.Add(peerConnectionn.ClientUuid, sleepingConnection);
			}
			
			var entry = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == peerConnectionn.ClientUuid);

			if(entry != null) {
				entry.Syncing = false;

				// lets remove it
				this.ConnectionOnDisconnected(entry.PeerConnection.connection, null);
			}
		}

		public void FreeLowLevelBans() {
			lock(this.locker) {
				// first, we clear the banned transactions that are done their jail time and did not strike more than the acceptable limit
				foreach(var banned in this.banned.Where(c => c.Value.Reason != ConnectionSet.BlockedConnection.BanReason.Evil).ToArray()) {
					this.banned.Remove(banned.Key);

					var entry = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == banned.Key);

					if(entry != null) {
						entry.Syncing = true;
					}
				}
			}
		}
		
		public void ClearBanned() {

			lock(this.locker) {
				
				this.FreeLowLevelBans();
				
				// first, we clear the banned transactions that are done their jail time and did not strike more than the acceptable limit
				foreach(var banned in this.banned.Where(c => this.bannedStrikes[c.Key] < BAN_LIMIT).ToArray()) {
					this.banned.Remove(banned.Key);

					var entry = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == banned.Key);

					if(entry != null) {
						entry.Syncing = true;
					}
				}
			}
		}

		public List<Guid> GetBannedIds() {
			lock(this.locker) {
				return this.GetBannedConnections().Select(c => c.ClientUuid).ToList();
			}
		}
		
		public List<Guid> GetSleepingIds() {
			lock(this.locker) {
				return this.GetSleepingConnections().Select(c => c.ClientUuid).ToList();
			}
		}
		

		public List<PeerConnection> GetBannedConnections() {
			lock(this.locker) {
				var rejectedIds = this.banned.Values.Select(r => r.PeerConnection).ToList();

				// this is our final rejected list
				return rejectedIds.Distinct().Shuffle().ToList();
			}
		}
		
		public List<PeerConnection> GetSleepingConnections() {
			lock(this.locker) {
				this.ClearSleeping();
				var rejectedIds = this.sleeping.Where(c => c.Value.IsSleeping).Select(r => r.Value.PeerConnection).ToList();

				// this is our final rejected list
				return rejectedIds.Distinct().Shuffle().ToList();
			}
		}

		/// <summary>
		///     check the list of active peer connections available and update our list to the ones that can sync this blockchain
		/// </summary>
		/// <returns></returns>
		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetNewPotentialConnections(IChainNetworkingProvider chainNetworkingProvider, BlockchainType chainType) {
			lock(this.locker) {
				var newConnections = new List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>();

				// this is our final rejected list
				var rejectedIds = this.GetBannedIds();

				var sleepIds = this.GetSleepingIds();
				
				// now our current connections valid for syncing

				var syncingIds = this.connections.Keys.Where(c => c.Syncing).Select(c => c.PeerConnection.ClientUuid).ToList();

				// get the list of connections that support our chain, are not already syncing, are not sleeping and are not rejected right now
				var newPeers = chainNetworkingProvider.SyncingConnectionsList.Where(p => !rejectedIds.Contains(p.ClientUuid) && !sleepIds.Contains(p.ClientUuid) && !syncingIds.Contains(p.ClientUuid)).ToList();

				foreach(PeerConnection peer in newPeers.ToList()) {
					if(this.connections.All(c => c.Key.PeerConnection.ClientUuid != peer.ClientUuid)) {
						// ok, its a new connection, lets add it to our list
						peer.connection.Disconnected += this.ConnectionOnDisconnected;
					}
					newConnections.Add(new ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> {PeerConnection = peer, Syncing = false, LastCheck = DateTime.UtcNow});
				}

				return newConnections;
			}
		}

		/// <summary>
		/// remove a disconnected connection
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ConnectionOnDisconnected(object sender, DisconnectedEventArgs e) {
			// well, we lost this guy, lets remove the peer connection
			foreach(var peer in this.connections.Keys.Where(c => c.PeerConnection.ClientUuid == ((ITcpConnection)sender).ReportedUuid).ToArray()) {
				try {
					peer.PeerConnection.connection.Disconnected -= this.ConnectionOnDisconnected;
				} catch {
					// do nothing
				}
				this.connections.RemoveSafe(peer);
			}
		}

	
		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetAllConnections() {
			lock(this.locker) {
				return this.connections.Keys.ToList();
			}
		}

		public void RemoveActiveConnection(ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> connection) {

			var cn = this.connections.Keys.SingleOrDefault(c => c.PeerConnection.ClientUuid == connection.PeerConnection.ClientUuid);

			if(cn != null) {
				this.connections.RemoveSafe(cn);
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetActiveConnections() {
			lock(this.locker) {
				// now clear all rejected adn keep only the good connections
				var rejectedIds = this.GetBannedIds();

				var sleepIds = this.GetSleepingIds();
				
				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Keys.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid) && !sleepIds.Contains(c.PeerConnection.ClientUuid)).ToList();
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetSyncingConnections() {
			lock(this.locker) {
				var rejectedIds = this.GetBannedIds();

				var sleepIds = this.GetSleepingIds();
				
				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Keys.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid) && !sleepIds.Contains(c.PeerConnection.ClientUuid)  && c.Syncing).ToList();
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetNonSyncingConnections() {
			lock(this.locker) {
				var rejectedIds = this.GetBannedIds();

				var sleepIds = this.GetSleepingIds();
				
				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Keys.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid) && !sleepIds.Contains(c.PeerConnection.ClientUuid) && !c.Syncing).ToList();
			}
		}

		public interface IActiveConnection {
			PeerConnection PeerConnection { get; set; }
			DateTime? LastCheck { get; set; }
			bool Syncing { get; set; }
			long ReportedDiskBlockHeight { get; set; }
			int ReportedDigestHeight { get; set; }
		}

		public interface IActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IActiveConnection
			where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
			where SERVER_TRIGGER_REPLY : ServerTriggerReply {
			ITargettedMessageSet<CHAIN_SYNC_TRIGGER, IBlockchainEventsRehydrationFactory> Trigger { get; set; }
			ITargettedMessageSet<SERVER_TRIGGER_REPLY, IBlockchainEventsRehydrationFactory> TriggerResponse { get; set; }
		}

		public class ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>
			where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
			where SERVER_TRIGGER_REPLY : ServerTriggerReply {

			public PeerConnection PeerConnection { get; set; }
			public DateTime? LastCheck { get; set; }
			public bool Syncing { get; set; }
			public long ReportedDiskBlockHeight { get; set; }
			public long ReportedPublicBlockHeight { get; set; }
			public int ReportedDigestHeight { get; set; }
			public ITargettedMessageSet<CHAIN_SYNC_TRIGGER, IBlockchainEventsRehydrationFactory> Trigger { get; set; }
			public ITargettedMessageSet<SERVER_TRIGGER_REPLY, IBlockchainEventsRehydrationFactory> TriggerResponse { get; set; }

			public override bool Equals(object obj) {
				if(obj is ConnectionSet.ConnectionStrikeset rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		private void Dispose(bool disposing) {
			if(!this.IsDisposed) {

				if(disposing) {
					
				}

				// the below, we do if disposing or not
				try {
					foreach(var peer in this.connections.Keys.ToArray()) {
						try {
							peer.PeerConnection.connection.Disconnected -= this.ConnectionOnDisconnected;
						} catch {
							// do nothing
						}
					}
					this.connections.Clear();
				} catch {
					// do nothing
				}
			}

			this.IsDisposed = true;
		}

		public bool IsDisposed { get; private set; }

		~ConnectionSet() {
			this.Dispose(false);
		}
	}
}