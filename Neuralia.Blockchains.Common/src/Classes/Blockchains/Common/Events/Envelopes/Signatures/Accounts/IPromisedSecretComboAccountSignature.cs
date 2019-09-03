namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {
	public interface IPromisedSecretComboAccountSignature : IPromisedSecretAccountSignature {
		long PromisedNonce1 { get; set; }

		long PromisedNonce2 { get; set; }
	}
}