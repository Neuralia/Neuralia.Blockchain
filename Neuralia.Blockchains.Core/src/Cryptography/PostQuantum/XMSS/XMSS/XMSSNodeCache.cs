using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	public class XMSSNodeCache : IDisposableExtended, IBinarySerializable {

		public enum XMSSCacheModes:byte {
			None = 0,
			Manual = 1,
			Automatic= 2,
			AllButFirst= 3,
			All = 4,
			
		}
		
		public const byte LEVELS_TO_CACHE_ABSOLUTELY = 9;
		public const byte AUTOMATIC_IDEAL_UNCACHED_LEVELS = 8; // 256 (2^8) nodes to compute
		
		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		private readonly ConcurrentDictionary<XMSSNodeId, (ByteArray root, ByteArray backupRoot)> nodes = new ConcurrentDictionary<XMSSNodeId, (ByteArray root, ByteArray backupRoot)>();

		private byte maximumCachedLevel;
		private byte minimumCachedLevel;
		private XMSSCacheModes cacheMode = XMSSCacheModes.Automatic;
		private byte cacheLevels = LEVELS_TO_CACHE_ABSOLUTELY;
		private byte height;

		public XMSSNodeCache() {

		}

		public XMSSNodeCache(XMSSNodeCache other) {
			this.Height = other.Height;
			this.DigestSize = other.DigestSize;
			this.BackupDigestSize = other.BackupDigestSize;
			this.CacheMode = other.CacheMode;
			this.CacheLevels = other.CacheLevels;

			foreach(var node in other.nodes) {
				this.nodes.TryAdd(node.Key, (node.Value.root.Clone(), node.Value.backupRoot.Clone()));
			}
		}

		public XMSSNodeCache(int height, int digestSize, int backupDigestSize, XMSSCacheModes cacheMode = XMSSCacheModes.Automatic, byte cacheLevels = LEVELS_TO_CACHE_ABSOLUTELY) {
			this.Height = (byte) height;
			this.DigestSize = (byte) digestSize;
			this.BackupDigestSize = (byte) backupDigestSize;
			this.CacheMode = cacheMode;
			this.CacheLevels = cacheLevels;
		}

		public byte Height {
			get => this.height;
			set {
				this.height = value;
				(this.minimumCachedLevel, this.maximumCachedLevel) = this.GetCachedLevels();
			}
		}

		public byte DigestSize { get; private set; }
		public byte BackupDigestSize { get; private set; }

		public XMSSCacheModes CacheMode {
			get => this.cacheMode;
			set {
				this.cacheMode = value;
				(this.minimumCachedLevel, this.maximumCachedLevel) = this.GetCachedLevels();
			}
		}

		public byte CacheLevels {
			get => this.cacheLevels;
			private set {
				this.cacheLevels = value;
				(this.minimumCachedLevel, this.maximumCachedLevel) = this.GetCachedLevels();
			}
		}
		
		public byte MinimumCachedLevel {
			get => this.minimumCachedLevel;
		}
		
		public byte MaximumCachedLevel {
			get => this.maximumCachedLevel;
		}
		
		public (ByteArray root, ByteArray backupRoot) this[XMSSNodeId id] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.nodes.ContainsKey(id) ? (null, null) : (this.nodes[id].root.Clone(), this.nodes[id].backupRoot.Clone());
		}

		public (ByteArray root, ByteArray backupRoot) GetOriginals(XMSSNodeId id) {
			return !this.nodes.ContainsKey(id) ? (null, null) : (this.nodes[id].root, this.nodes[id].backupRoot);
		}

		public XMSSNodeCache Clone => new XMSSNodeCache(this);
		
		public List<XMSSNodeId> NodeIds => this.nodes.Keys.ToList();

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			dehydrator.Write(this.Height);
			dehydrator.Write(this.DigestSize);
			dehydrator.Write(this.BackupDigestSize);
			
			dehydrator.Write((byte)this.CacheMode);
			dehydrator.Write(this.CacheLevels);
			
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = this.nodes.Count;
			adaptiveLong.Dehydrate(dehydrator);

			foreach((XMSSNodeId key, var value) in this.nodes) {

				adaptiveLong.Value = key.Index;
				adaptiveLong.Dehydrate(dehydrator);
				
				dehydrator.Write((byte) key.Height);

				dehydrator.Write(value.root.IsEmpty);

				if(!value.root.IsEmpty) {
					dehydrator.WriteRawArray(value.root);
				}
				
				dehydrator.Write(value.backupRoot.IsEmpty);

				if(!value.backupRoot.IsEmpty) {
					dehydrator.WriteRawArray(value.backupRoot);
				}
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();

			this.Height = rehydrator.ReadByte();
			this.DigestSize = rehydrator.ReadByte();
			this.BackupDigestSize = rehydrator.ReadByte();
			this.CacheMode = rehydrator.ReadByteEnum<XMSSCacheModes>();
			this.CacheLevels = rehydrator.ReadByte();
			
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			int count = (int)adaptiveLong.Value;

			this.nodes.Clear();

			for(int i = 0; i < count; i++) {

				adaptiveLong.Rehydrate(rehydrator);
				int index = (int) adaptiveLong.Value;
				byte height = rehydrator.ReadByte();

				bool isEmpty = rehydrator.ReadBool();

				ByteArray buffer = null;

				if(!isEmpty) {
					buffer = rehydrator.ReadArray(this.DigestSize);
				} else {
					buffer = ByteArray.Create();
				}
				
				isEmpty = rehydrator.ReadBool();

				ByteArray backupbuffer = null;

				if(!isEmpty) {
					backupbuffer = rehydrator.ReadArray(this.BackupDigestSize);
				} else {
					backupbuffer = ByteArray.Create();
				}

				this.nodes.AddSafe((index, height), (buffer, backupbuffer));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Cache(XMSSNodeId id, (ByteArray root, ByteArray backupRoot) node, bool force = false) {
			if(this.nodes.ContainsKey(id)) {
				return;
			}

			// if this is true, we cache no matter what
			if(this.IsLevelCached(id.Height)) {
				this.nodes.AddSafe(id, (node.root.Clone(), node.backupRoot?.Clone()));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsLevelCached(byte level) {
			return level >= this.minimumCachedLevel && level <= this.maximumCachedLevel;
		}

		private (byte minimum, byte maximum) GetCachedLevels() {
			if(this.CacheMode == XMSSCacheModes.None) {
				return (byte.MaxValue, 0);
			}
			else if(this.CacheMode == XMSSCacheModes.All) {
				return (0, this.Height);
			}
			else if(this.CacheMode == XMSSCacheModes.AllButFirst) {
				return (1, this.Height);
			}
			else if(this.CacheMode == XMSSCacheModes.Manual) {
				return ((byte)Math.Max((this.Height - this.CacheLevels), 0), this.Height);
			}
			else if(this.CacheMode == XMSSCacheModes.Automatic) {

				return ((byte)Math.Min(Math.Max(this.Height-LEVELS_TO_CACHE_ABSOLUTELY, 0), AUTOMATIC_IDEAL_UNCACHED_LEVELS), this.Height);
			}

			throw new ArgumentException();
		}

		public void Clear() {
			foreach(var node in this.nodes) {
				node.Value.root?.Dispose();
				node.Value.backupRoot?.Dispose();
			}

			this.nodes.Clear();
		}

		public void Merge(XMSSNodeCache other) {
			if(other == null) {
				return;
			}
			this.Merge(other.nodes);
		}

		public void Merge(ConcurrentDictionary<XMSSNodeId, (ByteArray root, ByteArray backupRoot)> nodes) {
			foreach(var node in nodes) {
				if(!this.nodes.ContainsKey(node.Key)) {
					
					this.nodes.TryAdd(node.Key, (node.Value.root.Clone(), node.Value.backupRoot.Clone()));
				}
			}
		}
		
		public void ClearNodes(XMSSNodeCache other, bool clearContent = true) {
			if(other == null) {
				return;
			}
			this.ClearNodes(other.NodeIds, clearContent);
		}

		public void ClearNodes(IEnumerable<XMSSNodeId> excludeNodes, bool clearContent = true) {
			foreach(XMSSNodeId id in excludeNodes) {
				if(this.nodes.ContainsKey(id)) {

					if(clearContent) {
						this.nodes[id].root?.Return();
						this.nodes[id].backupRoot?.Return();
					}

					this.nodes.RemoveSafe(id);
				}
			}
		}

		public void ClearLevel(int level, IEnumerable<long> excludedIndices, bool clearContent = true) {

			var excludedIndicesHash = excludedIndices.ToHashSet();

			this.ClearNodes(this.nodes.Keys.Where(e => e.Height == level && !excludedIndicesHash.Contains(e.Index)), clearContent);
		}
		
		public virtual void Load(SafeArrayHandle bytes) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			this.Rehydrate(rehydrator);
		}

		public virtual SafeArrayHandle Save() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return SafeArrayHandle.WrapAndOwn(dehydrator.ToReleasedArray());
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				foreach(var entry in this.nodes.Values) {
					entry.root?.Dispose();
					entry.backupRoot?.Dispose();
				}
			}

			this.IsDisposed = true;
		}

		~XMSSNodeCache() {
			this.Dispose(false);
		}

	#endregion

	}
}