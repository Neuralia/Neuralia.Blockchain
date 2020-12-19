using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {
	public interface IBlockchainDigestChannelFactory {

		IDigestChannel CreateCreateDigestChannels(BlockchainDigestSimpleChannelDescriptor channelDescriptor, string folder);

		T CreateCreateDigestChannels<T>(BlockchainDigestSimpleChannelDescriptor channelDescriptor, string folder)
			where T : class, IDigestChannel;

		IStandardAccountKeysDigestChannel CreateUserAccountKeysDigestChannel(int groupSize, string folder);
		IUserAccountSnapshotDigestChannel CreateUserAccountSnapshotDigestChannel(int groupSize, string folder);
		
		IStandardAccountKeysDigestChannel CreateServerAccountKeysDigestChannel(int groupSize, string folder);
		IServerAccountSnapshotDigestChannel CreateServerAccountSnapshotDigestChannel(int groupSize, string folder);
		
		IStandardAccountKeysDigestChannel CreateModeratorAccountKeysDigestChannel(int groupSize, string folder);
		IModeratorAccountSnapshotDigestChannel CreateModeratorAccountSnapshotDigestChannel(int groupSize, string folder);

		
		IJointAccountSnapshotDigestChannel CreateJointAccountSnapshotDigestChannel(int groupSize, string folder);

		IAccreditationCertificateDigestChannel CreateAccreditationCertificateDigestChannel(string folder);

		IChainOptionsDigestChannel CreateChainOptionsDigestChannel(string folder);
	}

	public abstract class BlockchainDigestChannelFactory : IBlockchainDigestChannelFactory {

		public virtual IDigestChannel CreateCreateDigestChannels(BlockchainDigestSimpleChannelDescriptor channelDescriptor, string folder) {
			return this.CreateCreateDigestChannels<IDigestChannel>(channelDescriptor, folder);
		}

		public virtual T CreateCreateDigestChannels<T>(BlockchainDigestSimpleChannelDescriptor channelDescriptor, string folder)
			where T : class, IDigestChannel {
			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.UserAccountSnapshot) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateUserAccountSnapshotDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}
			
			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.ServerAccountSnapshot) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateServerAccountSnapshotDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}
			
			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.ModeratorAccountSnapshot) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateModeratorAccountSnapshotDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}

			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.JointAccountSnapshot) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateJointAccountSnapshotDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}

			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.UserAccountKeys) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateUserAccountKeysDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}

			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.ServerAccountKeys) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateServerAccountKeysDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}
			
			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.ModeratorAccountKeys) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateModeratorAccountKeysDigestChannel(channelDescriptor.GroupSize, folder);
				}
			}
			
			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.AccreditationCertificates) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateAccreditationCertificateDigestChannel(folder);
				}
			}

			if(channelDescriptor.ChannelType == DigestChannelTypes.Instance.ChainOptions) {
				if(channelDescriptor.Version == (0, 1)) {
					return (T) this.CreateChainOptionsDigestChannel(folder);
				}
			}

			return null;
		}

		public abstract IStandardAccountKeysDigestChannel CreateUserAccountKeysDigestChannel(int groupSize, string folder);
		public abstract IUserAccountSnapshotDigestChannel CreateUserAccountSnapshotDigestChannel(int groupSize, string folder);
		
		public abstract IStandardAccountKeysDigestChannel CreateServerAccountKeysDigestChannel(int groupSize, string folder);
		public abstract IServerAccountSnapshotDigestChannel CreateServerAccountSnapshotDigestChannel(int groupSize, string folder);
		
		public abstract IStandardAccountKeysDigestChannel CreateModeratorAccountKeysDigestChannel(int groupSize, string folder);
		public abstract IModeratorAccountSnapshotDigestChannel CreateModeratorAccountSnapshotDigestChannel(int groupSize, string folder);

		public abstract IJointAccountSnapshotDigestChannel CreateJointAccountSnapshotDigestChannel(int groupSize, string folder);

		public abstract IAccreditationCertificateDigestChannel CreateAccreditationCertificateDigestChannel(string folder);

		public abstract IChainOptionsDigestChannel CreateChainOptionsDigestChannel(string folder);
	}
}