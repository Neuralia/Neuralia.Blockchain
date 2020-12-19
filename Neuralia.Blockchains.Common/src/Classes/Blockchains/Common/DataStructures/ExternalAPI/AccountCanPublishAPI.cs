namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {
	public class AccountCanPublishAPI {
		public bool CanPublish { get; set; }
		public int PublishMode { get; set; }
		public string RequesterId { get; set; }
		public string ConfirmationCode { get; set; }
	}
}