namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain.ChainSync.Messages.V1.Tags {

	public interface ISyncRequest<KEY> : ISyncEvent<KEY> {
		byte RequestAttempt { get; set; }
	}
}