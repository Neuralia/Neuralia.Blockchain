using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events {
	public interface IBlockchainEvent : ITreeHashable, IJsonSerializable {
		//		void Rehydrate(ArrayWrapper data, IRehydrationFactory rehydrationFactory);
		//		void Rehydrate(IDataRehydrator rehydrator, IRehydrationFactory rehydrationFactory);
	}

	public interface IBlockchainEvent<VERSION_TYPE> : IVersionable<VERSION_TYPE>, IBlockchainEvent
		where VERSION_TYPE : SimpleUShort<VERSION_TYPE>, new() {
		//		void Rehydrate(ArrayWrapper data, IRehydrationFactory rehydrationFactory);
		//		void Rehydrate(IDataRehydrator rehydrator, IRehydrationFactory rehydrationFactory);
	}

	public interface IBlockchainEvent<DEHYDRATED, in REHYDRATION_FACTORY, VERSION_TYPE> : IBlockchainEvent<VERSION_TYPE>
		where DEHYDRATED : IDehydrateBlockchainEvent
		where REHYDRATION_FACTORY : IRehydrationFactory
		where VERSION_TYPE : SimpleUShort<VERSION_TYPE>, new() {

		DEHYDRATED Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels);
		void Rehydrate(DEHYDRATED data, REHYDRATION_FACTORY rehydrationFactory);
		void Rehydrate(SafeArrayHandle data, REHYDRATION_FACTORY rehydrationFactory);
		void Rehydrate(IDataRehydrator rehydrator, REHYDRATION_FACTORY rehydrationFactory);
	}

	/// <summary>
	///     The base class for all events in the blockchain
	/// </summary>
	/// <typeparam name="REHYDRATION_FACTORY"></typeparam>
	public abstract class BlockchainEvent<DEHYDRATED, DEHYDRATED_IMPL, REHYDRATION_FACTORY, VERSION_TYPE> : Versionable<VERSION_TYPE>, IBlockchainEvent<DEHYDRATED, REHYDRATION_FACTORY, VERSION_TYPE>
		where DEHYDRATED : IDehydrateBlockchainEvent
		where DEHYDRATED_IMPL : DEHYDRATED, new()
		where REHYDRATION_FACTORY : IRehydrationFactory
		where VERSION_TYPE : SimpleUShort<VERSION_TYPE>, new() {

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}

		public abstract DEHYDRATED Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels);

		public void Rehydrate(SafeArrayHandle data, REHYDRATION_FACTORY rehydrationFactory) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator, rehydrationFactory);

		}

		public void Rehydrate(IDataRehydrator rehydrator, REHYDRATION_FACTORY rehydrationFactory) {
			DEHYDRATED dehydrated = new DEHYDRATED_IMPL();

			dehydrated.Rehydrate(rehydrator);

			this.Rehydrate(dehydrated, rehydrationFactory);
		}

		public abstract void Rehydrate(DEHYDRATED data, REHYDRATION_FACTORY rehydrationFactory);

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
		}
	}
}