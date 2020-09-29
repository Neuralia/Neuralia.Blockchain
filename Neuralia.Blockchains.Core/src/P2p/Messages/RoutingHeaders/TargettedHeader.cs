using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders {
	public class TargettedHeader : RoutingHeader {

		public enum TargettedNetworkMessageOptions : byte {
			/// <summary>
			///     if this option is set, the peer receivng this message will not mark us as having received this message, and will
			///     send it back.
			///     the peer will always remove this option, so it is only valid for ourselves only (one step)
			/// </summary>
			//ReturnMeMessage = 1 << 0
		}

		/// <summary>
		///     the client id of the creator of the workflow. Allows us to know if we created it, or if the client did.
		/// </summary>
		public Guid OriginatorId { get; set; }

		public uint WorkflowCorrelationId { get; set; }

		public uint? WorkflowSessionId { get; set; }

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.WorkflowCorrelationId);
			dehydrator.Write(this.WorkflowSessionId);
			dehydrator.Write(this.OriginatorId);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.WorkflowCorrelationId = rehydrator.ReadUInt();
			this.WorkflowSessionId = rehydrator.ReadNullableUInt();
			this.OriginatorId = rehydrator.ReadGuid();
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (1, 1, 0);
		}

		public void SetWorkflowTrigger() {
			this.options.SetOption(Options.WorkflowTrigger);
		}

		public void RemoveWorkflowTrigger() {
			this.options.RemoveOption(Options.WorkflowTrigger);
		}

		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(base.GetStructuresArray());

			nodeList.Add(this.WorkflowCorrelationId);
			nodeList.Add(this.WorkflowSessionId);
			nodeList.Add(this.OriginatorId);

			return nodeList;
		}
	}
}