using System.Collections.Generic;
using System.Text;
using LiteDB;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssWalletKey : IXmssKey, IWalletKey {
		long WarningHeight { get; set; }
		long ChangeHeight { get; set; }
		long MaximumHeight { get; set; }
		SafeArrayHandle NextKeyNodeCache { get; set; }
		string ExportKey();
	}

	public class XmssWalletKey : WalletKey, IXmssWalletKey {
		[BsonIgnore]
		private SafeArrayHandle nextKeyNodeCache = SafeArrayHandle.Create();

		protected override ComponentVersion<CryptographicKeyType> SetIdentity() {
			return (CryptographicKeyTypes.Instance.XMSS, 1,0);
		}
		
		/// <summary>
		///     the amount of bits used for hashing XMSS tree
		/// </summary>
		public Enums.KeyHashType HashType { get; set; } = Enums.KeyHashType.SHA3_256;
		
		public Enums.KeyHashType BackupHashType { get; set; } = Enums.KeyHashType.SHA2_256;
		
		/// <summary>
		///     the amount of keys allowed before we should think about changing our key
		/// </summary>
		public long WarningHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public long ChangeHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public long MaximumHeight { get; set; }

		/// <summary>
		///     xmss tree height
		/// </summary>
		public byte TreeHeight { get; set; }

		/// <summary>
		/// the exponent to define the group size by which to apply the nonce
		/// </summary>
		public byte NoncesExponent { get; set; }

		/// <summary>
		/// we might have precomputed the nodes for the next key use. This is where we store it if this is the case.
		/// </summary>
		public SafeArrayHandle NextKeyNodeCache {
			get => this.nextKeyNodeCache;
			set {
				this.nextKeyNodeCache?.Dispose();
				this.nextKeyNodeCache = value;
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeHeight);
			nodeList.Add(this.WarningHeight);
			nodeList.Add(this.ChangeHeight);
			nodeList.Add(this.MaximumHeight);
			nodeList.Add((byte) this.HashType);
			nodeList.Add((byte) this.BackupHashType);
			nodeList.Add(this.NextKeyNodeCache);
			
			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			
			dehydrator.Write(this.TreeHeight);
			
			dehydrator.Write(this.NoncesExponent);
			
			entry.Value = this.WarningHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.ChangeHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.MaximumHeight;
			entry.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.HashType);
			
			dehydrator.Write((byte) this.BackupHashType);
			
			dehydrator.Write(this.NextKeyNodeCache);
		}


		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();

			this.TreeHeight = rehydrator.ReadByte();
			
			this.NoncesExponent = rehydrator.ReadByte();
			
			entry.Rehydrate(rehydrator);
			this.WarningHeight = entry.Value;

			entry.Rehydrate(rehydrator);
			this.ChangeHeight = entry.Value;

			entry.Rehydrate(rehydrator);
			this.MaximumHeight = entry.Value;

			this.HashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();
			this.BackupHashType = rehydrator.ReadByteEnum<Enums.KeyHashType>();

			this.NextKeyNodeCache.Entry = rehydrator.ReadNullEmptyArray();
		}

		protected override void SetFromWalletKey(IWalletKey other) {
			base.SetFromWalletKey(other);

			if(other is IXmssWalletKey xmssWalletKey) {
				this.NextKeyNodeCache.Entry = xmssWalletKey.NextKeyNodeCache.Entry;
				this.WarningHeight = xmssWalletKey.WarningHeight;
				this.ChangeHeight = xmssWalletKey.ChangeHeight;
				this.MaximumHeight = xmssWalletKey.MaximumHeight;
			}
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			// yes, its good to clear this. just in case
			this.MaximumHeight = 0;
			
			this.NextKeyNodeCache?.Dispose();
		}

		public string ExportKey() {

			var key = new {
				Version = this.Version.ToString(), 
				this.Name,
				this.Ordinal, 
				this.AccountCode,
				this.Hash, 
				this.CreatedTime,
				this.WarningHeight,
				this.ChangeHeight,
				this.MaximumHeight,
				PublicKey = this.PublicKey.ToBase64(),
				PrivateKey = this.ExportPrivateKey(),
				Extras = this.ExportExtrasKey()
			};

			string keyString = JsonConvert.SerializeObject(key);
			
			using var hash = HashingUtils.HashSha256(hasher => {

				using var parts = (SafeArrayHandle) Encoding.Unicode.GetBytes(keyString);
				return hasher.Hash(parts);
			});
			
			return $"{this.KeyExportName}:::{keyString}:::{hash.ToBase64()}";
		}

		protected virtual string KeyExportName => "XMSS_WALLET";
		protected virtual string ExportPrivateKey() {
			using(XMSSProvider provider = new XMSSProvider(this.HashType, this.BackupHashType, this.TreeHeight, Enums.ThreadMode.Single, this.NoncesExponent)) {
				provider.Initialize();
				var privateKey = provider.LoadPrivateKey(this.PrivateKey);

				return privateKey.ExportKey();
			}
		}

		protected virtual object ExportExtrasKey() {
			return null;
		}
	}
}