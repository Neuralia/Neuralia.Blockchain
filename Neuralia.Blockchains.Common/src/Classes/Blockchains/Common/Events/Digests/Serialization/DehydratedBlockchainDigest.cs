using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Serialization {

	public interface IDehydratedBlockchainDigest : IDehydrateBlockchainEvent {
		SafeArrayHandle Hash { get;  }
		int DigestId { get; set; }
		SafeArrayHandle Contents { get; }
		IBlockchainDigest RehydratedDigest { get; }
		IBlockchainDigest RehydrateDigest(IBlockchainEventsRehydrationFactory rehydrationFactory);
	}

	public class DehydratedBlockchainDigest : IDehydratedBlockchainDigest {

		public int DigestId { get; set; }
		public SafeArrayHandle Contents { get;  } = SafeArrayHandle.Create();
		public IBlockchainDigest RehydratedDigest { get; private set; }

		public SafeArrayHandle Dehydrate() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public void Rehydrate(SafeArrayHandle data) {
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
			this.Hash.Entry = rehydrator.ReadNonNullableArray();

			this.Contents.Entry = rehydrator.ReadArrayToEnd();
		}

		public IBlockchainDigest RehydrateDigest(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if(this.RehydratedDigest == null) {

				this.RehydratedDigest = rehydrationFactory.CreateDigest(this);
				this.RehydratedDigest.Rehydrate(this, rehydrationFactory);
			}

			return this.RehydratedDigest;
		}

		public SafeArrayHandle Hash { get;  } = SafeArrayHandle.Create();
		
	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if(this.IsDisposed) {
				return;
			}
			
			if(disposing) {
				this.Contents?.Dispose();
				this.Hash?.Dispose();
			}
			this.IsDisposed = true;
		}

		~DehydratedBlockchainDigest() {
			this.Dispose(false);
		}
		
		public bool IsDisposed { get; private set; }

	#endregion
	}
}