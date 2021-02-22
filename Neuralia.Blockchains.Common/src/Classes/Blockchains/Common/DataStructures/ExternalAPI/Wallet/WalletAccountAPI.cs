using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI.Wallet {

	public class WalletAccountAPI {
		public string AccountCode { get; set; }
		public string AccountId { get; set; }
		public string FriendlyName { get; set; }
		public bool IsActive { get; set; }
		public int Status { get; set; }
	}

	public class WalletAccountDetailsAPI {
		public string AccountCode { get; set; }
		public string AccountId { get; set; }
		public string AccountHash { get; set; }
		public string FriendlyName { get; set; }
		public bool IsActive { get; set; }
		public int Status { get; set; }
		public long DeclarationBlockId { get; set; }
		public bool KeysEncrypted { get; set; }
		public int AccountType { get; set; }
		public int TrustLevel { get; set; }
		public int Verification { get; set; }
		public bool InAppointment { get; set; }
		public DateTime? VerificationExpiration { get; set; }
		public DateTime? VerificationExpirationWarning { get; set; }
		public bool VerificationExpiring { get; set; }
		public bool VerificationExpired { get; set; }
	}
	
	public struct WalletAccountAppointmentDetailsAPI {

		public int Status { get; set; } 
		public DateTime? AppointmentRequestTimeStamp  { get; set; }
		public long? AppointmentConfirmationId { get; set; }
		public DateTime? AppointmentTime { get; set; }
		public DateTime? AppointmentContextTime { get; set; }
		public DateTime? AppointmentVerificationTime { get; set; }
		public DateTime? AppointmentConfirmationIdExpiration { get; set; }
		public int? AppointmentWindow { get; set; }
		public int Region { get; set; }
		public DateTime? AppointmentPreparationWindowStart { get; set; }
		public DateTime? AppointmentPreparationWindowEnd { get; set; }
	}
}