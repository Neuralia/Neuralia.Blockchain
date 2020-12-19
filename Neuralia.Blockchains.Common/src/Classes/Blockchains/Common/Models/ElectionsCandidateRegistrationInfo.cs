using System;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Models {
	/// <summary>
	///     Everything required to inform the validators of our mining coordinates
	/// </summary>
	public class ElectionsCandidateRegistrationInfo {

		public AccountId AccountId { get; set; } = new AccountId();

		public AccountId DelegateAccountId { get; set; }

		/// <summary>
		///     Which chain are we mining from
		/// </summary>
		public BlockchainType ChainType { get; set; }

		/// <summary>
		///     The IP of the miner
		/// </summary>
		public Guid Ip { get; set; }

		/// <summary>
		///     The IP of the miner
		/// </summary>
		public int Port { get; set; }
		
		public int ValidatorPort { get; set; }

		/// <summary>
		///     The password to be offered up to begin a confirmation exchange
		/// </summary>
		public Guid Password { get; set; }

		/// <summary>
		/// The accompanying secret code
		/// </summary>
		public Guid SecretCode { get; set; }

		public Enums.MiningTiers MiningTier { get; set; } = MiningTierUtils.DefaultTier;

		public byte[] Autograph { get; set; }

		/// <summary>
		///     When was this created
		/// </summary>
		public DateTime Timestamp { get; set; }

		public virtual SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.AccountId.Dehydrate(dehydrator);

			dehydrator.Write(this.DelegateAccountId);

			dehydrator.Write(this.ChainType.Value);
			dehydrator.Write(this.Ip);
			dehydrator.Write(this.Port);
			dehydrator.Write(this.ValidatorPort);
			dehydrator.Write(this.Password);
			dehydrator.Write(this.SecretCode);
			dehydrator.Write(this.Timestamp);
			dehydrator.Write((byte) this.MiningTier);

			return dehydrator.ToArray();
		}

		public virtual void Rehydrate(SafeArrayHandle data) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.AccountId.Rehydrate(rehydrator);
			this.DelegateAccountId = rehydrator.ReadRehydratable<AccountId>();

			this.ChainType     = rehydrator.ReadUShort();
			this.Ip            = rehydrator.ReadGuid();
			this.Port          = rehydrator.ReadInt();
			this.ValidatorPort = rehydrator.ReadInt();
			this.Password      = rehydrator.ReadGuid();
			this.SecretCode = rehydrator.ReadGuid();
			this.Timestamp     = rehydrator.ReadDateTime();
			this.MiningTier    = rehydrator.ReadByteEnum<Enums.MiningTiers>();
		}
	}
}