using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	public class XMSSNodeCache : IDisposableExtended, IBinarySerializable {

		public const int LEVELS_TO_CACHE_ABSOLUTELY = 5;

		// versioning information
		public readonly byte Major = 1;
		public readonly byte Minor = 0;

		private readonly ConcurrentDictionary<XMSSNodeId, ByteArray> nodes = new ConcurrentDictionary<XMSSNodeId, ByteArray>();
		public readonly byte Revision = 0;

		public XMSSNodeCache() {

		}

		public XMSSNodeCache(int height, int digestSize) {
			this.Height = (byte) height;
			this.DigestSize = (byte) digestSize;
		}

		public bool IsChanged { get; private set; }
		public byte Height { get; private set; }
		public byte DigestSize { get; private set; }

		public ByteArray this[XMSSNodeId id] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.nodes.ContainsKey(id) ? null : this.nodes[id].Clone();

		}

		public List<XMSSNodeId> NodeIds => this.nodes.Keys.ToList();

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);

			dehydrator.Write(this.Height);
			dehydrator.Write(this.DigestSize);

			dehydrator.Write((ushort) this.nodes.Count);

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();

			foreach(var node in this.nodes) {

				adaptiveLong.Value = node.Key.Index;
				adaptiveLong.Dehydrate(dehydrator);
				dehydrator.Write((byte) node.Key.Height);

				dehydrator.WriteRawArray(node.Value);
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();
			int revision = rehydrator.ReadByte();

			this.Height = rehydrator.ReadByte();
			this.DigestSize = rehydrator.ReadByte();

			ushort count = rehydrator.ReadUShort();

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			this.nodes.Clear();

			for(int i = 0; i < count; i++) {

				adaptiveLong.Rehydrate(rehydrator);
				int index = (int) adaptiveLong.Value;
				int height = rehydrator.ReadByte();

				ByteArray buffer = rehydrator.ReadArray(this.DigestSize);

				this.nodes.AddSafe((index, height), buffer);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Cache(XMSSNodeId id, ByteArray node) {
			if(this.nodes.ContainsKey(id)) {
				return;
			}

			// if this is true, we cache no matter what
			bool forceCache = id.Height >= (this.Height - 1 - LEVELS_TO_CACHE_ABSOLUTELY);

			//TODO; add heuristics and more sophisticated logics
			if(forceCache) {
				this.IsChanged = true;
				this.nodes.AddSafe(id, node.Clone());
			}
		}

		public void ClearNodes(List<XMSSNodeId> excludeNodes) {
			foreach(XMSSNodeId id in excludeNodes) {
				if(this.nodes.ContainsKey(id)) {
					this.nodes[id]?.Return();
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
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray().Release();
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				foreach(ByteArray entry in this.nodes.Values) {
					entry?.Dispose();
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