using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto.Prng;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {
	public interface IEnvelope : ITreeHashable {
		SafeArrayHandle EventBytes { get; }

		SafeArrayHandle DehydrateEnvelope();
		void RehydrateEnvelope(SafeArrayHandle data);
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
		private readonly SafeArrayHandle dehydratedEnvelopeBytes = SafeArrayHandle.Create();

		protected Envelope() {
			this.Version = this.SetIdentity();

			if(this.Version.IsNull) {
				throw new ApplicationException("Version has not been set for this component");
			}
		}

		public ComponentVersion<T> Version { get; }
		private bool ContentsLoaded => this.contents != null;

		public SafeArrayHandle EventBytes { get; } = SafeArrayHandle.Create();

		public BLOCKCHAIN_EVENT_TYPE Contents {
			get {
				if(!this.ContentsLoaded && this.EventBytes.HasData) {
					this.RehydrateContents();
				}

				return this.contents;
			}
			set => this.contents = value;
		}

		public SafeArrayHandle DehydrateEnvelope() {

			if(this.dehydratedEnvelopeBytes.IsEmpty) {
				IDataDehydrator dh = DataSerializationFactory.CreateDehydrator();

				this.Version.Dehydrate(dh);

				this.Dehydrate(dh);

				this.DehydrateContents();

				// reuse the bytes we already have
				dh.WriteRawArray(this.EventBytes);

				this.dehydratedEnvelopeBytes.Entry = dh.ToArray().Entry;
			}

			return this.dehydratedEnvelopeBytes;
		}

		public void RehydrateEnvelope(SafeArrayHandle data) {

			if(this.dehydratedEnvelopeBytes.IsEmpty) {
				this.dehydratedEnvelopeBytes.Entry = data.Entry;

				using(IDataRehydrator rh = DataSerializationFactory.CreateRehydrator(data)) {

					this.Version.Rehydrate(rh);

					this.Rehydrate(rh);

					// save the raw bytes for lazy loading
					this.EventBytes.Entry = rh.ReadArrayToEnd();
				}
			}
		}

		public void RehydrateContents() {
			if(!this.ContentsLoaded) {
				if(this.EventBytes.IsEmpty) {
					throw new ApplicationException("Event bytes can not be null while rehydrating contents");
				}

				using(var rehydrator = DataSerializationFactory.CreateRehydrator(this.EventBytes)) {
					this.Contents = this.RehydrateContents(rehydrator);
				}
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

				this.EventBytes.Entry = dh.ToArray().Entry;
			}
		}

		protected abstract BLOCKCHAIN_EVENT_TYPE RehydrateContents(IDataRehydrator rh);

		protected abstract void Dehydrate(IDataDehydrator dh);

		protected abstract void Rehydrate(IDataRehydrator rh);

		protected abstract ComponentVersion<T> SetIdentity();
	}
}