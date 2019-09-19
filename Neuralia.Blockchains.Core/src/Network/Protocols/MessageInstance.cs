using System;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class MessageInstance {
		public long Hash { get; set; }
		public bool IsCached { get; set; }
		public int Size { get; set; }
		public SafeArrayHandle MessageBytes { get; } = SafeArrayHandle.Create();
		public ISplitMessageEntry SplitMessage { get; set; }

		public bool IsSpliMessage => this.SplitMessage != null;
	}
}