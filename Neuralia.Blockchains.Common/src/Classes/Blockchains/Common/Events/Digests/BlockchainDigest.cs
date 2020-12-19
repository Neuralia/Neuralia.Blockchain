using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Specialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Published;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests {

	public interface IBlockchainDigest : IBlockchainEvent<IDehydratedBlockchainDigest, IDigestRehydrationFactory, BlockchainDigestsType> {

		int DigestId { get; set; }
		int GroupingSize { get; set; }

		TransactionTimestamp Timestamp { get; set; }
		DateTime FullTimestamp { get; set; }
		SafeArrayHandle Hash { get; }
		SafeArrayHandle PreviousDigestHash { get; }

		BlockSignatureSet BlockSignatureSet { get; set; }

		SafeArrayHandle GenesisBlockHash { get; }
		SafeArrayHandle BlockHash { get; }
		BlockId BlockId { get; set; }
		long LastUserAccountId { get; set; }
		long LastServerAccountId { get; set; }
		long LastModeratorAccountId { get; set; }
		long LastJointAccountId { get; set; }

		IPublishedAccountSignature Signature { get; }

		BlockchainDigestDescriptor DigestDescriptor { get; }
	}

	/// <summary>
	///     A very special type of entity which allows us to resume the blockchain to a certain point in time.
	/// </summary>
	public abstract class BlockchainDigest : BlockchainEvent<IDehydratedBlockchainDigest, DehydratedBlockchainDigest, IDigestRehydrationFactory, BlockchainDigestsType>, IBlockchainDigest {

		public const int DIGEST_GROUPS_SIZE = 100_000;

		//TODO: make this work so we can link back to the genesis block
		public List<ITransactionEnvelope> KeyHistory { get; } = new List<ITransactionEnvelope>();

		public int DigestId { get; set; }
		public int GroupingSize { get; set; }

		public BlockId BlockId { get; set; } = new BlockId();
		public TransactionTimestamp Timestamp { get; set; } = new TransactionTimestamp();
		public DateTime FullTimestamp { get; set; }

		public BlockSignatureSet BlockSignatureSet { get; set; } = new BlockSignatureSet();

		public IPublishedAccountSignature Signature { get; } = new PublishedAccountSignature();
		public SafeArrayHandle Hash { get; } = SafeArrayHandle.Create();
		public SafeArrayHandle PreviousDigestHash { get; } = SafeArrayHandle.Create();

		public SafeArrayHandle BlockHash { get; } = SafeArrayHandle.Create();
		public SafeArrayHandle GenesisBlockHash { get; } = SafeArrayHandle.Create();

		public long LastUserAccountId { get; set; }
		public long LastServerAccountId { get; set; }
		public long LastModeratorAccountId { get; set; }
		public long LastJointAccountId { get; set; }

		public BlockchainDigestDescriptor DigestDescriptor { get; set; } = new BlockchainDigestDescriptor();

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.DigestId);
			nodeList.Add(this.GroupingSize);

			nodeList.Add(this.BlockId);
			nodeList.Add(this.BlockHash);

			nodeList.Add(this.PreviousDigestHash);
			nodeList.Add(this.GenesisBlockHash);

			nodeList.Add(this.Timestamp);

			nodeList.Add(this.LastUserAccountId);
			nodeList.Add(this.LastServerAccountId);
			nodeList.Add(this.LastModeratorAccountId);
			nodeList.Add(this.LastJointAccountId);

			nodeList.Add(this.DigestDescriptor);

			return nodeList;
		}

		public override void Rehydrate(IDehydratedBlockchainDigest data, IDigestRehydrationFactory rehydrationFactory) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data.Contents);

			ComponentVersion<BlockchainDigestsType> rehydratedVersion = rehydrator.Rehydrate<ComponentVersion<BlockchainDigestsType>>();
			this.Version.EnsureEqual(rehydratedVersion);

			this.DigestId = rehydrator.ReadInt();
			this.GroupingSize = rehydrator.ReadInt();
			this.Hash.Entry = rehydrator.ReadNonNullableArray();

			this.BlockId.Rehydrate(rehydrator);
			this.BlockHash.Entry = rehydrator.ReadNonNullableArray();

			this.BlockSignatureSet.Rehydrate(rehydrator);

			this.PreviousDigestHash.Entry = rehydrator.ReadArray();
			this.GenesisBlockHash.Entry = rehydrator.ReadNonNullableArray();

			this.Timestamp.Rehydrate(rehydrator);
			this.LastUserAccountId = rehydrator.ReadLong();
			this.LastServerAccountId = rehydrator.ReadLong();
			this.LastModeratorAccountId = rehydrator.ReadLong();
			this.LastJointAccountId = rehydrator.ReadLong();

			this.Signature.Rehydrate(rehydrator);

			this.DigestDescriptor.Rehydrate(rehydrator);

			//			int count = rehydrator.ReadInt();

			// TODO: handle moderator keys that link back to the genesis block
			//			for(int i = 0; i < count; i++) {
			//				ITransactionEnvelope envelope = new TransactionEnvelope();
			//				
			//				KeyHistory
			//				
			//				this.Files.Add(type, file);
			//			}

			rehydrationFactory.PrepareDigest(this);
		}

		public override IDehydratedBlockchainDigest Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels) {
			// do nothing here, we really never dehydrate a block
			throw new NotImplementedException();
		}

		protected abstract IAccountSnapshotDigestChannel CreateAccountSnapshotDigestChannel(int groupSize);

		protected override ComponentVersion<BlockchainDigestsType> SetIdentity() {
			return (BlockchainDigestsTypes.Instance.Basic, 1, 0);

		}
	}
}