using System.Collections.Generic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {

	/// <summary>
	///     A holder class to handle the nonces that we have loaded
	/// </summary>
	public class XMSSNonceSet {

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		public readonly List<(int nonce1, int nonce2)> Nonces = new List<(int nonce1, int nonce2)>();
		public readonly byte Revision = 0;

		public XMSSNonceSet() {

		}

		public XMSSNonceSet(List<(int nonce1, int nonce2)> nonces) {
			this.Nonces.Clear();

			this.Nonces.AddRange(nonces);
		}

		public (int nonce1, int nonce2) this[int i] => this.Nonces[i];

		public virtual void Load(ByteArray bytes, int leafCount) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			this.Rehydrate(rehydrator, leafCount);
		}

		public virtual ByteArray Save() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			//TODO: this should be a realease, not clone
			return dehydrator.ToArray().Entry.Clone();
		}

		public void Rehydrate(IDataRehydrator rehydrator, int leafCount) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();
			int revision = rehydrator.ReadByte();

			this.Nonces.Clear();

			for(int i = 0; i < leafCount; i++) {
				int nonce1 = rehydrator.ReadInt();
				int nonce2 = rehydrator.ReadInt();
				this.Nonces.Add((nonce1, nonce2));
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);

			foreach((int nonce1, int nonce2) nonce in this.Nonces) {
				dehydrator.Write(nonce.nonce1);
				dehydrator.Write(nonce.nonce2);
			}
		}
	}
}