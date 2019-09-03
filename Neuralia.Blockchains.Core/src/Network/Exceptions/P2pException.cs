using System;

namespace Neuralia.Blockchains.Core.Network.Exceptions {
	/// <summary>
	///     Wrapper for exceptions thrown from P2p.
	/// </summary>
	[Serializable]
	public class P2pException : Exception {
		public enum Direction {
			Send,
			Receive
		}

		public enum Severity {
			Casual,
			Bad,
			VerySerious
		}

		internal P2pException(string msg, Direction direction, Severity severity) : base(msg) {
		}

		internal P2pException(string msg, Direction direction, Severity severity, Exception e) : base(msg, e) {
		}
	}
}