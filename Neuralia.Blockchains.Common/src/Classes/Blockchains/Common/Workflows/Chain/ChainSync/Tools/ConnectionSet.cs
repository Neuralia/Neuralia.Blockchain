using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Tools {

	public interface IConnectionSet {
	}

	public interface IConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IConnectionSet
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply {
	}

	/// <summary>
	///     Various operations to handle our syncing connections
	/// </summary>
	/// TODO: we keep all connections and veil them as rejected or banned. it can be slow to rebuild the veil every request. maybe its better to copy accross various state arrays instead?
	/// <typeparam name="CHAIN_SYNC_TRIGGER"></typeparam>
	/// <typeparam name="SERVER_TRIGGER_REPLY"></typeparam>
	public class ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> : IConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>
		where CHAIN_SYNC_TRIGGER : ChainSyncTrigger
		where SERVER_TRIGGER_REPLY : ServerTriggerReply {

		//TODO: make this something like 5 or 10 minutes
		protected const int REJECTED_TIMEOUT = 60 * 1;


		public const int BAN_LIMIT = 3;
		/// <summary>
		///     And this one has peers that are permanently banned. no way back; we dont trust them at all.
		/// </summary>
		protected readonly Dictionary<Guid, RejectedConnection> bannedChainConnections = new Dictionary<Guid, RejectedConnection>();
		
		protected readonly Dictionary<Guid, int> bannedStrikes = new Dictionary<Guid, int>();

		protected readonly List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> connections = new List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>();

		/// <summary>
		///     How much time in seconds do we exclude a rejected transactin before we give them another try.
		/// </summary>
		private readonly object locker = new object();

		/// <summary>
		///     THis list contains peers that are temporarily banned
		/// </summary>
		protected readonly Dictionary<Guid, RejectedConnection> rejectedChainConnections = new Dictionary<Guid, RejectedConnection>();

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
				
				
				foreach(var entry in newConnections.rejectedChainConnections) {
					temp.AddRejectedConnection(entry.Value.PeerConnection, entry.Value.rejectionReason);
				}
				foreach(var entry in newConnections.bannedChainConnections) {
					
					temp.AddBannedConnection(entry.Value.PeerConnection);
				}
				this.AddValidConnections(newConnections.connections);

				this.Set(temp);
			}
		}

		public void Set(ConnectionSet<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> other) {
			// ok, lets sync the two.  

			lock(this.locker) {
				this.connections.Clear();

				this.rejectedChainConnections.Clear();
				this.bannedChainConnections.Clear();

				this.connections.AddRange(other.connections.Distinct());

				foreach(var entry in other.rejectedChainConnections) {
					this.AddRejectedConnection(entry.Value.PeerConnection, entry.Value.rejectionReason);
				}

				foreach(var entry in other.bannedChainConnections) {
					this.AddBannedConnection(entry.Value.PeerConnection);
					
				}
			}
		}

		private void AddBannedStrike(Guid uuid) {
			if(bannedStrikes.ContainsKey(uuid)) {
				bannedStrikes[uuid]++;
			} else {
				bannedStrikes.Add(uuid, 1);
			}
		}
		public void Set(List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> activeChainConnections) {
			// ok, lets sync the two.  

			lock(this.locker) {
				this.connections.Clear();

				this.rejectedChainConnections.Clear();
				this.bannedChainConnections.Clear();

				this.connections.AddRange(activeChainConnections);
			}
		}

		/// <summary>
		///     can be called by another thread
		/// </summary>
		/// <param name="connections"></param>
		public void AddValidConnections(List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> connections) {

			lock(this.locker) {
				foreach(var entry in connections) {

					if(this.connections.All(p => p.PeerConnection.ClientUuid != entry.PeerConnection.ClientUuid)) {
						this.connections.Add(entry);
					}
				}
			}
		}

		public void AddRejectedConnection(PeerConnection peerConnectionn, RejectedConnection.RejectionReason rejectionReason = RejectedConnection.RejectionReason.None) {

			ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> entry = null;

			lock(this.locker) {
				entry = this.connections.SingleOrDefault(c => c.PeerConnection.ClientUuid == peerConnectionn.ClientUuid);

				if(bannedChainConnections.ContainsKey(peerConnectionn.ClientUuid)) {
					return;
				}

				if(rejectedChainConnections.ContainsKey(peerConnectionn.ClientUuid)) {
					if(rejectedChainConnections[peerConnectionn.ClientUuid].AddRejection()) {
						// thats it, this one is gone
						this.rejectedChainConnections.Remove(peerConnectionn.ClientUuid);

						this.AddBannedConnection(peerConnectionn);
					}
					
					return;
				}
				
				this.rejectedChainConnections.Add(peerConnectionn.ClientUuid, new RejectedConnection(peerConnectionn, rejectionReason));
			}
		}

		public void AddRejectedConnection(Guid clientUuid, RejectedConnection.RejectionReason rejectionReason = RejectedConnection.RejectionReason.None) {
			lock(this.locker) {
				this.AddRejectedConnection(this.connections.Single(c => c.PeerConnection.ClientUuid == clientUuid).PeerConnection, rejectionReason);
			}
		}

		public void AddBannedConnection(PeerConnection peerConnectionn) {
			// remove any rejected if we are banning them. they are out

			ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> entry = null;

			lock(this.locker) {
				this.AddBannedStrike(peerConnectionn.ClientUuid);
				
				if(rejectedChainConnections.ContainsKey(peerConnectionn.ClientUuid)) {
					rejectedChainConnections.Remove(peerConnectionn.ClientUuid);
				}

				if(!bannedChainConnections.ContainsKey(peerConnectionn.ClientUuid)) {
					entry = this.connections.SingleOrDefault(c => c.PeerConnection.ClientUuid == peerConnectionn.ClientUuid);

					if(entry != null) {
						entry.Syncing = false;
						entry.Trigger = null;
						entry.TriggerResponse = null;
						entry.ReportedDiskBlockHeight = 0;
						entry.ReportedDigestHeight = 0;
					}
					
					bannedChainConnections.Add(peerConnectionn.ClientUuid, new RejectedConnection(peerConnectionn, RejectedConnection.RejectionReason.Banned));
				}
			}
		}

		public void FreeRejected() {
			lock(this.locker) {
				// first, we clear the rejected transactions that are done their jail time
				foreach(var entry in this.rejectedChainConnections.ToArray()) {
					entry.Value.ReleaseObsoleteRejections();

					if(entry.Value.IsClear || entry.Value.lastCheck.AddSeconds(REJECTED_TIMEOUT) < DateTime.Now) {
						this.rejectedChainConnections.Remove(entry.Key);
					}
				}
			}
		}

		public void ClearRejected() {
			lock(this.locker) {
				this.rejectedChainConnections.Clear();
			}
		}

		public void ClearBanned() {
			lock(this.locker) {
				// first, we clear the banned transactions that are done their jail time and did not strike more than the acceptable limit
				foreach(var banned in this.bannedChainConnections.Where(c => this.bannedStrikes[c.Key] < BAN_LIMIT).ToArray()) {
					this.bannedChainConnections.Remove(banned.Key);
					
					var entry = this.connections.SingleOrDefault(c => c.PeerConnection.ClientUuid == banned.Key);

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

		public List<PeerConnection> GetBannedConnections() {
			lock(this.locker) {
				var rejectedIds = this.bannedChainConnections.Values.Select(r => r.PeerConnection).ToList();

				// this is our final rejected list
				return rejectedIds.Distinct().Shuffle().ToList();
			}
		}

		/// <summary>
		///     check the list of active peer connections available and update our list to the ones that can sync this blockchain
		/// </summary>
		/// <returns></returns>
		public virtual List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetNewPotentialConnections(IChainNetworkingProvider chainNetworkingProvider, BlockchainType chainType) {
			lock(this.locker) {
				var newConnections = new List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>>();

				// this is our final rejected list
				var rejectedIds = this.GetBannedIds();

				// now our current connections valid for syncing
				List<Guid> syncingIds;

				syncingIds = this.connections.Where(c => c.Syncing).Select(c => c.PeerConnection.ClientUuid).ToList();

				// get the list of connections that support our chain, are not already syncing and are not rejected right now
				var newPeers = chainNetworkingProvider.SyncingConnectionsList.Where(p => !rejectedIds.Contains(p.ClientUuid) && !syncingIds.Contains(p.ClientUuid)).ToList();

				foreach(PeerConnection peer in newPeers.ToList()) {
					if(this.connections.All(c => c.PeerConnection.ClientUuid != peer.ClientUuid)) {
						// ok, its a new connection, lets add it to our list

						// if we get a disconnection, lets handle it and remove the peer
						peer.connection.Disconnected += (sender, args) => {

							// well, we lost this guy, lets remove the peer connection
							this.connections.RemoveAll(c => c.PeerConnection.ClientUuid == peer.ClientUuid);
						};

						newConnections.Add(new ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY> {PeerConnection = peer, Syncing = false, LastCheck = DateTime.Now});
					}
				}

				return newConnections;
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetAllConnections() {
			lock(this.locker) {
				return this.connections.ToList();
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetActiveConnections() {
			lock(this.locker) {
				// now clear all rejected adn keep only the good connections
				var rejectedIds = this.GetBannedIds();

				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid)).ToList();
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetSyncingConnections() {
			lock(this.locker) {
				var rejectedIds = this.GetBannedIds();

				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid) && c.Syncing).ToList();
			}
		}

		public List<ActiveConnection<CHAIN_SYNC_TRIGGER, SERVER_TRIGGER_REPLY>> GetNonSyncingConnections() {
			lock(this.locker) {
				var rejectedIds = this.GetBannedIds();

				// clean our actives array, make sure we have no dirty connections in there by mistake	

				return this.connections.Where(c => !rejectedIds.Contains(c.PeerConnection.ClientUuid) && !c.Syncing).ToList();
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
			public int ReportedDigestHeight { get; set; }
			public ITargettedMessageSet<CHAIN_SYNC_TRIGGER, IBlockchainEventsRehydrationFactory> Trigger { get; set; }
			public ITargettedMessageSet<SERVER_TRIGGER_REPLY, IBlockchainEventsRehydrationFactory> TriggerResponse { get; set; }

			public override bool Equals(object obj) {
				if(obj is RejectedConnection rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}

		public class RejectedConnection {

			public static readonly TimeSpan RejectionTimeout = TimeSpan.FromSeconds(20);
			public const int LIMIT = 3;
			
			public class RejectionSet {

				public readonly DateTime Timestamp = DateTime.Now;
			}

			public enum RejectionReason {
				NoNextBlock,
				NoConnection,
				InvalidResponse,
				NoAnswer,
				CannotHelp,
				Banned,
				None
			}

			public DateTime lastCheck;

			public readonly PeerConnection PeerConnection;
			public RejectionReason rejectionReason;

			public RejectedConnection(PeerConnection peerConnectionn, RejectionReason rejectionReason) {
				this.PeerConnection = peerConnectionn;
				this.lastCheck = DateTime.Now;
				this.rejectionReason = rejectionReason;

				this.AddRejection();
			}

			public bool AddRejection() {
				this.ReleaseObsoleteRejections();
				this.rejections.Add(new RejectionSet());

				return this.IsFinished;
			}

			public readonly List<RejectionSet> rejections = new List<RejectionSet>();

			public bool IsClear => !this.rejections.Any();
			public bool IsFinished => this.rejections.Count >= LIMIT;
			
			public void ReleaseObsoleteRejections() {
				this.rejections.RemoveAll(r => (r.Timestamp+RejectionTimeout)< DateTime.Now);
			}
			
			public override bool Equals(object obj) {
				if(obj is RejectedConnection rc) {
					return rc.PeerConnection.ClientUuid == this.PeerConnection.ClientUuid;
				}

				return base.Equals(obj);
			}

			public override int GetHashCode() {
				return this.PeerConnection.ClientUuid.GetHashCode();
			}
		}
	}
}