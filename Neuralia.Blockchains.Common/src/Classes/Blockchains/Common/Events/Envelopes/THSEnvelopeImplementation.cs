using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface ITHSEnvelope : ITreeHashable  {
	
		ITHSEnvelopeSignature THSEnvelopeSignatureBase { get; set; }
		HashNodeList GetTHSStructuresArray();
		string Key { get; }
	}

	public interface ITHSEnvelope<BLOCKCHAIN_EVENT_TYPE, THS_SIGNATURE> : ITHSEnvelope
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where THS_SIGNATURE : class, ITHSEnvelopeSignature{
		
		THS_SIGNATURE THSEnvelopeSignature { get; set; }
	}

	public class THSEnvelopeImplementation<BLOCKCHAIN_EVENT_TYPE, THS_SIGNATURE> : ITHSEnvelope<BLOCKCHAIN_EVENT_TYPE, THS_SIGNATURE>, IBinarySerializable, ITreeHashable
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where THS_SIGNATURE : class, ITHSEnvelopeSignature, new(){

		public THS_SIGNATURE THSEnvelopeSignature { get; set; } = new THS_SIGNATURE();

		
		public THSEnvelopeImplementation() {
		}
		
		public HashNodeList GetTHSStructuresArray() {
			throw new NotImplementedException();
		}

		
		public HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.THSEnvelopeSignature);

			return nodeList;
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			
			this.THSEnvelopeSignature.Dehydrate(dehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.THSEnvelopeSignature.Rehydrate(rehydrator);
		}

		public ITHSEnvelopeSignature THSEnvelopeSignatureBase {
			get => this.THSEnvelopeSignature;
			set => this.THSEnvelopeSignature = (THS_SIGNATURE)value;
		}
		
		public string Key => throw new NotImplementedException();
	}
}