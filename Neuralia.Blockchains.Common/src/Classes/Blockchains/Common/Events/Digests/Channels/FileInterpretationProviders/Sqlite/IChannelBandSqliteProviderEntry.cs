namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.FileInterpretationProviders.Sqlite {
	public interface IChannelBandSqliteProviderEntry<KEY>
		where KEY : struct {

		KEY Id { get; set; }
	}
}