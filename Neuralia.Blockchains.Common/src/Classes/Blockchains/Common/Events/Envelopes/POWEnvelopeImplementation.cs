using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IPOWEnvelope : ITreeHashable  {
	
		IPOWEnvelopeSignature PowEnvelopeSignatureBase { get; set; }
		HashNodeList GetPOWStructuresArray();
		string Key { get; }
	}

	public interface IPOWEnvelope<BLOCKCHAIN_EVENT_TYPE, POW_SIGNATURE> : IPOWEnvelope
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where POW_SIGNATURE : class, IPOWEnvelopeSignature{
		
		POW_SIGNATURE PowEnvelopeSignature { get; set; }
	}

	public class POWEnvelopeImplementation<BLOCKCHAIN_EVENT_TYPE, POW_SIGNATURE> : IPOWEnvelope<BLOCKCHAIN_EVENT_TYPE, POW_SIGNATURE>, IBinarySerializable, ITreeHashable
		where BLOCKCHAIN_EVENT_TYPE : class, IBinarySerializable, ITreeHashable
		where POW_SIGNATURE : class, IPOWEnvelopeSignature, new(){

		public POW_SIGNATURE PowEnvelopeSignature { get; set; } = new POW_SIGNATURE();

		
		public POWEnvelopeImplementation() {
		}
		
		public HashNodeList GetPOWStructuresArray() {
			throw new NotImplementedException();
		}

		
		public HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.PowEnvelopeSignature);

			return nodeList;
		}

		public void Dehydrate(IDataDehydrator dehydrator) {
			
			this.PowEnvelopeSignature.Dehydrate(dehydrator);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {

			this.PowEnvelopeSignature.Rehydrate(rehydrator);
		}

		public IPOWEnvelopeSignature PowEnvelopeSignatureBase {
			get => this.PowEnvelopeSignature;
			set => this.PowEnvelopeSignature = (POW_SIGNATURE)value;
		}
		
		public string Key => throw new NotImplementedException();
	}
}