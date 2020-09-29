using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders {
	public class GossipHeader : RoutingHeader {

		public enum GossipNetworkMessageOptions : byte {
			/// <summary>
			///     if this option is set, the peer receivng this message will not mark us as having received this message, and will
			///     send it back.
			///     the peer will always remove this option, so it is only valid for ourselves only (one step)
			/// </summary>
			ReturnMeMessage = 1 << 0
		}

		public GossipHeader() {
			// always true for a workflow message
			this.options.SetOption(Options.WorkflowTrigger);
		}

		/// <summary>
		///     unique hash of the message
		/// </summary>
		public long Hash { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Hash);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Hash = rehydrator.ReadLong();
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (2, 1, 0);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());

			return nodeList;
		}
	}
}