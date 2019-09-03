using Neuralia.Blockchains.Tools.Data;
using Neuralia.BouncyCastle.extra.pqc.crypto.qtesla;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts {
	public interface IFirstAccountKey : IAccountSignature {
		IByteArray PublicKey { get; set; }
		QTESLASecurityCategory.SecurityCategories SecurityCategory { get; set; }
	}
}