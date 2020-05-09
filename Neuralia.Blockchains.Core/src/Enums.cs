using System;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Core {
	public static class Enums {

		public enum AccountTypes : byte {
			Standard = 1,
			Joint = 2,
			Presentation = AccountId.MAX_ACCOUNT_TYPE
		}

		public enum BlockchainEventTypes : byte {
			Transaction = 1,
			Message = 2,
			Block = 3,
			Digest = 4
		}

		public enum BlockHashingModes : byte {
			Mode1 = 1
		}

		[Flags]
		public enum CertificateAccountPermissionTypes {
			None = 0,
			FixedList = 1,
			MaximumAmount = 2,
			Any = 3
		}

		[Flags]
		public enum CertificateApplicationTypes {
			Envelope = 1 << 1,
			Transaction = 1 << 2,
			Election = 1 << 3,
			Abstract = 1 << 4
		}

		public enum CertificateStates : byte {
			Revoked = 0,
			Active = 1

		}

		public enum ChainSharingTypes : byte {
			None = 0,
			BlockOnly = 1,
			DigestThenBlocks = 2,
			DigestAndBlocks = 3,
			Full = DigestAndBlocks
		}

		public enum ChainSyncState {
			Synchronized,
			LikelyDesynchronized,
			Desynchronized
		}

		public enum GossipSupportTypes : byte {
			None = 0,
			Basic = 1,

			//Basic = 2,
			Full = 3
		}

		[Flags]
		public enum KeyHashBits : byte {
			SHA3_256 = SHA3,
			SHA3_512 = SHA3 | HASH512,
			SHA2_256 = SHA2,
			SHA2_512 = SHA2 | HASH512,
			BLAKE2_256 = BLAKE2,
			BLAKE2_512 = BLAKE2 | HASH512
		}

		public enum KeyStatus : byte {
			Ok = 1,
			Changing = 2,
			New = 3
		}

		public enum KeyTypes : byte {
			Unknown = 0,
			XMSS = 1,
			XMSSMT = 2,
			NTRU = 3,
			SPHINCS = 4,
			QTESLA = 5,
			ECDSA = 6,
			RSA = 7,

			// more?

			Secret = 15,
			SecretCombo = 16,
			SecretDouble = 17,
			SecretPenta = 18,
			MCELIECE = 19

		}

		public enum MiningStatus : byte {
			Unknown = 0,
			Mining = 1,
			IpUsed = 2,
			NotMining = byte.MaxValue
		}

		public enum MiningTiers : byte {

			FirstTier = 1,
			SecondTier = 2,
			ThirdTier = 3,
			FourthTier = 4,
			Other = byte.MaxValue
		}

		public enum PeerTypes : byte {
			Unknown = 0,
			FullNode = 1,
			Mobile = 2,
			Sdk = 3,
			Hub = 4
		}

		public enum PublicationStatus : byte {
			New = 1,
			Dispatched = 2,
			Published = 3,
			Rejected = 4
		}

		public enum ServiceExecutionTypes {
			None,
			Threaded,
			Synchronous
		}

		public enum ThreadMode {
			Single,
			Quarter,
			Half,
			ThreeQuarter,
			Full
		}

		public const byte SHA2 = 0x0;
		public const byte SHA3 = 0x10;
		public const byte BLAKE2 = 0x20;

		public const byte HASH512 = 0x80;

		public const string INTERFACE = "interface";
		public const string BLOCKCHAIN_SERVICE = "blockchain";
		public const string GOSSIP_SERVICE = "gossip";

		/// <summary>
		///     ensure an arbitrary value can be converted to the enum in question. ensures it is a valid value.
		/// </summary>
		/// <param name="enumValue"></param>
		/// <param name="retVal"></param>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="TEnum"></typeparam>
		/// <returns></returns>
		public static bool TryParseEnum<T, TEnum>(this T enumValue, out TEnum retVal) {
			retVal = default;
			bool success = Enum.IsDefined(typeof(TEnum), enumValue);

			if(success) {
				retVal = (TEnum) Enum.ToObject(typeof(TEnum), enumValue);
			}

			return success;
		}
	}
}