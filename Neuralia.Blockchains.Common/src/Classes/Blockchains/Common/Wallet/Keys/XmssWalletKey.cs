using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssWalletKey : IXmssKey, IWalletKey {
		int WarningHeight { get; set; }
		int ChangeHeight { get; set; }
		int MaximumHeight { get; set; }
	}

	public class XmssWalletKey : WalletKey, IXmssWalletKey {

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
		public int WarningHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public int ChangeHeight { get; set; }

		/// <summary>
		///     maximum amount of keys allowed before we must change our key
		/// </summary>
		public int MaximumHeight { get; set; }

		/// <summary>
		///     xmss tree height
		/// </summary>
		public byte TreeHeight { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeHeight);
			nodeList.Add(this.WarningHeight);
			nodeList.Add(this.ChangeHeight);
			nodeList.Add(this.MaximumHeight);
			nodeList.Add((byte) this.HashType);
			nodeList.Add((byte) this.BackupHashType);
			
			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			
			dehydrator.Write(this.TreeHeight);
			
			entry.Value = this.WarningHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.ChangeHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.MaximumHeight;
			entry.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.HashType);
			
			dehydrator.Write((byte) this.BackupHashType);
		}


		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();

			this.TreeHeight = rehydrator.ReadByte();
			
			entry.Rehydrate(rehydrator);
			this.WarningHeight = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.ChangeHeight = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.MaximumHeight = (int) entry.Value;

			this.HashType = (Enums.KeyHashType) rehydrator.ReadByte();
			this.BackupHashType = (Enums.KeyHashType) rehydrator.ReadByte();
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			// yes, its good to clear this. just in case
			this.MaximumHeight = 0;
		}
	}
}