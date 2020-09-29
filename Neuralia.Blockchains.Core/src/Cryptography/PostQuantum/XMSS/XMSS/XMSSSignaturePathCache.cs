using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.WOTS;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS {
	
	/// <summary>
	/// this class is used to optimize and snapshot the state of an index in the auth path tree, so we can cache the required nodes and load only the ones required.
	/// </summary>
	public class XMSSSignaturePathCache : IDisposableExtended {

		public readonly byte Major = 1;
		public readonly byte Minor = 0;
		public int TreeHeight { get; private set; }
		public int Index { get; private set; }
		public int DigestSize { get; private set; }

		private SignatureCacheEntry[] cache;

		public XMSSSignaturePathCache() {

		}

		public XMSSSignaturePathCache(byte treeHeight, XMSSExecutionContext XmssExecutionContext) {
			this.TreeHeight = treeHeight;
			this.cache = new SignatureCacheEntry[treeHeight - 1];

			this.DigestSize = XmssExecutionContext.DigestSize;
		}

		public void Load(ByteArray bytes) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

			this.Rehydrate(rehydrator);
		}

		protected virtual void Rehydrate(IDataRehydrator rehydrator) {

			int major = rehydrator.ReadByte();
			int minor = rehydrator.ReadByte();

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Rehydrate(rehydrator);
			this.TreeHeight = (int) adaptiveLong.Value;

			adaptiveLong.Rehydrate(rehydrator);
			this.DigestSize = (int) adaptiveLong.Value;

			adaptiveLong.Rehydrate(rehydrator);
			this.Index = (int) adaptiveLong.Value;

			this.cache = new SignatureCacheEntry[this.TreeHeight - 1];

			for(int i = 0; i < this.cache.Length; i++) {
				bool isNull = rehydrator.ReadBool();

				if(!isNull) {
					SignatureCacheEntry cacheEntry = new SignatureCacheEntry();

					cacheEntry.Rehydrate(rehydrator, this.DigestSize);

					this.cache[i] = cacheEntry;
				}
			}
		}

		public ByteArray Save() {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToReleasedArray();
		}

		protected virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);

			AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
			adaptiveLong.Value = this.TreeHeight;
			adaptiveLong.Dehydrate(dehydrator);

			adaptiveLong.Value = this.DigestSize;
			adaptiveLong.Dehydrate(dehydrator);

			adaptiveLong.Value = this.Index;
			adaptiveLong.Dehydrate(dehydrator);

			for(int i = 0; i < this.cache.Length; i++) {
				bool isNull = this.cache[i] == null;
				dehydrator.Write(isNull);

				if(!isNull) {
					this.cache[i].Dehydrate(dehydrator);
				}
			}
		}

		public bool ContainsTreeIndex(int height, int index) {
			if(height == 0 || height == this.TreeHeight) {
				// we never cache the first tree height or the root
				return false;
			}

			var entry = this.cache[height - 1];

			return entry?.Index == index;
		}

		public void AdjustTreeSignAuth(XMSSSignature.XMSSTreeSignature treeSignature, ImmutableList<XMSSNodeId> authNodeList) {

			for(int i = 1; i < treeSignature.Auth.Length; i++) {
				if(this.ContainsTreeIndex(i, authNodeList[i].Index)) {
					// reset the array
					treeSignature.Auth[i] = null;
					treeSignature.BackupAuth[i] = null;
				}
			}
		}

		public void UpdateFromSignature(XMSSSignature signature, ImmutableList<XMSSNodeId> indices) {

			for(int i = 1; i < signature.XmssTreeSignature.Auth.Length; i++) {
				int cacheIndex = i - 1;

				if(signature.XmssTreeSignature.Auth[i] != null) {
					this.cache[cacheIndex]?.Dispose();
					this.cache[cacheIndex] = new SignatureCacheEntry() {Index = indices[i].Index, Auth = signature.XmssTreeSignature.Auth[i].Clone(), BackupAuth = signature.XmssTreeSignature.BackupAuth[i].Clone()};
				}
			}

			this.Index = signature.Index;
			signature.Optimized = true;
		}

		public void RestoreSignature(XMSSSignature signature) {
			if(signature.XmssTreeSignature.Auth.Length != this.TreeHeight) {
				throw new ApplicationException("Invalid tree height.");
			}

			if(this.Index != 0 && this.Index != (signature.Index - 1)) {
				throw new ApplicationException("Invalid signature index.");
			}

			for(int i = 0; i < this.TreeHeight; i++) {
				if(signature.XmssTreeSignature.Auth[i] == null) {
					signature.XmssTreeSignature.Auth[i] = this.cache[i - 1].Auth;
					signature.XmssTreeSignature.BackupAuth[i] = this.cache[i - 1].BackupAuth;
				}
			}
		}

	#region disposable

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {
				this.DisposeAll();
			}

			this.IsDisposed = true;
		}

		~XMSSSignaturePathCache() {
			this.Dispose(false);
		}

		protected virtual void DisposeAll() {
			foreach(var entry in this.cache) {
				entry?.Dispose();
			}
		}

	#endregion

		public class SignatureCacheEntry : IDisposableExtended {
			public int Index { get; set; }
			public ByteArray Auth { get; set; }
			public ByteArray BackupAuth { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator, int digestSize) {

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				this.Index = (int) adaptiveLong.Value;

				this.Auth = rehydrator.ReadArray(digestSize);
				this.BackupAuth = rehydrator.ReadArray(digestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Value = this.Index;
				adaptiveLong.Dehydrate(dehydrator);

				dehydrator.WriteRawArray(this.Auth);
				dehydrator.WriteRawArray(this.BackupAuth);
			}

		#region disposable

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {
					this.DisposeAll();
				}

				this.IsDisposed = true;
			}

			~SignatureCacheEntry() {
				this.Dispose(false);
			}

			protected virtual void DisposeAll() {
				this.Auth?.Dispose();
				this.Auth = null;
				this.BackupAuth?.Dispose();
				this.BackupAuth = null;
			}

		#endregion

		}
	}
}