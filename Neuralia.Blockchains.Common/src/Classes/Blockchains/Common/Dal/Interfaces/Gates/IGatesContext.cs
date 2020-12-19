using Neuralia.Blockchains.Core.DataAccess.Interfaces;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates {

	public interface IGatesContext : IContextInterfaceBase {
	}

	public interface IGatesContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : IGatesContext
		where STANDARD_ACCOUNT_GATES : class, IStandardAccountGates
		where JOINT_ACCOUNT_GATES : class, IJointAccountGates{
	}
}