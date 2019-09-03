using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {
	public interface IEnvelope : ITreeHashable {
		IByteArray EventBytes { get; }

		IByteArray DehydrateEnvelope();
		void RehydrateEnvelope(IByteArray data);
		void RehydrateContents();
	}

	public interface IEnvelope<BLOCKCHAIN_EVENT_TYPE> : IEnvelope
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable {

		BLOCKCHAIN_EVENT_TYPE Contents { get; set; }
	}

	public abstract class Envelope<BLOCKCHAIN_EVENT_TYPE, T> : IEnvelope<BLOCKCHAIN_EVENT_TYPE>
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where T : SimpleUShort<T>, new() {

		private BLOCKCHAIN_EVENT_TYPE contents;
		private IByteArray dehydratedEnvelopeBytes;

		protected Envelope() {
			this.Version = this.SetIdentity();

			if(this.Version.IsNull) {
				throw new ApplicationException("Version has not been set for this component");
			}
		}

		public ComponentVersion<T> Version { get; }
		private bool ContentsLoaded => this.contents != null;

		public IByteArray EventBytes { get; private set; } = new ByteArray();

		public BLOCKCHAIN_EVENT_TYPE Contents {
			get {
				if(!this.ContentsLoaded && this.EventBytes.HasData) {
					this.RehydrateContents();
				}

				return this.contents;
			}
			set => this.contents = value;
		}

		public IByteArray DehydrateEnvelope() {

			if(this.dehydratedEnvelopeBytes == null) {
				IDataDehydrator dh = DataSerializationFactory.CreateDehydrator();

				this.Version.Dehydrate(dh);

				this.Dehydrate(dh);

				this.DehydrateContents();

				// reuse the bytes we already have
				dh.WriteRawArray(this.EventBytes);

				this.dehydratedEnvelopeBytes = dh.ToArray();
			}

			return this.dehydratedEnvelopeBytes;
		}

		public void RehydrateEnvelope(IByteArray data) {

			if(this.dehydratedEnvelopeBytes == null) {
				this.dehydratedEnvelopeBytes = data;

				IDataRehydrator rh = DataSerializationFactory.CreateRehydrator(data);

				this.Version.Rehydrate(rh);

				this.Rehydrate(rh);

				// save the raw bytes for lazy loading
				this.EventBytes = rh.ReadArrayToEnd();
			}
		}

		public void RehydrateContents() {
			if(!this.ContentsLoaded) {
				if(this.EventBytes.IsEmpty) {
					throw new ApplicationException("Event bytes can not be null while rehydrating contents");
				}

				this.Contents = this.RehydrateContents(DataSerializationFactory.CreateRehydrator(this.EventBytes));
			}
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Version);

			this.DehydrateContents();

			nodeList.Add(this.EventBytes);

			return nodeList;
		}

		public void DehydrateContents() {
			if((this.EventBytes == null) || this.EventBytes.IsEmpty) {

				if(!this.ContentsLoaded) {
					throw new ApplicationException("Blockchain event must be loaded to dehydrate an envelope");
				}

				IDataDehydrator dh = DataSerializationFactory.CreateDehydrator();
				this.Contents.Dehydrate(dh);

				this.EventBytes = dh.ToArray();
			}
		}

		protected abstract BLOCKCHAIN_EVENT_TYPE RehydrateContents(IDataRehydrator rh);

		protected abstract void Dehydrate(IDataDehydrator dh);

		protected abstract void Rehydrate(IDataRehydrator rh);

		protected abstract ComponentVersion<T> SetIdentity();
	}
}