using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Serialization {

	public interface IDehydratedBlockchainDigest : IDehydrateBlockchainEvent {
		IByteArray Hash { get; set; }
		int DigestId { get; set; }
		IByteArray Contents { get; set; }
		IBlockchainDigest RehydratedDigest { get; }
		IBlockchainDigest RehydrateDigest(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}

	public class DehydratedBlockchainDigest : IDehydratedBlockchainDigest {

		public int DigestId { get; set; }
		public IByteArray Contents { get; set; }
		public IBlockchainDigest RehydratedDigest { get; private set; }

		public IByteArray Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Rehydrate(IByteArray data) {
			IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(data);

			this.Rehydrate(rehydrator);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			//TODO: what to do here?
			//			nodeList.Add(this.GetStructuresArray());

			return nodeList;
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.DigestId);
			dehydrator.WriteNonNullable(this.Hash);
			dehydrator.WriteRawArray(this.Contents);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.DigestId = rehydrator.ReadInt();
			this.Hash = rehydrator.ReadNonNullableArray();

			this.Contents = rehydrator.ReadArrayToEnd();
		}

		public IBlockchainDigest RehydrateDigest(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if(this.RehydratedDigest == null) {

				this.RehydratedDigest = rehydrationFactory.CreateDigest(this);
				this.RehydratedDigest.Rehydrate(this, rehydrationFactory);
			}

			return this.RehydratedDigest;
		}

		public IByteArray Hash { get; set; }
	}
}