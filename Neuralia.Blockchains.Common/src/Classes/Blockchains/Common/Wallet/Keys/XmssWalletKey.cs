using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys {
	public interface IXmssWalletKey : IWalletKey {
		int TreeHeight { get; set; }
		int KeyUseIndex { get; set; }
		int WarningHeight { get; set; }
		int ChangeHeight { get; set; }
		int MaximumHeight { get; set; }
		Enums.KeyHashBits HashBits { get; set; }
	}

	public class XmssWalletKey : WalletKey, IXmssWalletKey {

		/// <summary>
		///     the amount of bits used for hashing XMSS tree
		/// </summary>
		public Enums.KeyHashBits HashBits { get; set; } = Enums.KeyHashBits.SHA3_256;

		/// <summary>
		///     The current private key index
		/// </summary>
		public int KeyUseIndex { get; set; }

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
		public int TreeHeight { get; set; }

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TreeHeight);
			nodeList.Add(this.KeyUseIndex);
			nodeList.Add(this.WarningHeight);
			nodeList.Add(this.ChangeHeight);
			nodeList.Add(this.MaximumHeight);
			nodeList.Add((byte) this.HashBits);

			return nodeList;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Value = this.TreeHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.KeyUseIndex;
			entry.Dehydrate(dehydrator);

			entry.Value = this.WarningHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.ChangeHeight;
			entry.Dehydrate(dehydrator);

			entry.Value = this.MaximumHeight;
			entry.Dehydrate(dehydrator);

			dehydrator.Write((byte) this.HashBits);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 entry = new AdaptiveLong1_9();
			entry.Rehydrate(rehydrator);
			this.TreeHeight = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.KeyUseIndex = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.WarningHeight = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.ChangeHeight = (int) entry.Value;

			entry.Rehydrate(rehydrator);
			this.MaximumHeight = (int) entry.Value;

			this.HashBits = (Enums.KeyHashBits) rehydrator.ReadByte();
		}

		protected override void DisposeAll() {
			base.DisposeAll();

			// yes, its good to clear this. just in case
			this.KeyUseIndex = 0;
			this.MaximumHeight = 0;
		}
	}
}