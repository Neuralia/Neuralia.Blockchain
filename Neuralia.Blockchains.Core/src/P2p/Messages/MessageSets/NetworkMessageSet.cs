using System;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.MessageSets {

	public interface INetworkMessageSet {

		DateTime ReceivedTime { get; set; }

		RoutingHeader BaseHeader { get; }

		bool HeaderCreated { get; }

		bool MessageCreated { get; }

		INetworkMessage BaseMessage2 { get; }

		SafeArrayHandle Dehydrate();
	}

	public interface INetworkMessageSet<R> : INetworkMessageSet
		where R : IRehydrationFactory {

		INetworkMessage<R> BaseMessage { get; }

		void RehydrateRest(IDataRehydrator dr, R rehydrationFactory);
	}

	public interface INetworkMessageSet<out T, out H, R> : INetworkMessageSet<R>
		where T : class, INetworkMessage<R>
		where H : RoutingHeader
		where R : IRehydrationFactory {
		H Header { get; }
		T Message { get; }
	}

	public interface INetworkMessageSet2<T, H, R> : INetworkMessageSet<R>
		where T : class, INetworkMessage<R>
		where H : RoutingHeader
		where R : IRehydrationFactory {
		H Header { get; set; }
		T Message { get; set; }
	}

	public static class NetworkMessageSet {
		// extract the message
		public static SafeArrayHandle ExtractMessageBytes(IDataRehydrator dr) {
			ResetAfterHeader(dr);

			return dr.ReadNonNullableArray();
		}

		public static void ResetAfterHeader(IDataRehydrator dr) {
			dr.Rewind2Start();
			dr.SkipByte(); // gossip optionsBase
			dr.SkipSection(); // skip the header

		}
	}

	public abstract class NetworkMessageSet<T, H, R> : INetworkMessageSet<T, H, R>, INetworkMessageSet2<T, H, R>
		where T : class, INetworkMessage<R>
		where H : RoutingHeader
		where R : IRehydrationFactory {

		public H Header { get; set; } = null;

		public T Message { get; set; } = null;

		/// <summary>
		///     this is the local time that we received the message
		/// </summary>
		public DateTime ReceivedTime { get; set; }

		public INetworkMessage<R> BaseMessage => this.Message;
		public INetworkMessage BaseMessage2 => this.Message;

		public RoutingHeader BaseHeader => this.Header;

		public bool HeaderCreated => this.Header != null;
		public bool MessageCreated => this.Message != null;

		/// <summary>
		///     Here we dehydrate everything and compress only the message
		/// </summary>
		/// <returns></returns>
		public SafeArrayHandle Dehydrate() {

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void RehydrateRest(IDataRehydrator dr, R rehydrationFactory) {

			// dont put anything here. the message MUST be next
			this.RehydrateMessage(dr, rehydrationFactory);

			this.RehydrateContents(dr, rehydrationFactory);

		}

		private void Dehydrate(IDataDehydrator dehydrator) {
			// we always serialize a byte for unhashed optionsBase first.
			dehydrator.Write(this.Header.NetworkOptions);

			dehydrator.WriteWrappedContent(dh => {
				this.Header.Dehydrate(dh);
			});

			// dont put anything here, the message MUST be next. see MainChainMessageFactory.RehydrateGossipMessage
			this.DehydrateMessage(dehydrator);

			// anything else before the message
			this.DehydrateContents(dehydrator);
		}

		protected virtual void DehydrateContents(IDataDehydrator dehydrator) {

		}

		protected virtual void DehydrateMessage(IDataDehydrator dehydrator) {
			IDataDehydrator subDehydrator = DataSerializationFactory.CreateDehydrator();
			this.Message.Dehydrate(subDehydrator);

			var bytes = subDehydrator.ToArray();
			dehydrator.WriteNonNullable(bytes);
			bytes.Return();
		}

		/// <summary>
		///     Here we decompress and rehydrate the message
		/// </summary>
		/// <param name="dr"></param>
		protected virtual void RehydrateContents(IDataRehydrator dr, R rehydrationFactory) {

		}

		/// <summary>
		///     Here we decompress and rehydrate the message
		/// </summary>
		/// <param name="dr"></param>
		protected void RehydrateMessage(IDataRehydrator dr, R rehydrationFactory) {

			var bytes = dr.ReadNonNullableArray();

			using(IDataRehydrator subRehydrator = DataSerializationFactory.CreateRehydrator(bytes)) {
				this.BaseMessage.Rehydrate(subRehydrator, rehydrationFactory);

				bytes.Return();
			}
		}
	}
}