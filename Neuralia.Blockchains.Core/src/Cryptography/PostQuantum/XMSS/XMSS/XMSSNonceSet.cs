using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
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

		public readonly Dictionary<int, (int nonce1, int nonce2)> Nonces = new Dictionary<int, (int nonce1, int nonce2)>();

		public XMSSNonceSet() {

		}

		public XMSSNonceSet(List<(int nonce1, int nonce2)> nonces) {
			this.Nonces.Clear();

			for(int i = 0; i < nonces.Count; i++) {
				this.Nonces.Add(i, nonces[i]);
			}
		}
		
		public XMSSNonceSet(Dictionary<int, (int nonce1, int nonce2)> nonces) {
			this.Nonces.Clear();

			this.Nonces = nonces.ToDictionary();
		}

		public (int nonce1, int nonce2) this[int i] => this.Nonces[i];

		public virtual void Load(ByteArray bytes, int leafCount) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			this.Rehydrate(rehydrator, leafCount);
		}

		public virtual ByteArray Save(int leafCount) {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator, leafCount);

			return dehydrator.ToArray().Release();
		}

		public void Rehydrate(IDataRehydrator rehydrator, int leafCount) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();

			bool full = rehydrator.ReadBool();
			
			AdaptiveLong1_9 adaptiveLong = null;
			int count = leafCount;
			if(full == false) {
				adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				count = (int) adaptiveLong.Value;
			}

			this.Nonces.Clear();

			for(int i = 0; i < count; i++) {

				int key = i;

				if(full == false) {
					adaptiveLong.Rehydrate(rehydrator);
					key = (int) adaptiveLong.Value;
				}

				int nonce1 = rehydrator.ReadInt();
				int nonce2 = rehydrator.ReadInt();
				this.Nonces.Add(key, (nonce1, nonce2));
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator, int leafCount) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			bool full = this.Nonces.Count == leafCount;
			dehydrator.Write(full);
			
			AdaptiveLong1_9 adaptiveLong = null;

			if(full == false) {
				adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Value = this.Nonces.Count;
				adaptiveLong.Dehydrate(dehydrator);
			}
			
			foreach(var entry in this.Nonces.OrderBy(e => e.Key)) {
				if(full == false) {
					adaptiveLong.Value = entry.Key;
					adaptiveLong.Dehydrate(dehydrator);
				}

				dehydrator.Write(entry.Value.nonce1);
				dehydrator.Write(entry.Value.nonce2);
			}
		}
	}
}