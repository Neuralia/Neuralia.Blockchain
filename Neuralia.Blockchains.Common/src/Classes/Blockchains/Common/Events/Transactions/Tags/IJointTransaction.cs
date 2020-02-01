using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.JointSignatureTypes;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags {
	
	public interface IJointTransaction{
		
	}
	
	/// <summary>
	/// These transactions will REQUIRE a joint signature
	/// </summary>
	public interface IJointTransaction<out T>  : IJointTransaction where T : IJointSignatureType {
		
	}
}