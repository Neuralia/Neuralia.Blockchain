using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	public class XMSSNodeCache : IDisposableExtended, IBinarySerializable {

		public enum XMSSCacheModes:byte {
			Heuristic= 1,
			AllButFirst= 2,
			All = 3
		}
		public const int LEVELS_TO_CACHE_ABSOLUTELY = 5;

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		private readonly ConcurrentDictionary<XMSSNodeId, (ByteArray root, ByteArray backupRoot)> nodes = new ConcurrentDictionary<XMSSNodeId, (ByteArray root, ByteArray backupRoot)>();

		public XMSSNodeCache() {

		}

		public XMSSNodeCache(int height, int digestSize, int backupDigestSize, XMSSCacheModes cacheMode = XMSSCacheModes.Heuristic) {
			this.Height = (byte) height;
			this.DigestSize = (byte) digestSize;
			this.BackupDigestSize = (byte) backupDigestSize;
			this.CacheMode = cacheMode;
		}

		public bool IsChanged { get; private set; }
		public byte Height { get; set; }
		public byte DigestSize { get; private set; }
		public byte BackupDigestSize { get; private set; }
		public XMSSCacheModes CacheMode { get; set; }

		public (ByteArray root, ByteArray backupRoot) this[XMSSNodeId id] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.nodes.ContainsKey(id) ? (null, null) : (this.nodes[id].root.Clone(), this.nodes[id].backupRoot.Clone());
		}

		public (ByteArray root, ByteArray backupRoot) GetOriginals(XMSSNodeId id) {
			return !this.nodes.ContainsKey(id) ? (null, null) : (this.nodes[id].root, this.nodes[id].backupRoot);
		}
		
		public List<XMSSNodeId> NodeIds => this.nodes.Keys.ToList();

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			dehydrator.Write(this.Height);
			dehydrator.Write(this.DigestSize);
			dehydrator.Write(this.BackupDigestSize);
			
			dehydrator.Write((byte)this.CacheMode);
			
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
			this.CacheMode = (XMSSCacheModes)rehydrator.ReadByte();
			
			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			int count = (int)adaptiveLong.Value;

			this.nodes.Clear();

			for(int i = 0; i < count; i++) {

				adaptiveLong.Rehydrate(rehydrator);
				int index = (int) adaptiveLong.Value;
				int height = rehydrator.ReadByte();

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
			bool cache = false;

			if(force || this.CacheMode == XMSSCacheModes.All) {
				cache = true;
			}
			else if(this.CacheMode == XMSSCacheModes.AllButFirst) {
				cache = id.Height != 0;
			}
			else if(this.CacheMode == XMSSCacheModes.Heuristic) {
				cache = id.Height >= (this.Height - LEVELS_TO_CACHE_ABSOLUTELY);
			}

			//TODO; add heuristics and more sophisticated logics
			if(cache) {
				this.IsChanged = true;
				this.nodes.AddSafe(id, (node.root.Clone(), node.backupRoot?.Clone()));
			}
		}

		public void Clear() {
			this.nodes.Clear();
		}

		public void ClearNodes(List<XMSSNodeId> excludeNodes) {
			foreach(XMSSNodeId id in excludeNodes) {
				if(this.nodes.ContainsKey(id)) {
					
					this.nodes[id].root?.Return();
					this.nodes[id].backupRoot?.Return();
					
					this.nodes.RemoveSafe(id);
					this.IsChanged = true;
				}
			}
		}

		public virtual void Load(ByteArray publicKey) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(publicKey);

			this.Rehydrate(rehydrator);
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