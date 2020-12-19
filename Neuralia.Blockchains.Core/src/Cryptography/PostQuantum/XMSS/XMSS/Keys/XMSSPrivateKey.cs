using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Utils;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS.Keys {

	public class XMSSPrivateKey : XMSSKey {

		protected readonly XMSSExecutionContext XmssExecutionContext;

		public XMSSPrivateKey(XMSSExecutionContext xmssExecutionContext) {
			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(this.XmssExecutionContext.NoncesExponent);
			this.NoncePart.NodeCache = new XMSSNodeCache(this.HeaderPart.Height, xmssExecutionContext.DigestSize, xmssExecutionContext.BackupDigestSize);
		}

		public XMSSPrivateKey(XMSSPrivateKey other, XMSSExecutionContext xmssExecutionContext) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.HeaderPart.Index = other.HeaderPart.Index;
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.Height = other.HeaderPart.Height;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;

			this.HeaderPart.PublicSeed = other.HeaderPart.PublicSeed?.Clone();

			this.SecretPart.SecretPrf = other.SecretPart.SecretPrf?.Clone();
			this.SecretPart.Root = other.SecretPart.Root?.Clone();
			this.SecretPart.SecretSeed = other.SecretPart.SecretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(other.NoncePart.Nonces);
			this.NoncePart.NodeCache = new XMSSNodeCache(this.HeaderPart.Height, xmssExecutionContext.DigestSize, xmssExecutionContext.BackupDigestSize);
		}

		/// <summary>
		///     Instantiate a new XMSS Private Key
		/// </summary>
		/// <param name="heigth">Height (number of levels - 1) of the tree</param>
		public XMSSPrivateKey(int heigth, ByteArray publicSeed, ByteArray secretSeed, ByteArray secretPrf, XMSSNonceSet nonces, XMSSExecutionContext xmssExecutionContext, XMSSNodeCache xmssNodeCache = null, int index = 0, ByteArray root = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY) {

			this.XmssExecutionContext = xmssExecutionContext;

			this.HeaderPart = new KeyHeaderPart(this.XmssExecutionContext.DigestSize);
			this.SecretPart = new KeySecretPart(this.XmssExecutionContext.DigestSize, this.XmssExecutionContext.BackupDigestSize);

			this.HeaderPart.Index = index;
			this.HeaderPart.HashType = this.XmssExecutionContext.HashType;
			this.HeaderPart.BackupHashType = this.XmssExecutionContext.BackupHashType;
			this.HeaderPart.Height = (byte) heigth;
			this.HeaderPart.NoncesExponent = this.XmssExecutionContext.NoncesExponent;

			this.HeaderPart.PublicSeed = publicSeed?.Clone();

			this.SecretPart.SecretPrf = secretPrf?.Clone();
			this.SecretPart.Root = root?.Clone();
			this.SecretPart.SecretSeed = secretSeed?.Clone();

			this.NoncePart = new KeyNoncePart(this.HeaderPart.LeafCount);
			this.NoncePart.Nonces = new XMSSNonceSet(nonces);
			this.NoncePart.NodeCache = xmssNodeCache ?? new XMSSNodeCache(this.Height, xmssExecutionContext.DigestSize, xmssExecutionContext.BackupDigestSize, cacheMode, cacheLevels);
		}

		// versioning information

		public byte Height {
			get => this.HeaderPart.Height;
			set => this.HeaderPart.Height = value;
		}

		public long LeafCount => this.HeaderPart.LeafCount;

		/// <summary>
		///     private key index to use (0 based)
		/// </summary>
		public long Index {
			get => this.HeaderPart.Index;
			set => this.HeaderPart.Index = value;
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

		public XMSSNodeCache NodeCache {
			get => this.NoncePart.NodeCache;
			set => this.NoncePart.NodeCache = value;
		}

		public KeyHeaderPart HeaderPart { get; set; }
		public KeySecretPart SecretPart { get; set; }
		public KeyNoncePart NoncePart { get; set; }

		public short Nonce1 => this.Nonces[this.Index].nonce1;
		public short Nonce2 => this.Nonces[this.Index].nonce2;

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

		public override void Rehydrate(IDataRehydrator rehydrator) {

			this.RehydrateParts(rehydrator, rehydrator, rehydrator);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

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
			} else {
				this.NoncePart.NodeCache.Height = this.Height;
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

		public void ClearNodeCache() {
			this.NodeCache = null;
		}

		public void IncrementIndex(XMSSEngine engine) {
			engine.CleanAuthTree(this);

			this.HeaderPart.Index += 1;
		}

		public void SetIndex(int index) {
			this.HeaderPart.Index = index;
		}

		public class KeyHeaderPart : IBinarySerializable, IDisposableExtended {

			public readonly byte Major = 1;
			public readonly byte Minor = 0;

			public KeyHeaderPart(int digestSize) {
				this.DigestSize = digestSize;
			}

			private int DigestSize { get; }

			public byte Height { get; set; }
			public byte NoncesExponent { get; set; }
			
			public long LeafCount => 1L << this.Height;

			/// <summary>
			///     private key index to use (0 based)
			/// </summary>
			public long Index { get; set; }

			public Enums.KeyHashType HashType { get; set; } = Enums.KeyHashType.SHA2_256;
			public Enums.KeyHashType BackupHashType { get; set; } = Enums.KeyHashType.SHA3_256;

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

				this.HashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();
				this.BackupHashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();
				
				this.Height = rehydrator.ReadByte();
				this.NoncesExponent = rehydrator.ReadByte();

				this.PublicSeed = rehydrator.ReadArray(this.DigestSize);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.Major);
				dehydrator.Write(this.Minor);

				AdaptiveLong1_9 adaptiveLong = new AdaptiveLong1_9();
				adaptiveLong.Value = this.Index;
				adaptiveLong.Dehydrate(dehydrator);

				dehydrator.Write((byte)this.HashType);
				dehydrator.Write((byte)this.BackupHashType);
				
				dehydrator.Write(this.Height);
				
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
			public KeyNoncePart(long leafCount) {
				this.LeafCount = leafCount;
			}

			public long LeafCount { get; set; }

			public XMSSNonceSet Nonces { get; set; }
			public XMSSNodeCache NodeCache { get; set; }

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
		
		/// <summary>
		/// export the key to a minimal size text string for physical archiving
		/// </summary>
		/// <returns></returns>
		public string ExportKey() {

			var key = new {
				this.HeaderPart.Major, this.HeaderPart.Minor, this.HeaderPart.Height, this.HeaderPart.NoncesExponent, HashType = this.HeaderPart.HashType.ToString(), BackupHashType = this.HeaderPart.BackupHashType.ToString(),
				
				PublicSeed = this.HeaderPart.PublicSeed.ToBase64(),
				
				Root = this.SecretPart.Root.ToBase64(),
				BackupRoot = this.SecretPart.BackupRoot.ToBase64(),
				SecretPrf = this.SecretPart.SecretPrf.ToBase64(),
				SecretSeed = this.SecretPart.SecretSeed.ToBase64(),
				
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
			
			return $"XMSS:::{keyString}:::{hash.ToBase64()}";
		}
	}

}