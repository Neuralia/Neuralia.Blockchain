using System;
using System.Collections.Immutable;
using System.Linq;

namespace Neuralia.Blockchains.Core {
	public static class Enums {

		public enum AccountTypes : byte {
			Standard = 1,
			Joint = 2
		}

		public enum BlockchainEventTypes : byte {
			Transaction = 1,
			Message = 2,
			Block = 3,
			Digest = 4
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

		public enum ChainSyncState {
			Synchronized,
			LikelyDesynchronized,
			Desynchronized
		}

		public enum KeyHashBits : byte {
			SHA3_256 = 1,
			SHA3_384 = 2,
			SHA3_512 = 3,
			SHA2_256 = 4,
			SHA2_512 = 5
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
			Secret = 6,
			SecretCombo = 7,
			SecretDouble = 8,
			SecretPenta = 9,
			MCELIECE = 10,
			ECDSA = 11,
			RSA = 12

		}

		public enum PeerTypes : byte {
			Unknown = 0,
			FullNode = 1,
			SimpleMobile = 2,
			PowerMobile = 3,
			SimpleSdk = 4,
			PowerSdk = 5,
			Hub = 6
		}

		[Flags]
		public enum PeerTypeSupport : short {
			None = 1 << 0,
			Sync = 1 << 1,
			GossipBasic = 1 << 2,
			FullGossip = GossipBasic | (1 << 3),
			Full = Sync | FullGossip
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

		public const PeerTypeSupport FullnodeSupport = PeerTypeSupport.Full;
		public const PeerTypeSupport SimpleMobileSupport = PeerTypeSupport.None;
		public const PeerTypeSupport GossipMobileSupport = PeerTypeSupport.GossipBasic;
		public const PeerTypeSupport SimpleSdkSupport = PeerTypeSupport.None;
		public const PeerTypeSupport PowerSdkSupport = PeerTypeSupport.GossipBasic;
		public const PeerTypeSupport HubSupport = PeerTypeSupport.None;

		public const string INTERFACE = "interface";
		public const string SERIALIZATION_SERVICE = "serialization";
		public const string VALIDATION_SERVICE = "validation";
		public const string BLOCKCHAIN_SERVICE = "blockchain";

		public static ImmutableList<(PeerTypeSupport supportType, PeerTypes peerType)> PeerTypeMappings => new[] {(SimpleMobileSupport, PeerTypes.Unknown), (FullnodeSupport, PeerTypes.FullNode), (SimpleMobileSupport, PeerTypes.SimpleMobile), (HubSupport, PeerTypes.Hub), (GossipMobileSupport, PeerTypes.PowerMobile), (SimpleSdkSupport, PeerTypes.SimpleSdk), (PowerSdkSupport, PeerTypes.PowerSdk)}.ToImmutableList();
		public static ImmutableList<PeerTypeSupport> PeerTypeSupports => PeerTypeMappings.Select(t => t.supportType).ToImmutableList();

		public static ImmutableList<PeerTypeSupport> SimplePeerSupportTypes => PeerTypeSupports.Where(t => t.HasFlag(PeerTypeSupport.None)).ToImmutableList();
		public static ImmutableList<PeerTypeSupport> SyncingPeerSupportTypes => PeerTypeSupports.Where(t => t.HasFlag(PeerTypeSupport.Sync)).ToImmutableList();
		public static ImmutableList<PeerTypeSupport> BasicGossipPeerSupportTypes => PeerTypeSupports.Where(t => t.HasFlag(PeerTypeSupport.GossipBasic)).ToImmutableList();
		public static ImmutableList<PeerTypeSupport> FullGossipPeerSupportTypes => PeerTypeSupports.Where(t => t.HasFlag(PeerTypeSupport.FullGossip)).ToImmutableList();
		public static ImmutableList<PeerTypeSupport> CompletePeerSupportTypes => PeerTypeSupports.Where(t => t.HasFlag(PeerTypeSupport.Full)).ToImmutableList();

		public static ImmutableList<PeerTypes> SimplePeerTypes => PeerTypeMappings.Where(t => SimplePeerSupportTypes.Contains(t.supportType)).Select(t => t.peerType).ToImmutableList();
		public static ImmutableList<PeerTypes> SyncingPeerTypes => PeerTypeMappings.Where(t => SyncingPeerSupportTypes.Contains(t.supportType)).Select(t => t.peerType).ToImmutableList();
		public static ImmutableList<PeerTypes> BasicGossipPeerTypes => PeerTypeMappings.Where(t => BasicGossipPeerSupportTypes.Contains(t.supportType)).Select(t => t.peerType).ToImmutableList();
		public static ImmutableList<PeerTypes> FullGossipPeerTypes => PeerTypeMappings.Where(t => FullGossipPeerSupportTypes.Contains(t.supportType)).Select(t => t.peerType).ToImmutableList();
		public static ImmutableList<PeerTypes> CompletePeerTypes => PeerTypeMappings.Where(t => CompletePeerSupportTypes.Contains(t.supportType)).Select(t => t.peerType).ToImmutableList();

		public static bool DoesPeerTypeSupport(PeerTypes peerType, PeerTypeSupport peerTypeSupport) {
			return PeerTypeMappings.Any(t => (t.peerType == peerType) && t.supportType.HasFlag(peerTypeSupport));
		}

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