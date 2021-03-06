﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSSMT.Keys {

	public class XMSSMTPrivateKey : XMSSMTKey {

		protected readonly XMSSExecutionContext XmssExecutionContext;

		public XMSSMTPrivateKey(XMSSExecutionContext xmssExecutionContext) {
			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;
			
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.NoncePart = new KeyNoncePart(0) {NodeCache = new XMSSMTNodeCache()};
			this.NoncePart.Nonces = new XMSSNonceSet(this.XmssExecutionContext.NoncesExponent);
			this.NoncePart.NodeCache = new XMSSMTNodeCache();
		}

		public XMSSMTPrivateKey(XMSSMTPrivateKey other, XMSSExecutionContext xmssExecutionContext, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.HeaderPart.Index = other.HeaderPart.Index;
			this.HeaderPart.Height = other.HeaderPart.Height;
			this.HeaderPart.Layers = other.HeaderPart.Layers;
			this.HeaderPart.LeafCount = other.HeaderPart.LeafCount;
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;
			
			this.HeaderPart.PublicSeed = other.HeaderPart.PublicSeed?.Clone();

			this.SecretPart.SecretPrf = other.SecretPart.SecretPrf?.Clone();
			this.SecretPart.Root = other.SecretPart.Root?.Clone();
			this.SecretPart.SecretSeed = other.SecretPart.SecretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(other.NoncePart.Nonces);
			this.NoncePart.NodeCache = new XMSSMTNodeCache(this.HeaderPart.Height, this.HeaderPart.Layers, xmssExecutionContext.DigestSize, xmssExecutionContext.BackupDigestSize, cacheMode, cacheLevels);
		}

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSMTPrivateKey(int heigth, int layer, ByteArray publicSeed, ByteArray secretSeed, ByteArray secretPrf, XMSSNonceSet nonces, XMSSExecutionContext xmssExecutionContext, long index = 0, ByteArray root = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.HeaderPart.LeafCount = 1 << (heigth / layer);
			this.HeaderPart.Index = index;
			this.HeaderPart.Height = (byte) heigth;
			this.HeaderPart.Layers = (byte) layer;
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;

			this.HeaderPart.PublicSeed = publicSeed?.Clone();

			this.SecretPart.SecretPrf = secretPrf?.Clone();
			this.SecretPart.Root = root?.Clone();
			this.SecretPart.SecretSeed = secretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(nonces);
			this.NoncePart.NodeCache = new XMSSMTNodeCache(this.HeaderPart.Height, this.HeaderPart.Layers, xmssExecutionContext.DigestSize, xmssExecutionContext.BackupDigestSize, cacheMode, cacheLevels);
		}

		// versioning information

		public byte Height {
			get => this.HeaderPart.Height;
			set => this.HeaderPart.Height = value;
		}

		public byte Layers {
			get => this.HeaderPart.Layers;
			set => this.HeaderPart.Layers = value;
		}

		public int LeafCount {
			get => this.HeaderPart.LeafCount;
			set => this.HeaderPart.LeafCount = value;
		}
		
		public Enums.KeyHashType HashType {
			get => this.HeaderPart.HashType;
			set => this.HeaderPart.HashType = value;
		}
		
		public Enums.KeyHashType BackupHashType {
			get => this.HeaderPart.BackupHashType;
			set => this.HeaderPart.BackupHashType = value;
		}
		
		public byte NoncesExponent {
			get => this.HeaderPart.NoncesExponent;
			set => this.HeaderPart.NoncesExponent = value;
		}

		/// <summary>
		///     private key index to use (0 based)
		/// </summary>
		public long Index {
			get => this.HeaderPart.Index;
			set => this.HeaderPart.Index = value;
		}

		public ByteArray PublicSeed {
			get => this.HeaderPart.PublicSeed;
			set => this.HeaderPart.PublicSeed = value;
		}

		/// <summary>
		///     private key index to use (1 based)
		/// </summary>
		public long IndexOne => this.Index + 1;

		public ByteArray SecretSeed {
			get => this.SecretPart.SecretSeed;
			set => this.SecretPart.SecretSeed = value;
		}

		public ByteArray Root {
			get => this.SecretPart.Root;
			set => this.SecretPart.Root = value;
		}
		
		public ByteArray BackupRoot {
			get => this.SecretPart.BackupRoot;
			set => this.SecretPart.BackupRoot = value;
		}

		public ByteArray SecretPrf {
			get => this.SecretPart.SecretPrf;
			set => this.SecretPart.SecretPrf = value;
		}

		public XMSSNonceSet Nonces {
			get => this.NoncePart.Nonces;
			set => this.NoncePart.Nonces = value;
		}

		public XMSSMTNodeCache NodeCache {
			get => this.NoncePart.NodeCache;
			set => this.NoncePart.NodeCache = value;
		}

		public KeyHeaderPart HeaderPart { get; set; }
		public KeySecretPart SecretPart { get; set; }
		public KeyNoncePart NoncePart { get; set; }

		protected override void DisposeAll() {
			base.DisposeAll();

			this.HeaderPart.Dispose();
			this.SecretPart.Dispose();
			this.NoncePart.Dispose();
		}

		public override void LoadKey(SafeArrayHandle keyBytes) {

			BrotliCompression compression = new BrotliCompression();
			using SafeArrayHandle deflatedPrivateKey = compression.Decompress(keyBytes);

			base.LoadKey(deflatedPrivateKey);
		}

		public override SafeArrayHandle SaveKey() {
			using SafeArrayHandle privateKey = base.SaveKey();

			BrotliCompression compression = new BrotliCompression();
			return compression.Compress(privateKey);
		}

		protected override void Rehydrate(IDataRehydrator rehydrator) {

			this.RehydrateParts(rehydrator, rehydrator, rehydrator);
		}

		protected override void Dehydrate(IDataDehydrator dehydrator) {

			this.DehydrateParts(dehydrator, dehydrator, dehydrator);
		}

		public void RehydrateParts(IDataRehydrator rehydratorHeader, IDataRehydrator rehydratorSecrets, IDataRehydrator rehydratorNonces) {
			if(rehydratorHeader != null) {
				this.HeaderPart.Rehydrate(rehydratorHeader);

				base.Rehydrate(rehydratorHeader);
			}

			if(rehydratorSecrets != null) {
				this.SecretPart.Rehydrate(rehydratorSecrets);
			}

			if(rehydratorNonces != null) {
				this.NoncePart.LeafCount = this.HeaderPart.LeafCount;
				this.NoncePart.Rehydrate(rehydratorNonces);
			}
		}

		public void DehydrateParts(IDataDehydrator dehydratorHeader, IDataDehydrator dehydratorSecrets, IDataDehydrator dehydratorNonces) {
			if(dehydratorHeader != null) {
				this.HeaderPart.Dehydrate(dehydratorHeader);

				base.Dehydrate(dehydratorHeader);
			}

			if(dehydratorSecrets != null) {
				this.SecretPart.Dehydrate(dehydratorSecrets);
			}

			if(dehydratorNonces != null) {
				this.NoncePart.Dehydrate(dehydratorNonces);
			}
		}

		public void IncrementIndex(XMSSMTEngine engine) {
			engine.CleanAuthTree(this);

			this.HeaderPart.Index += 1;
		}

		
		/// <summary>
		/// export the key to a minimal size text string for physical archiving
		/// </summary>
		/// <returns></returns>
		public string ExportKey() {

			var key = new {
				this.HeaderPart.Major, this.HeaderPart.Minor, this.HeaderPart.Height, this.HeaderPart.Layers, this.HeaderPart.NoncesExponent, this.HeaderPart.HashType, this.HeaderPart.BackupHashType,
				
				PublicSeed = this.HeaderPart.PublicSeed.ToBase32(),
				
				Root = this.SecretPart.Root.ToBase32(),
				BackupRoot = this.SecretPart.BackupRoot.ToBase32(),
				SecretPrf = this.SecretPart.SecretPrf.ToBase32(),
				SecretSeed = this.SecretPart.SecretSeed.ToBase32(),
				
				Nonces = new {
					this.NoncePart.Nonces.Major, this.NoncePart.Nonces.Minor,
					Nonces = this.NoncePart.Nonces.Nonces.OrderBy(n => n.Key).Select(n => new {n.Key, Nonce1 = n.Value.nonce1, Nonce2 = n.Value.nonce2}).ToList()
				},
			};

			var keyString = JsonConvert.SerializeObject(key);

			using var hash = HashingUtils.HashSha256(hasher => {

				using var parts = (SafeArrayHandle) Encoding.Unicode.GetBytes(keyString);
				return hasher.Hash(parts);
			});
			
			return $"XMSS^MT:::{keyString}:::{hash.ToBase32()}";
		}
		
		public class KeyHeaderPart : IBinarySerializable, IDisposableExtended {

			public readonly byte Major = 1;
			public readonly byte Minor = 0;

			public KeyHeaderPart(int digestSize) {
				this.DigestSize = digestSize;
			}

			private int DigestSize { get; }

			public byte Height { get; set; }

			public byte Layers { get; set; }
			
			public byte NoncesExponent { get; set; }

			public int LeafCount { get; set; }

			public Enums.KeyHashType HashType { get; set; } = Enums.KeyHashType.SHA2_256;
			public Enums.KeyHashType BackupHashType { get; set; } = Enums.KeyHashType.SHA3_256;

			/// <summary>
			///     private key index to use (0 based)
			/// </summary>
			public long Index { get; set; }

			/// <summary>
			///     private key index to use (1 based)
			/// </summary>
			public long IndexOne => this.Index + 1;

			public ByteArray PublicSeed { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {

				int major = rehydrator.ReadByte();
				int minor = rehydrator.ReadByte();

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Rehydrate(rehydrator);
				this.Index = (int) adaptiveLong.Value;
				this.Height = rehydrator.ReadByte();
				this.Layers = rehydrator.ReadByte();
				adaptiveLong.Rehydrate(rehydrator);
				this.LeafCount = (int) adaptiveLong.Value;
				this.HashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();
				this.BackupHashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();
				this.NoncesExponent = rehydrator.ReadByte();
				
				this.PublicSeed = rehydrator.ReadArray(this.DigestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.Major);
				dehydrator.Write(this.Minor);

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Value = this.Index;
				adaptiveLong.Dehydrate(dehydrator);

				dehydrator.Write(this.Height);
				dehydrator.Write(this.Layers);

				adaptiveLong.Value = this.LeafCount;
				adaptiveLong.Dehydrate(dehydrator);
				
				dehydrator.Write((byte)this.HashType);
				dehydrator.Write((byte)this.BackupHashType);
				dehydrator.Write(this.NoncesExponent);

				dehydrator.WriteRawArray(this.PublicSeed);
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

			protected virtual void DisposeAll() {
				this.PublicSeed?.Dispose();
			}

			~KeyHeaderPart() {
				this.Dispose(false);
			}

		#endregion
		}

		public class KeySecretPart : IBinarySerializable, IDisposableExtended {

			public KeySecretPart(int digestSize, int backupDigestSize) {
				this.DigestSize = digestSize;
				this.BackupDigestSize = backupDigestSize;
			}

			private int DigestSize { get; }
			private int BackupDigestSize { get; }
			
			public ByteArray SecretSeed { get; set; }
			public ByteArray Root { get; set; }
			public ByteArray BackupRoot { get; set; }
			public ByteArray SecretPrf { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				this.SecretSeed = rehydrator.ReadArray();
				this.SecretPrf = rehydrator.ReadArray();
				this.Root = rehydrator.ReadArray(this.DigestSize);
				this.BackupRoot = rehydrator.ReadArray(this.BackupDigestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.SecretSeed);
				dehydrator.Write(this.SecretPrf);
				dehydrator.WriteRawArray(this.Root);
				dehydrator.WriteRawArray(this.BackupRoot);
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

			protected virtual void DisposeAll() {
				this.Root?.Dispose();
				this.BackupRoot?.Dispose();
				this.SecretPrf?.Dispose();
				this.SecretSeed?.Dispose();
			}

			~KeySecretPart() {
				this.Dispose(false);
			}

		#endregion

		}

		public class KeyNoncePart : IBinarySerializable, IDisposableExtended {
			public KeyNoncePart(int leafCount) {
				this.LeafCount = leafCount;
			}

			public int LeafCount { get; set; }

			public XMSSNonceSet Nonces { get; set; }
			public XMSSMTNodeCache NodeCache { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				this.NodeCache.Rehydrate(rehydrator);

				this.Nonces.Rehydrate(rehydrator, this.LeafCount);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				this.NodeCache.Dehydrate(dehydrator);

				this.Nonces.Dehydrate(dehydrator, this.LeafCount);
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

			protected virtual void DisposeAll() {
				this.NodeCache?.Dispose();
			}

			~KeyNoncePart() {
				this.Dispose(false);
			}

		#endregion
		}
	}
}