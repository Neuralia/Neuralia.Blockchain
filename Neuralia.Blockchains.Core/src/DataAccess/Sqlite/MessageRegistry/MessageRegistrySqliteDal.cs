using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry {
	public interface IMessageRegistrySqliteDal : ISqliteDal<IMessageRegistrySqliteContext>, IMessageRegistryDal {
	}

	public class MessageRegistrySqliteDal : SqliteDal<MessageRegistrySqliteContext>, IMessageRegistrySqliteDal {

		public static readonly TimeSpan ExternalMessageLifetime = TimeSpan.FromMinutes(30);
		public static readonly TimeSpan LocalMessageLifetime = TimeSpan.FromDays(3);

		public MessageRegistrySqliteDal(string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, st => (MessageRegistrySqliteContext) chainDalCreationFactory.CreateMessageRegistryContext(st), serializationType) {

		}

		/// <summary>
		///     remove all messages that are out of age from the database
		/// </summary>
		public async Task CleanMessageCache() {

			try {
				await this.PerformOperationAsync(async db => {

					var now = DateTimeEx.CurrentTime;
					foreach(MessageEntrySqlite staleMessage in db.MessageEntries.Include(me => me.Peers).Where(me => (me.Received.AddTicks(ExternalMessageLifetime.Ticks) < now) && (me.Local == false))) {
						foreach(MessagePeerSqlite peerEntry in staleMessage.Peers.ToArray()) {
							staleMessage.Peers.Remove(peerEntry);
						}

						db.MessageEntries.Remove(staleMessage);
					}

					foreach(MessageEntrySqlite staleMessage in db.MessageEntries.Include(me => me.Peers).Where(me => (me.Received.AddTicks(LocalMessageLifetime.Ticks) < now) && me.Local)) {
						foreach(MessagePeerSqlite peerEntry in staleMessage.Peers.ToArray()) {
							staleMessage.Peers.Remove(peerEntry);
						}

						db.MessageEntries.Remove(staleMessage);
					}

					await db.SaveChangesAsync().ConfigureAwait(false);
				}).ConfigureAwait(false);
			} catch(Exception ex) {
				NLog.Default.Error(ex, "failed to clean message cache");
			}
		}

		public Task AddMessageToCache(long xxhash, bool isvalid, bool local) {
			return this.PerformOperationAsync(async db => {
				// 

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = await db.MessageEntries.SingleOrDefaultAsync(me => me.Hash == xxhash).ConfigureAwait(false);

				if(messageEntrySqlite != null) {
					throw new ApplicationException("A new gossip message we are sending is already in our cache. this should never happen");
				}

				messageEntrySqlite = new MessageEntrySqlite();

				messageEntrySqlite.Hash = xxhash;
				messageEntrySqlite.Received = DateTimeEx.CurrentTime;
				messageEntrySqlite.Valid = isvalid;
				messageEntrySqlite.Local = local;

				db.MessageEntries.Add(messageEntrySqlite);

				await db.SaveChangesAsync().ConfigureAwait(false);
			});
		}

		public Task ForwardValidGossipMessage(long xxhash, List<string> activeConnectionIds, Func<List<string>, List<string>> forwardMessageCallback) {
			return this.PerformOperationAsync(async db => {
				//

				List<string> peerNotReceived = new List<string>();

				// if we have the message in our cache, then we reject it, we got it already

				MessageEntrySqlite messageEntrySqlite = await db.MessageEntries.Include(me => me.Peers).SingleOrDefaultAsync(me => me.Hash == xxhash).ConfigureAwait(false);

				if(messageEntrySqlite == null) {
					throw new ApplicationException("Valid message being forwarded was not in the message cache.");
				}

				// since the message was valid, we will forward it to any other peer that may not have ever sent or received it.
				// finally, lets make a lot of all Peers that have NOT received the message, we will forward it to them

				//now all the Peers that received the message that are also in the active connection list
				List<string> activeReceived = messageEntrySqlite.Peers.Where(me => activeConnectionIds.Contains(me.PeerKey)).Select(me => me.PeerKey).ToList();

				//finally, we invert this and get all the active connections that HAVE NOT received the message (as far as we know):

				peerNotReceived.AddRange(activeConnectionIds.Where(ac => !activeReceived.Contains(ac)));

				if(peerNotReceived.Count != 0) {
					// ok, the message was in cache and was valid, lets forward it to any peer that may need it

					List<string> sentKeys = forwardMessageCallback(peerNotReceived);

					// all messages sent, now lets update our cache entry, that we sent it to them so we dont do it again and annoy them
					foreach(string peerKey in sentKeys.Distinct()) // thats it, now we add the outbound message to this peer
					{

						PeerSqlite peerSqlite = await db.Peers.SingleOrDefaultAsync(p => p.PeerKey == peerKey).ConfigureAwait(false);

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
							messagePeerSqlite.Received = DateTimeEx.CurrentTime;
							messagePeerSqlite.Direction = MessagePeerSqlite.CommunicationDirection.Sent;

							messageEntrySqlite.Peers.Add(messagePeerSqlite);
							peerSqlite.Messages.Add(messagePeerSqlite);
						}
					}

					await db.SaveChangesAsync().ConfigureAwait(false);
				}

			});
		}

		public async Task<(bool messageInCache, bool messageValid)> CheckRecordMessageInCache<R>(long xxhash, MessagingManager<R>.MessageReceivedTask task, bool returnMessageToSender)
			where R : IRehydrationFactory {

			bool messageInCache = false;
			bool messageValid = false;

			await this.PerformOperationAsync(async db => {
				//

				// first we add the peer in case it was not already
				PeerSqlite peerSqlite = await db.Peers.Include(me => me.Messages).SingleOrDefaultAsync(p => p.PeerKey == task.Connection.ScoppedIp).ConfigureAwait(false);

				if(peerSqlite == null) {
					// add the peer
					peerSqlite = new PeerSqlite();
					peerSqlite.PeerKey = task.Connection.ScoppedIp;

					db.Peers.Add(peerSqlite);
				}

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = await db.MessageEntries.Include(me => me.Peers).SingleOrDefaultAsync(me => me.Hash == xxhash).ConfigureAwait(false);

				if(messageEntrySqlite == null) {
					// ok, we had not received it, so its new. lets record it as received. we will add later if it was valid or invalid.
					// ok, its a new message, we will accept it
					messageEntrySqlite = new MessageEntrySqlite();

					messageEntrySqlite.Hash = xxhash;
					messageEntrySqlite.Valid = false; // by default it is invalid, we will confirm later if it passes validation in the chain
					messageEntrySqlite.Received = DateTimeEx.CurrentTime;

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

						messagePeerSqlite.Received = DateTimeEx.CurrentTime;
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

				await db.SaveChangesAsync().ConfigureAwait(false);
			}).ConfigureAwait(false);

			return (messageInCache, messageValid);
		}

		/// <summary>
		///     Take a list of messages, and check if we have already received them or not.
		/// </summary>
		/// <param name="hashes"></param>
		/// <returns></returns>
		public Task<List<bool>> CheckMessagesReceived(List<long> xxHashes, PeerConnection peerConnectionn) {

			return this.PerformOperationAsync(async db => {

				List<bool> replies = new List<bool>();

				foreach(long hash in xxHashes) {
					bool reply = true; // true, we want the message

					// if we have the message in our cache, then we reject it, we got it already
					MessageEntrySqlite messageEntrySqlite = await db.MessageEntries.Include(me => me.Peers).SingleOrDefaultAsync(me => me.Hash == hash).ConfigureAwait(false);

					if(messageEntrySqlite != null) {

						reply = false; // we already received it, we dont want it anymore.

						// we record that this peer tried to send us a copy
						messageEntrySqlite.Echos++;

						PeerSqlite peerSqlite = await db.Peers.Include(me => me.Messages).SingleOrDefaultAsync(p => p.PeerKey == peerConnectionn.ScoppedIp).ConfigureAwait(false);

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

					await db.SaveChangesAsync().ConfigureAwait(false);
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
		public Task<bool> CheckMessageInCache(long messagexxHash, bool validated) {
			return this.PerformOperationAsync(async db => {

				// if we have the message in our cache, then we reject it, we got it already
				MessageEntrySqlite messageEntrySqlite = await db.MessageEntries.SingleOrDefaultAsync(me => me.Hash == messagexxHash).ConfigureAwait(false);

				if(messageEntrySqlite != null) {
					// update its validation status with what we have found
					messageEntrySqlite.Valid = validated;
					await db.SaveChangesAsync().ConfigureAwait(false);

					return true;
				}

				return false;
			});
		}

		public Task<bool> GetUnvalidatedBlockGossipMessageCached(long blockId) {
			return this.PerformOperationAsync(db => {
				// if we have the message in our cache, then we reject it, we got it already
				return db.UnvalidatedBlockGossipMessageCacheEntries.AnyAsync(e => e.BlockId == blockId);
			});
		}

		public Task<bool> CacheUnvalidatedBlockGossipMessage(long blockId, long xxHash) {
			return this.PerformOperationAsync(async db => {
				// if we have the message in our cache, then we reject it, we got it already
				UnvalidatedBlockGossipMessageCacheEntrySqlite cachedEntry = await db.UnvalidatedBlockGossipMessageCacheEntries.SingleOrDefaultAsync(e => (e.BlockId == blockId) && (e.Hash == xxHash)).ConfigureAwait(false);

				if(cachedEntry == null) {

					cachedEntry = new UnvalidatedBlockGossipMessageCacheEntrySqlite();

					cachedEntry.BlockId = blockId;
					cachedEntry.Hash = xxHash;
					cachedEntry.Received = DateTimeEx.CurrentTime;

					db.UnvalidatedBlockGossipMessageCacheEntries.Add(cachedEntry);

					await db.SaveChangesAsync().ConfigureAwait(false);

					return true;
				}

				return false;
			});
		}

		public Task<List<long>> GetCachedUnvalidatedBlockGossipMessage(long blockId) {

			return this.PerformOperationAsync(db => {
				// lets get all the hashes for this block id
				return db.UnvalidatedBlockGossipMessageCacheEntries.Where(e => e.BlockId == blockId).Select(e => e.Hash).ToListAsync();
			});
		}

		/// <summary>
		///     Clear a block message set, and every block before it
		/// </summary>
		/// <param name="blockIds"></param>
		/// <returns></returns>
		public Task<List<(long blockId, long xxHash)>> RemoveCachedUnvalidatedBlockGossipMessages(long blockId) {

			return this.PerformOperationAsync(async db => {
				// lets get all the hashes for this block id
				List<UnvalidatedBlockGossipMessageCacheEntrySqlite> deletedEntries = await db.UnvalidatedBlockGossipMessageCacheEntries.Where(e => e.BlockId <= blockId).ToListAsync().ConfigureAwait(false);
				db.UnvalidatedBlockGossipMessageCacheEntries.RemoveRange(deletedEntries);

				await db.SaveChangesAsync().ConfigureAwait(false);

				// return the deletd entries
				return deletedEntries.ToList().Select(e => (e.BlockId, e.Hash)).ToList();
			});
		}
	}
}