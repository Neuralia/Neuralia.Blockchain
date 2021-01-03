using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.P2p.Messages.Base;
using Neuralia.Blockchains.Core.P2p.Messages.MessageSets;
using Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Workflows.Base {

	public interface INullWorkflow<R> : IClientWorkflow<NullMessageFactory<R>, R>
		where R : IRehydrationFactory {

	}

	/// <summary>
	/// a workflow that does nothing at all
	/// </summary>
	public class NullWorkflow<R> : ClientWorkflow<NullMessageFactory<R>, R>, INullWorkflow<R>
		where R : IRehydrationFactory {

		public NullWorkflow(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		protected override Task PerformWork(LockContext lockContext) {

			// this workflow does absolutely nothing
			return Task.CompletedTask;
		}

		protected override NullMessageFactory<R> CreateMessageFactory() {
			return new NullMessageFactory<R>(this.serviceSet);
		}
	}

	public class NullMessageFactory<R> : MessageFactory<R>
		where R : IRehydrationFactory {

		public NullMessageFactory(ServiceSet<R> serviceSet) : base(serviceSet) {
		}

		public override ITargettedMessageSet<R> Rehydrate(SafeArrayHandle data, TargettedHeader header, R rehydrationFactory) {
			return new NullTrigger<R>();
		}
	}

	public class NullTrigger<R> : ITargettedMessageSet<R>
		where R : IRehydrationFactory {

		public DateTime ReceivedTime { get; set; }
		RoutingHeader INetworkMessageSet.BaseHeader { get; }
		public TargettedHeader Header { get; set; }
		public TargettedHeader BaseHeader { get; set; }
		public bool HeaderCreated { get; }
		public bool MessageCreated { get; }
		public INetworkMessage BaseMessage2 { get; }

		public SafeArrayHandle Dehydrate() {
			throw new NotImplementedException();
		}

		public INetworkMessage<R> BaseMessage { get; }

		public void RehydrateRest(IDataRehydrator dr, R rehydrationFactory) {
			throw new NotImplementedException();
		}
	}
}