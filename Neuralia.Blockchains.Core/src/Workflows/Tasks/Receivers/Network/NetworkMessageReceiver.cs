using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.Tools;
using Serilog;

namespace Neuralia.Blockchains.Core.Workflows.Tasks.Receivers.Network {
	public class NetworkMessageReceiver<T>
		where T : INetworkMessageSet {

		/// <summary>
		///     The collection in which other threads post network messages
		/// </summary>
		/// <returns></returns>
		private readonly ConcurrentDictionary<int, T> messageQueue = new ConcurrentDictionary<int, T>();

		private readonly Dictionary<int, T> transferredQueue = new Dictionary<int, T>();

		protected object locker = new object();
		protected object transferredQueueLocker = new object();

		public NetworkMessageReceiver(ServiceSet serviceSet) {

		}

		public bool HasMessage => this.messageQueue.Any();

		public event Action MessageReceived;

		/// <summary>
		///     Check for messsages received in our queue. First, we take them out of the thread safe queue, then we run
		///     validation callbacks to see if we take them
		/// </summary>
		/// <param name="ProcessSingle"></param>
		/// <param name="ProcessBatch"></param>
		/// <returns></returns>
		public List<T> CheckMessages(Func<T, bool> ProcessSingle, Func<List<T>, bool> ProcessBatch) {
			List<KeyValuePair<int, T>> results = null;

			// transfer messages
			foreach(KeyValuePair<int, T> messageSet in this.messageQueue.ToArray()) {
				this.messageQueue.RemoveSafe(messageSet.Key);

				lock(this.transferredQueueLocker) {
					if(!this.transferredQueue.ContainsKey(messageSet.Key)) {
						this.transferredQueue.Add(messageSet.Key, messageSet.Value);
					}
				}
			}

			KeyValuePair<int, T>[] transferredQueue = null;

			lock(this.transferredQueueLocker) {
				transferredQueue = this.transferredQueue.ToArray();
			}

			foreach(KeyValuePair<int, T> messageSet in transferredQueue) {
				if(results == null) {
					results = new List<KeyValuePair<int, T>>();
				}

				if(ProcessSingle(messageSet.Value)) {
					results.Add(messageSet);
				}
			}

			if((results != null) && results.Any()) {
				List<T> messageResult = results.Select(v => v.Value).ToList();

				// now check the entire collection to see if it passes the group test
				if(((ProcessBatch != null) && ProcessBatch(messageResult)) || results.Any()) {

					// ok, we accepted the messages. lets remove them from the pending queue and return them
					foreach(KeyValuePair<int, T> messageSet in results) {
						lock(this.transferredQueueLocker) {
							this.transferredQueue.Remove(messageSet.Key);
						}
					}

					return messageResult;
				}
			}

			return null; // we failed the test, so we return an empty list 

		}

		public void ReceiveNetworkMessage(T message) {
			try {
				this.messageQueue.AddSafe(message.GetHashCode(), message);

				if(this.MessageReceived != null) {
					this.MessageReceived();
				}
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to post network message");
			}
		}
	}
}