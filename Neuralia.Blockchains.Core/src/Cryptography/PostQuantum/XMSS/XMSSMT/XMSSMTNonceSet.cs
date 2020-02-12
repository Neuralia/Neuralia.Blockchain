using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT {

	/// <summary>
	///     A holder class to handle the nonces that we have loaded
	/// </summary>
	public class XMSSMTNonceSet {

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		public readonly Dictionary<XMSSMTLeafId, (int nonce1, int nonce2)> Nonces = new Dictionary<XMSSMTLeafId, (int nonce1, int nonce2)>();
		public readonly byte Revision = 0;

		public XMSSMTNonceSet() {

		}

		public XMSSMTNonceSet(Dictionary<XMSSMTLeafId, (int nonce1, int nonce2)> nonces) {
			this.Nonces.Clear();

			foreach(var nonce in nonces) {
				this.Nonces.Add(nonce.Key, nonce.Value);
			}
		}

		public (int nonce1, int nonce2) this[XMSSMTLeafId i] => this.Nonces[i];

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
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			int layerCount = (int) adaptiveLong.Value;

			for(int i = 0; i < layerCount; i++) {
				adaptiveLong.Rehydrate(rehydrator);
				int layer = (int) adaptiveLong.Value;

				adaptiveLong.Rehydrate(rehydrator);
				int count = (int) adaptiveLong.Value;

				for(int j = 0; j < count; j++) {
					adaptiveLong.Rehydrate(rehydrator);
					int tree = (int) adaptiveLong.Value;

					for(int k = 0; k < leafCount; k++) {
						int nonce1 = rehydrator.ReadInt();
						int nonce2 = rehydrator.ReadInt();
						this.Nonces.Add((k, tree, layer), (nonce1, nonce2));
					}
				}
			}
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);

			var layerGroups = this.Nonces.GroupBy(e => e.Key.Layer).ToList();

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = layerGroups.Count();
			adaptiveLong.Dehydrate(dehydrator);

			foreach(var layerGroup in layerGroups.OrderBy(e => e.Key)) {

				adaptiveLong.Value = layerGroup.Key;
				adaptiveLong.Dehydrate(dehydrator);
				
				var treeGroups = layerGroup.GroupBy(e => e.Key.Tree).OrderBy(e => e.Key).ToList();

				adaptiveLong.Value = treeGroups.Count();
				adaptiveLong.Dehydrate(dehydrator);

				
				foreach(var entry in treeGroups.OrderBy(e => e.Key)) {
					adaptiveLong.Value = entry.Key;
					adaptiveLong.Dehydrate(dehydrator);

					foreach(var nonce in entry.OrderBy(e => e.Key.Index)) {
						dehydrator.Write(nonce.Value.nonce1);
						dehydrator.Write(nonce.Value.nonce2);
					}
				}
			}
		}
	}
}