using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	public interface IMessageRegistrySqliteDal : ISqliteDal<IMessageRegistrySqliteContext>, IMessageRegistryDal {
	}

	public class MessageRegistrySqliteDal : SqliteDal<MessageRegistrySqliteContext>, IMessageRegistrySqliteDal {

		public static readonly TimeSpan ExternalMessageLifetime = TimeSpan.FromMinutes(30);
		public static readonly TimeSpan LocalMessageLifetime = TimeSpan.FromDays(3);

		public MessageRegistrySqliteDal(string folderPath, ServiceSet serviceSet, IDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, st => (MessageRegistrySqliteContext) chainDalCreationFactory.CreateMessageRegistryContext(st), serializationType) {

		}

		/// <summary>
		///     remove all messages that are out of age from the database
		/// </summary>
		public void CleanMessageCache() {

			try {
				this.PerformOperation(db => {
					
					foreach(MessageEntrySqlite staleMessage in db.MessageEntries.Include(me => me.Peers).Where(me => (me.Received.AddTicks(ExternalMessageLifetime.Ticks) < this.timeService.CurrentRealTime) && (me.Local == false))) {
						foreach(MessagePeerSqlite peerEntry in staleMessage.Peers.ToArray()) {
							staleMessage.Peers.Remove(peerEntry);
						}

						db.MessageEntries.Remove(staleMessage);
					}

					foreach(MessageEntrySqlite staleMessage in db.MessageEntries.Include(me => me.Peers).Where(me => (me.Received.AddTicks(LocalMessageLifetime.Ticks) < this.timeService.CurrentRealTime) && me.Local)) {
						foreach(MessagePeerSqlite peerEntry in staleMessage.Peers.ToArray()) {
							staleMessage.Peers.Remove(peerEntry);
						}

						db.MessageEntries.Remove(staleMessage);
					}

					db.SaveChanges();
				});
			} catch(Exception ex) {
				Log.Error(ex, "failed to clean message cache");
			}
		}

		public void AddMessageToCache(long xxhash, bool isvalid, bool local) {
			this.PerformOperation(db => {
				// 

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = db.MessageEntries.SingleOrDefault(me => me.Hash == xxhash);

				if(messageEntrySqlite != null) {
					throw new ApplicationException("A new gossip message we are sending is already in our cache. this should never happen");
				}

				messageEntrySqlite = new MessageEntrySqlite();

				messageEntrySqlite.Hash = xxhash;
				messageEntrySqlite.Received = this.timeService.CurrentRealTime;
				messageEntrySqlite.Valid = isvalid;
				messageEntrySqlite.Local = local;

				db.MessageEntries.Add(messageEntrySqlite);

				db.SaveChanges();
			});
		}

		public void ForwardValidGossipMessage(long xxhash, List<string> activeConnectionIds, Func<List<string>, List<string>> forwardMessageCallback) {
			this.PerformOperation(db => {
				//

				var peerNotReceived = new List<string>();

				// if we have the message in our cache, then we reject it, we got it already

				MessageEntrySqlite messageEntrySqlite = db.MessageEntries.Include(me => me.Peers).SingleOrDefault(me => me.Hash == xxhash);

				if(messageEntrySqlite == null) {
					throw new ApplicationException("Valid message being forwarded was not in the message cache.");
				}

				// since the message was valid, we will forward it to any other peer that may not have ever sent or received it.
				// finally, lets make a lot of all Peers that have NOT received the message, we will forward it to them

				//now all the Peers that received the message that are also in the active connection list
				var activeReceived = messageEntrySqlite.Peers.Where(me => activeConnectionIds.Contains(me.PeerKey)).Select(me => me.PeerKey).ToList();

				//finally, we invert this and get all the active connections that HAVE NOT received the message (as far as we know):

				peerNotReceived.AddRange(activeConnectionIds.Where(ac => !activeReceived.Contains(ac)));

				if(peerNotReceived.Count != 0) {
					// ok, the message was in cache and was valid, lets forward it to any peer that may need it

					var sentKeys = forwardMessageCallback(peerNotReceived);

					// all messages sent, now lets update our cache entry, that we sent it to them so we dont do it again and annoy them
					foreach(string peerKey in sentKeys.Distinct()) // thats it, now we add the outbound message to this peer
					{

						PeerSqlite peerSqlite = db.Peers.SingleOrDefault(p => p.PeerKey == peerKey);

						if(peerSqlite == null) {
							// add the peer
							peerSqlite = new PeerSqlite();
							peerSqlite.PeerKey = peerKey;

							db.Peers.Add(peerSqlite);
						}

						MessagePeerSqlite messagePeerSqlite = messageEntrySqlite.Peers.SingleOrDefault(mp => mp.PeerKey == peerKey);

						// if this peer connection was never recorded, we do so now
						if(messagePeerSqlite == null) {
							// ok, we record this message as having been received from this peer
							messagePeerSqlite = new MessagePeerSqlite();
							messagePeerSqlite.PeerKey = peerKey; // it should exist since we queried it above
							messagePeerSqlite.Received = DateTime.UtcNow;
							messagePeerSqlite.Direction = MessagePeerSqlite.CommunicationDirection.Sent;

							messageEntrySqlite.Peers.Add(messagePeerSqlite);
							peerSqlite.Messages.Add(messagePeerSqlite);
						}
					}

					db.SaveChanges();
				}

			});
		}

		public (bool messageInCache, bool messageValid) CheckRecordMessageInCache<R>(long xxhash, MessagingManager<R>.MessageReceivedTask task, bool returnMessageToSender)
			where R : IRehydrationFactory {

			bool messageInCache = false;
			bool messageValid = false;

			this.PerformOperation(db => {
				//

				// first we add the peer in case it was not already
				PeerSqlite peerSqlite = db.Peers.Include(me => me.Messages).SingleOrDefault(p => p.PeerKey == task.Connection.ScoppedIp);

				if(peerSqlite == null) {
					// add the peer
					peerSqlite = new PeerSqlite();
					peerSqlite.PeerKey = task.Connection.ScoppedIp;

					db.Peers.Add(peerSqlite);
				}

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = db.MessageEntries.Include(me => me.Peers).SingleOrDefault(me => me.Hash == xxhash);

				if(messageEntrySqlite == null) {
					// ok, we had not received it, so its new. lets record it as received. we will add later if it was valid or invalid.
					// ok, its a new message, we will accept it
					messageEntrySqlite = new MessageEntrySqlite();

					messageEntrySqlite.Hash = xxhash;
					messageEntrySqlite.Valid = false; // by default it is invalid, we will confirm later if it passes validation in the chain
					messageEntrySqlite.Received = this.timeService.CurrentRealTime;

					db.MessageEntries.Add(messageEntrySqlite);
				} else {
					messageEntrySqlite.Echos++;
					messageInCache = true; // if we have an entry, then we already received it
				}

				messageValid = messageEntrySqlite.Valid;

				// now no matter what, we mark this message as received from this peer
				MessagePeerSqlite messagePeerSqlite = messageEntrySqlite.Peers.SingleOrDefault(mp => mp.PeerKey == peerSqlite.PeerKey);

				// if this peer connection was never recorded, we do so now (unless they asked to get it back)
				if(messagePeerSqlite == null) {
					if(!returnMessageToSender) {
						// ok, we record this message as having been received from this peer
						messagePeerSqlite = new MessagePeerSqlite();
						messagePeerSqlite.PeerKey = peerSqlite.PeerKey; // it should exist since we queried it above
						messagePeerSqlite.Hash = messageEntrySqlite.Hash;

						messagePeerSqlite.Received = DateTime.UtcNow;
						messagePeerSqlite.Direction = MessagePeerSqlite.CommunicationDirection.Received;

						messageEntrySqlite.Peers.Add(messagePeerSqlite);
						peerSqlite.Messages.Add(messagePeerSqlite);
					}
				} else {
					// well, it seems we have already received this message and consumed it
					// what we will do now is mark this message as received (again) by this peer

					// we record that this peer tried to send us another copy
					messagePeerSqlite.Echos++;
				}

				if(messageInCache) {

				}

				db.SaveChanges();
			});

			return (messageInCache, messageValid);
		}

		/// <summary>
		///     Take a list of messages, and check if we have already received them or not.
		/// </summary>
		/// <param name="hashes"></param>
		/// <returns></returns>
		public List<bool> CheckMessagesReceived(List<long> xxHashes, PeerConnection peerConnectionn) {

			return this.PerformOperation(db => {

				var replies = new List<bool>();

				foreach(long hash in xxHashes) {
					bool reply = true; // true, we want the message

					// if we have the message in our cache, then we reject it, we got it already
					MessageEntrySqlite messageEntrySqlite = db.MessageEntries.Include(me => me.Peers).SingleOrDefault(me => me.Hash == hash);

					if(messageEntrySqlite != null) {

						reply = false; // we already received it, we dont want it anymore.

						// we record that this peer tried to send us a copy
						messageEntrySqlite.Echos++;

						PeerSqlite peerSqlite = db.Peers.Include(me => me.Messages).SingleOrDefault(p => p.PeerKey == peerConnectionn.ScoppedIp);

						if(peerSqlite == null) {
							// add the peer
							peerSqlite = new PeerSqlite();
							peerSqlite.PeerKey = peerConnectionn.ScoppedIp;

							db.Peers.Add(peerSqlite);
						}

						MessagePeerSqlite messagePeerSqlite = messageEntrySqlite.Peers.SingleOrDefault(mp => mp.PeerKey == peerSqlite.PeerKey);

						// if this peer connection was never recorded, we do so now
						if(messagePeerSqlite == null) {
							messagePeerSqlite = new MessagePeerSqlite();
							messagePeerSqlite.PeerKey = peerSqlite.PeerKey; // it should exist since we queried it above
							messagePeerSqlite.Hash = messageEntrySqlite.Hash;

							messageEntrySqlite.Peers.Add(messagePeerSqlite);
							peerSqlite.Messages.Add(messagePeerSqlite);
						}

						messagePeerSqlite.Echos++;
					}

					replies.Add(reply);

					db.SaveChanges();
				}

				return replies;

			});
		}

		/// <summary>
		///     check if a message is in the cache. if it is, we also update the validation status
		/// </summary>
		/// <param name="messagexxHash"></param>
		/// <param name="validated"></param>
		/// <returns></returns>
		public bool CheckMessageInCache(long messagexxHash, bool validated) {
			return this.PerformOperation(db => {

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = db.MessageEntries.SingleOrDefault(me => me.Hash == messagexxHash);

				if(messageEntrySqlite != null) {
					// update its validation status with what we have found
					messageEntrySqlite.Valid = validated;
					db.SaveChanges();

					return true;
				}

				return false;
			});
		}

		public bool GetUnvalidatedBlockGossipMessageCached(long blockId) {
			return this.PerformOperation(db => {
				// if we have the message in our cache, then we reject it, we got it already
				return db.UnvalidatedBlockGossipMessageCacheEntries.Any(e => e.BlockId == blockId);
			});
		}

		public bool CacheUnvalidatedBlockGossipMessage(long blockId, long xxHash) {
			return this.PerformOperation(db => {
				// if we have the message in our cache, then we reject it, we got it already
				UnvalidatedBlockGossipMessageCacheEntrySqlite cachedEntry = db.UnvalidatedBlockGossipMessageCacheEntries.SingleOrDefault(e => (e.BlockId == blockId) && (e.Hash == xxHash));

				if(cachedEntry == null) {

					cachedEntry = new UnvalidatedBlockGossipMessageCacheEntrySqlite();

					cachedEntry.BlockId = blockId;
					cachedEntry.Hash = xxHash;
					cachedEntry.Received = DateTime.UtcNow;

					db.UnvalidatedBlockGossipMessageCacheEntries.Add(cachedEntry);

					db.SaveChanges();

					return true;
				}

				return false;
			});
		}

		public List<long> GetCachedUnvalidatedBlockGossipMessage(long blockId) {

			return this.PerformOperation(db => {
				// lets get all the hashes for this block id
				return db.UnvalidatedBlockGossipMessageCacheEntries.Where(e => e.BlockId == blockId).Select(e => e.Hash).ToList();
			});
		}

		/// <summary>
		///     Clear a block message set, and every block before it
		/// </summary>
		/// <param name="blockIds"></param>
		/// <returns></returns>
		public List<(long blockId, long xxHash)> RemoveCachedUnvalidatedBlockGossipMessages(long blockId) {

			return this.PerformOperation(db => {
				// lets get all the hashes for this block id
				var deletedEntries = db.UnvalidatedBlockGossipMessageCacheEntries.Where(e => e.BlockId <= blockId).ToList();
				db.UnvalidatedBlockGossipMessageCacheEntries.RemoveRange(deletedEntries);

				db.SaveChanges();

				// return the deletd entries
				return deletedEntries.ToList().Select(e => (e.BlockId, e.Hash)).ToList();
			});
		}
	}
}