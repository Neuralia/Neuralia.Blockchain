using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network.Exceptions;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Tasks.Receivers.Network;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Exceptions;
using Neuralia.Blockchains.Tools.Locking;

namespace Neuralia.Blockchains.Core.Workflows.Base {

	public interface INetworkingWorkflow : IWorkflow {
		uint CorrelationId { get; }
		uint? SessionId { get; }
		Guid ClientId { get; }
	}

	public interface INetworkingWorkflow<R> : IWorkflow<R>, INetworkingWorkflow
		where R : IRehydrationFactory {
	}

	public interface INetworkingWorkflow<in MESSAGE_SET, HEADER, R> : INetworkingWorkflow<R>
		where MESSAGE_SET : INetworkMessageSet<R>
		where HEADER : RoutingHeader
		where R : IRehydrationFactory {

		void ReceiveNetworkMessage(MESSAGE_SET message);
	}

	public static class NetworkingWorkflow {
	}

	public abstract class NetworkingWorkflow<MESSAGE_SET, HEADER, R> : Workflow<R>, INetworkingWorkflow<MESSAGE_SET, HEADER, R>
		where R : IRehydrationFactory
		where MESSAGE_SET : class, INetworkMessageSet<R>
		where HEADER : RoutingHeader {

		/// <summary>
		///     The collection in which other threads post network messages
		/// </summary>
		/// <returns></returns>
		protected readonly NetworkMessageReceiver<MESSAGE_SET> networkMessageReceiver;

		public NetworkingWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {

			this.networkMessageReceiver = new NetworkMessageReceiver<MESSAGE_SET>(serviceSet);

			this.ClientId = Guid.Empty;
		}

		/// <summary>
		///     If false, we use the workflow type to determine if it is equal. If true, we also use the peer ID
		/// </summary>
		public bool PeerUnique { get; protected set; } = false;

		protected bool HasMessages => this.networkMessageReceiver.HasMessage;

		/// <summary>
		///     an ID to correlate between servers and clients
		/// </summary>
		public uint CorrelationId { get; protected set; }

		/// <summary>
		///     an session ID to correlate between servers and clients when there are multiple instances of the same workflow
		/// </summary>
		public uint? SessionId { get; protected set; }

		public override WorkflowId Id => new NetworkWorkflowId(this.ClientId, this.CorrelationId, this.SessionId);

		/// <summary>
		///     The client Scope that is attached to this workflow.abstract If its our own, the ID is 0
		/// </summary>
		public Guid ClientId { get; protected set; }

		public override async Task Stop() {
			await base.Stop().ConfigureAwait(false);

			// just in case
			this.Awaken();
		}

		public void ReceiveNetworkMessage(MESSAGE_SET message) {
			try {
				this.networkMessageReceiver.ReceiveNetworkMessage(message);
			} catch(Exception ex) {
				NLog.Default.Error(ex, "Failed to post network message");
			} finally {
				this.Awaken();
			}
		}

		/// <summary>
		///     for networking workflows, we consider the type and client id if required
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public override bool VirtualMatch(IWorkflow other) {
			bool result = base.VirtualMatch(other);

			if(result && other is INetworkingWorkflow networkingWorkflow) {
				// here we either compare using the peer Id or not
				return !this.PeerUnique || this.CompareOtherPeerId(networkingWorkflow);
			}

			return result;
		}

		protected virtual bool CompareOtherPeerId(IWorkflow other) {
			return true;
		}

		/// <summary>
		///     wait for messages and when received, process the search in the lambda.
		/// </summary>
		/// <param name="Process">returns true if satisfied to end the loop, false if it still needs to wait</param>
		/// <returns></returns>
		private List<MESSAGE_SET> WaitNetworkMessages(Func<MESSAGE_SET, bool> ProcessSingle, Func<List<MESSAGE_SET>, bool> ProcessBatch, TimeSpan? timeout = null, int expectedCount = -1, ManualResetEventSlim autoEvent = null) {

			if(!timeout.HasValue) {
				timeout = DEFAULT_HIBERNATE_TIMEOUT;
			}

			if(autoEvent == null) {
				autoEvent = this.AutoEvent;
			}

			//TODO: is the datetime precision high enough here?
			DateTime timeoutTime = DateTimeEx.CurrentTime + timeout.Value;

			List<MESSAGE_SET> messages = new List<MESSAGE_SET>();

			while(true) {
				try {
					this.CheckShouldCancel();

					List<MESSAGE_SET> result = this.networkMessageReceiver.CheckMessages(ProcessSingle, ProcessBatch);

					// now process the list
					if((result != null) && result.Any()) {
						messages.AddRange(result);

						if((expectedCount == -1) || (messages.Count >= expectedCount)) {
							return messages; // finally, we got it
						}
					}

					DateTime now = DateTimeEx.CurrentTime;

					if(now > timeoutTime) {
						// we timed out
						return messages;
					}

					TimeSpan timeRemaining = timeoutTime - now;

					// lets hibernate for a maximum of 3 second
					int minTimeout = (int) Math.Min(timeRemaining.TotalMilliseconds, 3000);
					this.Hibernate(TimeSpan.FromMilliseconds(minTimeout), autoEvent);

				} catch(ThreadTimeoutException ex) {
					//NLog.Default.Verbose(ex, $"Timeout occured while waiting for a network message for workflow type: {this.GetType().Name}");

					// we return what we have
					return messages;
				}
			}
		}

		protected override Task Initialize(LockContext lockContext) {
			return base.Initialize(lockContext);

		}

		/// <summary>
		///     Get a single message of a specific type. wait untl we receive it
		/// </summary>
		/// <returns></returns>
		protected SPECIALIZED_MESSAGE_SET WaitSingleNetworkMessage<T, SPECIALIZED_MESSAGE_SET, R>(TimeSpan? timeout = null, ManualResetEventSlim autoEvent = null)
			where T : NetworkMessage<R>
			where SPECIALIZED_MESSAGE_SET : class, MESSAGE_SET, INetworkMessageSet<T, HEADER, R>
			where R : IRehydrationFactory {

			List<MESSAGE_SET> messages = this.WaitNetworkMessages(messageSet => {
				if(messageSet.BaseMessage is T) {
					return true;
				}

				return false; // keep waiting
			}, null, timeout, 1, autoEvent);

			if(messages.Count == 0) {
				throw new WorkflowException("We got no message, we were waiting for only one");
			}

			if(messages.Count > 1) {
				// let's check if its the same message by hashing them
				xxHashSakuraTree hasher = new xxHashSakuraTree();

				IEnumerable<long> messageHashes = messages.Select(m => {

					using HashNodeList nodes = m.BaseMessage.GetStructuresArray();

					return hasher.HashLong(nodes);

				}).Distinct();

				if(messages.Count == 1) {
					// the are the same message repeated. lets just take one.
					return messages.First() as SPECIALIZED_MESSAGE_SET;
				}

				// ok, we will take the earliest one and return the later ones
				List<MESSAGE_SET> orderedMessages = messages.OrderBy(m => m.ReceivedTime).ToList();

				// return them ot the received queue
				foreach(MESSAGE_SET message in orderedMessages.Skip(1)) {
					this.networkMessageReceiver.ReceiveNetworkMessage(message);
				}

				NLog.Default.Warning("We got multiple different messages of the expected type, we were waiting for only one. The extra messages are returned to the queue");

				return orderedMessages.First() as SPECIALIZED_MESSAGE_SET;
			}

			return messages.Single() as SPECIALIZED_MESSAGE_SET;
		}

		/// <summary>
		///     Get a single message of any of a list of specific types. wait untl we receive it
		/// </summary>
		/// <param name="types"></param>
		/// <returns></returns>
		/// <exception cref="WorkflowException"></exception>
		protected MESSAGE_SET WaitSingleNetworkMessage(IEnumerable<Type> types, TimeSpan? timeout = null, ManualResetEventSlim autoEvent = null) {

			List<MESSAGE_SET> messages = this.WaitNetworkMessages(types, timeout, 1, autoEvent);

			if(messages.Count == 0) {
				throw new MessageReceptionException("We got no message, we were waiting for only one");
			}

			if(messages.Count > 1) {
				// let's check if its the same message by hashing them
				xxHasher64 hasher = new xxHasher64();

				IEnumerable<long> messageHashes = messages.Select(m => {

					using var array = m.Dehydrate();
					return hasher.HashLong(array);
				}).Distinct();

				if(messageHashes.Count() == 1) {
					// the are the same message repeated. lets just take one.
					return messages.First();
				}

				// ok, we will take the earliest one and return the later ones
				List<MESSAGE_SET> orderedMessages = messages.OrderBy(m => m.ReceivedTime).ToList();

				// return them ot the received queue
				foreach(MESSAGE_SET message in orderedMessages.Skip(1)) {
					this.networkMessageReceiver.ReceiveNetworkMessage(message);
				}

				NLog.Default.Warning("We got multiple different messages of the expected type, we were waiting for only one. The extra messages are returned to the queue");

				return orderedMessages.First();
			}

			return messages.Single();
		}

		/// <summary>
		///     Get a list of messages of a specific type. wait untl we receive them
		/// </summary>
		/// <returns></returns>
		protected List<ITargettedMessageSet<T, R>> WaitNetworkMessages<T, R>(TimeSpan? timeout = null, int expectedCount = -1, ManualResetEventSlim autoEvent = null)
			where T : class, INetworkMessage<R>
			where R : IRehydrationFactory {

			List<MESSAGE_SET> messages = this.WaitNetworkMessages(messageSet => {
				if(messageSet.BaseMessage is T) {
					return true;
				}

				return false; // keep waiting
			}, null, timeout, expectedCount, autoEvent);

			return messages.Cast<ITargettedMessageSet<T, R>>().ToList();
		}

		/// <summary>
		///     Get a list of messages of a specific list of types. wait until we receive them
		/// </summary>
		/// <returns></returns>
		protected List<MESSAGE_SET> WaitNetworkMessages(IEnumerable<Type> messageTypes, TimeSpan? timeout = null, int expectedCount = -1, ManualResetEventSlim autoEvent = null) {
			return this.WaitNetworkMessages(messageSet => messageTypes.Contains(messageSet.BaseMessage.GetType()), null, timeout, expectedCount, autoEvent);
		}

		protected override void LogWorkflowException(Exception ex) {

			// dont show send message exceptions
			if(ex is AggregateException aex && (aex.InnerExceptions.Count == 1) && (aex.InnerException is SendMessageException sendEx || aex.InnerException is WorkflowNetworkException wnex)) {
				// make this exception less intense. we dont need to log it. it will be logged in the connection manager

			} else {
				base.LogWorkflowException(ex);
			}
		}
	}

}