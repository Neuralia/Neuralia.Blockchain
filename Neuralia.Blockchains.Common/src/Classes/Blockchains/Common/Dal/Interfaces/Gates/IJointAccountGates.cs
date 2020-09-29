namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates {
	public interface IJointAccountGates : IAccountGates {
		
		long? RulesBlockId { get; set; }
	}
}