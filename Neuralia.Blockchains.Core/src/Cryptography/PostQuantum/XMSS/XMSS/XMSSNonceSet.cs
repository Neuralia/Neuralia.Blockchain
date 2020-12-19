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

		public readonly Dictionary<long, (short nonce1, short nonce2)> Nonces = new Dictionary<long, (short nonce1, short nonce2)>();

		public byte NoncesExponent { get; private set; }

		public XMSSNonceSet( byte noncesExponent) {
			this.NoncesExponent = noncesExponent;
		}

		public XMSSNonceSet(List<(short nonce1, short nonce2)> nonces, byte noncesExponent) {

			this.NoncesExponent = noncesExponent;
			this.Nonces.Clear();

			for(int i = 0; i < nonces.Count; i++) {
				this.Nonces.Add(i, nonces[i]);
			}
		}

		public XMSSNonceSet(XMSSNonceSet nonceSet) {
			this.NoncesExponent = nonceSet.NoncesExponent;
			this.Nonces.Clear();

			this.Nonces = nonceSet.Nonces.ToDictionary();
		}

		public (short nonce1, short nonce2) this[long i] => this.Nonces[i >> this.NoncesExponent];

		public virtual void Load(ByteArray bytes, int leafCount) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			this.Rehydrate(rehydrator, leafCount);
		}

		public virtual ByteArray Save(int leafCount) {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator, leafCount);

			return dehydrator.ToArray().Release();
		}

		public void Rehydrate(IDataRehydrator rehydrator, long leafCount) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();

			this.NoncesExponent = rehydrator.ReadByte();
			
			bool full = rehydrator.ReadBool();
			
			AdaptiveLong1_9 adaptiveLong = null;
			long count = leafCount;
			if(full == false) {
				adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				count = adaptiveLong.Value;
			}

			this.Nonces.Clear();

			for(long key = 0; key < count; key++) {

				long currentKey = key;
				if(full == false) {
					adaptiveLong.Rehydrate(rehydrator);
					currentKey = adaptiveLong.Value;
				}

				short nonce1 = rehydrator.ReadShort();
				short nonce2 = rehydrator.ReadShort();
				this.Nonces.Add(currentKey, (nonce1, nonce2));
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator, long leafCount) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			dehydrator.Write(this.NoncesExponent);
			
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