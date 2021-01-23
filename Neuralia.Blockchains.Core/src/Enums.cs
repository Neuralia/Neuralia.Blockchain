using System;
using Neuralia.Blockchains.Core.General.Types;
namespace Neuralia.Blockchains.Core {
	public static class Enums {
		
		/// <summary>
		///   Describes how memory used by Time Hard Signature is handled. 
		/// </summary>
		public enum THSMemoryTypes {
			/// <summary>
			/// Use RAM memory
			/// </summary>
			RAM = 1,
			/// <summary>
			/// Use hard disk for memory.
			/// </summary>
			HDD = 2,
			/// <summary>
			/// Write data to database or if not possible write to hard disk.
			/// </summary>
			HDD_DB = 3
		}
		
		public enum AccountTypes : byte {
			Unknown = 0,
			User = 1,
			Server = 2,
			Moderator = 3,
			Joint = 4,
			Presentation = AccountId.MAX_ACCOUNT_TYPE
		}
		
		public enum AccountVerificationTypes : byte {
			None = 0,
			Appointment = 1,
			SMS = 2,
			Phone = 3,
			Email = 4,
			Gate = 5,
			KYC = 100,
			Expired = byte.MaxValue-1,
			Unknown = byte.MaxValue
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

		public enum BookkeepingTypes {
			None,
			Debit,
			Credit
		}

		[Flags]
		public enum KeyHashType : byte {
			SHA3_256 = SHA3,
			SHA3_512 = SHA3 | HASH512,
			SHA2_256 = SHA2,
			SHA2_512 = SHA2 | HASH512
		}

		public enum KeyStatus : byte {
			New = 1,
			Ready = 2,
			Changing = 3,
			
		}

		/// <summary>
		/// 
		/// </summary>
		public enum MiningStatus : byte {
			Unknown = 0,
			Mining = 1,
			IpUsed = 2,
			NotMining = byte.MaxValue
		}

		/// <summary>
		/// 
		/// </summary>
		public enum MiningTiers : byte {

			FirstTier = 1,
			SecondTier = 2,
			ThirdTier = 3,
			FourthTier = 4,
			Other = byte.MaxValue
		}

		/// <summary>
		/// 
		/// </summary>
		public enum PeerTypes : byte {
			Unknown = 0,
			FullNode = 1,
			Mobile = 2,
			Sdk = 3,
			Hub = 4
		}

		/// <summary>
		/// 
		/// </summary>
		public enum PublicationStatus : byte {
			Unknown = 0,
			New = 1,
			Dispatched = 2,
			Published = 3,
			Dispatching = 4,
			Rejected = byte.MaxValue
		}
		
		/// <summary>
		/// 
		/// </summary>
		public enum AccountPublicationModes : byte {
			Unknown = 0,
			Appointment = 1,
			SMS = 2,
			Server = 3
		}

		/// <summary>
		/// 
		/// </summary>
		public enum OperationStatus : byte {
			Unknown = 0,
			None = 1,
			Appointment = 2,
			Presenting = 3,
		}
		/// <summary>
		/// 
		/// </summary>
		public enum AppointmentStatus : byte {
			None = 0,
			AppointmentRequested = 1,
			AppointmentSet = 2,
			AppointmentContextCached = 3,
			AppointmentPuzzleCompleted = 4,
			AppointmentCompleted = 5
		}
		
		public enum AppointmentResults : byte {
			None = 0,
			Succeeded = 1,
			Failed = 2,
		}

		public enum ServiceExecutionTypes {
			None,
			Threaded,
			Synchronous
		}
		/** @brief 
		**/
		/// <summary>
		/// How much of the available processor's cores should we use?
		/// </summary>
		public enum ThreadMode:byte {
			/// <summary>
			/// Use only 1 core
			/// </summary>
			Single = 0,
			/// <summary>
			/// Use 25% of the available processor's cores
			/// </summary>
			Quarter = 1,
			/// <summary>
			/// Use 50% or the available processor's cores
			/// </summary>
			Half = 2,
			/// <summary>
			/// Use 75% of the available processor's cores
			/// </summary>
			ThreeQuarter = 3,
			/// <summary>
			/// Use all available processor cores
			/// </summary>
			Full = 4
		}

		public enum AppointmentsResultTypes : int {
			Puzzle = 1,
			SecretCodeL2 = 3
		}

		[Flags]
		public enum MutableStructureTypes :int{
			None = 0,
			Fixed = 1 << 0,
			Mutable = 1 << 1,
			All = Fixed |  Mutable
		}

		[Flags]
		public enum AppointmentsRegions : int {
			Occident = 1 << 0, // 1
			Central = 1 << 1, // 2
			Orient = 1 << 2, // 4,
			Test = 0x80000 // 524288
		}
		
		public enum AppointmentValidationProtocols:byte {
			Undefined=0,
			Standard=1,
			Backup=2,
		}

		public enum AppointmentRequestModes:byte {
			Unknown = 0, RequestConfirmation= 1, Context = 2, Trigger = 3, VerificationConfirmation = 4
		}

		public enum TransactionTargetTypes : int {
			None=0, Range=1, All=2
		}
		public const byte SHA2 = 0x1;
		public const byte SHA3 = 0x2;

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