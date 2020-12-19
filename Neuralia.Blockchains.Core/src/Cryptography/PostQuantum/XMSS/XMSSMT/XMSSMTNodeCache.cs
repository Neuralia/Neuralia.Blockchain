using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT {
	public class XMSSMTNodeCache : IDisposableExtended, IBinarySerializable {

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		public XMSSMTNodeCache() {

		}

		public XMSSMTNodeCache(int height, int layers, int digestSize, int backupDigestSize, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY) {
			this.Height = (byte) height;
			this.Layers = (byte) layers;
			this.ReducedHeight = (byte) (this.Height / this.Layers);
			this.DigestSize = (byte) digestSize;
			this.BackupDigestSize = (byte) backupDigestSize;

			for(int layer = 0; layer < this.Layers; layer++) {
				for(int tree = 0; tree < (1 << ((this.Layers - 1 - layer) * this.ReducedHeight)); tree++) {
					this.CachesTree.AddSafe((tree, layer), new XMSSNodeCache(this.ReducedHeight, this.DigestSize, this.BackupDigestSize, cacheMode, cacheLevels));
				}
			}
		}

		public ConcurrentDictionary<XMSSMTreeId, XMSSNodeCache> CachesTree { get; } = new ConcurrentDictionary<XMSSMTreeId, XMSSNodeCache>();

		public bool IsChanged { get; private set; }
		public byte Height { get; private set; }
		public byte DigestSize { get; private set; }
		public byte BackupDigestSize { get; private set; }
		
		public byte ReducedHeight { get; }
		public byte Layers { get; private set; }

		public XMSSNodeCache this[XMSSMTreeId id] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.CachesTree.ContainsKey(id) ? null : this.CachesTree[id];

		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			dehydrator.Write(this.Height);
			dehydrator.Write(this.Layers);
			dehydrator.Write(this.DigestSize);
			dehydrator.Write(this.BackupDigestSize);
			
			IEnumerable<IGrouping<int, KeyValuePair<XMSSMTreeId, XMSSNodeCache>>> layerGroups = this.CachesTree.GroupBy(e => e.Key.Layer);

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = layerGroups.Count();
			adaptiveLong.Dehydrate(dehydrator);

			foreach(IGrouping<int, KeyValuePair<XMSSMTreeId, XMSSNodeCache>> layerGroup in layerGroups) {

				adaptiveLong.Value = layerGroup.Key;
				adaptiveLong.Dehydrate(dehydrator);

				adaptiveLong.Value = layerGroup.Count();
				adaptiveLong.Dehydrate(dehydrator);

				foreach((XMSSMTreeId key, XMSSNodeCache value) in layerGroup) {
					adaptiveLong.Value = key.Tree;
					adaptiveLong.Dehydrate(dehydrator);

					value.Dehydrate(dehydrator);
				}
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();

			this.Height = rehydrator.ReadByte();
			this.Layers = rehydrator.ReadByte();
			this.DigestSize = rehydrator.ReadByte();
			this.BackupDigestSize = rehydrator.ReadByte();
			
			this.CachesTree.Clear();
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

					XMSSNodeCache xmssNodeCache = new XMSSNodeCache();
					xmssNodeCache.Rehydrate(rehydrator);

					this.CachesTree.AddSafe((tree, layer), xmssNodeCache);
				}
			}
		}

		public void Clear() {
			this.CachesTree.Clear();
		}

		public virtual void Load(ByteArray publicKey) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(publicKey)) {

				this.Rehydrate(rehydrator);
			}
		}

		public virtual ByteArray Save() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToReleasedArray();
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				foreach(XMSSNodeCache entry in this.CachesTree.Values) {
					entry?.Dispose();
				}
			}

			this.IsDisposed = true;
		}

		~XMSSMTNodeCache() {
			this.Dispose(false);
		}

	#endregion

	}
}