namespace Neuralia.Blockchains.Core.Network.Protocols {
	public struct ProtocolVersion {

		public byte Version { get; }
		public byte Revision { get; }

		public ProtocolVersion(byte version, byte revision) {
			this.Version = version;
			this.Revision = revision;
		}
	}
}