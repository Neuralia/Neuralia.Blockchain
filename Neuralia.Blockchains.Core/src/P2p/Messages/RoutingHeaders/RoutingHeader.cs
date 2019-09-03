using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.P2p.Messages.RoutingHeaders {
	public interface IRoutingHeader : IVersionable<SimpleUShort> {
		BlockchainType ChainId { get; }
		bool IsWorkflowTrigger { get; }
		Guid ClientId { get; set; }
		ByteExclusiveOption NetworkOptions { get; set; }
	}

	public abstract class RoutingHeader : Versionable<SimpleUShort>, IRoutingHeader {
		/// <summary>
		///     optionsBase
		/// </summary>
		/// <returns></returns>
		public enum Options : byte {
			WorkflowTrigger = 1 << 0,
			Compressed = 1 << 1,
			Option3 = 1 << 2,

			// This is a special reserved option, to indicate a special confirm IP protocol.
			IPConfirmation = 1 << 7
		}

		public ushort chainId;

		/// <summary>
		///     Various more serious optionsBase about the message
		/// </summary>
		public ByteExclusiveOption<Options> options = new ByteExclusiveOption<Options>();

		/// <summary>
		///     this is the time the other side sent the message
		/// </summary>
		public DateTime SentTime;

		public RoutingHeader() {

			//for now, we always set it on. later, we can make this more sophisticated. for example, small messages may not need to be compressed
			this.options.SetOption(Options.Compressed);
		}

		public byte Type { get; set; }
		public byte Major { get; set; }
		public byte Minor { get; set; }

		public BlockchainType ChainId => this.chainId;

		// we use this to store the ClientId of the peer who sent us this message, when applicable
		public Guid ClientId { get; set; }

		/// <summary>
		///     Various network behavior optionsBase we wish to send to the peers about this message. This one is very special,
		///     because
		///     along with the has itself, it is the only parameters that is NOT hashed in the message. which means they can be
		///     modified without affecting message final hashing verifications of the message. use wisely.
		/// </summary>
		public ByteExclusiveOption NetworkOptions { get; set; } = new ByteExclusiveOption();

		public bool IsWorkflowTrigger => this.options.HasOption((byte) Options.WorkflowTrigger);

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.chainId);
			nodeList.Add(this.options);
			nodeList.Add(this.SentTime);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.chainId);
			dehydrator.Write(this.options);

			dehydrator.Write(this.SentTime);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.chainId = rehydrator.ReadUShort();
			this.options = rehydrator.ReadByte();
			this.SentTime = rehydrator.ReadDateTime();
		}
	}
}