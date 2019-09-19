using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {
	public interface IFirstAccountKey : IAccountSignature {
		SafeArrayHandle PublicKey { get; }
		QTESLASecurityCategory.SecurityCategories SecurityCategory { get; set; }
	}
}