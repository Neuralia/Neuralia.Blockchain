using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages {

	public interface IBlockchainMessage : IBlockchainEvent<IDehydratedBlockchainMessage, IMessageRehydrationFactory, BlockchainMessageType> {
		Guid Uuid { get; set; }

		TransactionTimestamp Timestamp { get; set; }
	}

	public abstract class BlockchainMessage : BlockchainEvent<IDehydratedBlockchainMessage, DehydratedBlockchainMessage, IMessageRehydrationFactory, BlockchainMessageType>, IBlockchainMessage {

		public Guid Uuid { get; set; } = Guid.NewGuid();

		public TransactionTimestamp Timestamp { get; set; } = new TransactionTimestamp();

		public override sealed void Rehydrate(IDehydratedBlockchainMessage dehydratedMessage, IMessageRehydrationFactory rehydrationFactory) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(dehydratedMessage.Contents);

			ComponentVersion<BlockchainMessageType> rehydratedVersion = rehydrator.Rehydrate<ComponentVersion<BlockchainMessageType>>();
			this.Version.EnsureEqual(rehydratedVersion);

			this.Uuid = rehydrator.ReadGuid();
			this.Timestamp.Rehydrate(rehydrator);

			this.RehydrateContents(rehydrator, rehydrationFactory);

		}

		public override sealed IDehydratedBlockchainMessage Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels) {

			IDehydratedBlockchainMessage dehydratedMessage = new DehydratedBlockchainMessage();

			dehydratedMessage.RehydratedMessage = this;

			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Version.Dehydrate(dehydrator);

			dehydrator.Write(this.Uuid);
			this.Timestamp.Dehydrate(dehydrator);

			this.DehydrateContents(dehydrator);

			dehydratedMessage.Contents.Entry = dehydrator.ToArray().Entry;

			return dehydratedMessage;
		}

		protected virtual void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {

		}

		protected virtual void DehydrateContents(IDataDehydrator dehydrator) {

		}
	}
}