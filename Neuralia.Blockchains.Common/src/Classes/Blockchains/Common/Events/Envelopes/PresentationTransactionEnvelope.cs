using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Services;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes {

	public interface IPresentationTransactionEnvelope : ITransactionEnvelope, ITHSEnvelope<IDehydratedTransaction, THSEnvelopeSignature> {
		PresentationEnvelopeSignature PresentationEnvelopeSignature { get; set; }
		SafeArrayHandle Metadata { get; set; }
		SafeArrayHandle IdentityAutograph { get; set; }

		Guid? RequesterId { get; set; }
		long? ConfirmationCode { get; set; }
	}
	
	public class PresentationTransactionEnvelope :  TransactionEnvelope, IPresentationTransactionEnvelope{
		
		public SafeArrayHandle Metadata { get; set; }
		public SafeArrayHandle IdentityAutograph { get; set; }
		
		public Guid? RequesterId { get; set; }
		public long? ConfirmationCode { get; set; }
		
		private readonly THSEnvelopeImplementation<IDehydratedTransaction, THSEnvelopeSignature> thsEnvelopeImplementation;

		public PresentationTransactionEnvelope() {
			this.thsEnvelopeImplementation = new THSEnvelopeImplementation<IDehydratedTransaction, THSEnvelopeSignature>();
		}
		
		public override HashNodeList GetStructuresArray() {
			var hashList = base.GetStructuresArray();

			hashList.Add(this.thsEnvelopeImplementation.GetStructuresArray());

			return hashList;
		}

		public override HashNodeList GetTransactionHashingStructuresArray(TransactionHashingTypes type = TransactionHashingTypes.Full) {
			HashNodeList hashNodeList = base.GetTransactionHashingStructuresArray(type);

			hashNodeList.Add(this.IdentityAutograph);
			hashNodeList.Add(this.Metadata);
			hashNodeList.Add(this.RequesterId);
			hashNodeList.Add(this.ConfirmationCode);
			
			return hashNodeList;
		}
		
		public HashNodeList GetTHSStructuresArray() {
			
			// get the fixed elements of the transaction
			HashNodeList hashNodeList = base.GetFixedStructuresArray();
			
			// here we exclude the expiration and any time based component. THS can be long
			hashNodeList.Add(this.GetTransactionHashingStructuresArray(TransactionHashingTypes.NoTime));

			return hashNodeList;
		}

		public string Key => this.Contents.Uuid.ToString();

		protected override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);
			
			this.thsEnvelopeImplementation.Rehydrate(rehydrator);
			this.IdentityAutograph = (SafeArrayHandle)rehydrator.ReadArray();
			this.Metadata = (SafeArrayHandle)rehydrator.ReadArray();
			this.RequesterId = rehydrator.ReadNullableGuid();
			this.ConfirmationCode = rehydrator.ReadNullableLong();

		}
		
		protected override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);
			
			this.thsEnvelopeImplementation.Dehydrate(dehydrator);
			dehydrator.Write(this.IdentityAutograph);
			dehydrator.Write(this.Metadata);
			dehydrator.Write(this.RequesterId);
			dehydrator.Write(this.ConfirmationCode);
		}
		
		protected override ComponentVersion<EnvelopeType> SetIdentity() {
			return (EnvelopeTypes.Instance.PresentationTransaction, 1, 0);
		}

		public ITHSEnvelopeSignature THSEnvelopeSignatureBase {
			get => this.thsEnvelopeImplementation.THSEnvelopeSignatureBase;
			set => this.thsEnvelopeImplementation.THSEnvelopeSignatureBase = value;
		}


		public THSEnvelopeSignature THSEnvelopeSignature {
			get => this.thsEnvelopeImplementation.THSEnvelopeSignature;
			set => this.thsEnvelopeImplementation.THSEnvelopeSignature = value;
		}

		public PresentationEnvelopeSignature PresentationEnvelopeSignature  {
			get => (PresentationEnvelopeSignature)this.Signature;
			set => this.Signature = value;
		}

		public class PresentationMetadata : IBinarySerializable {

			public PresentationMetadata() {
				this.ValidatorSignatureCryptographicKey = new XmssCryptographicKey();
				this.ValidatorSecretCryptographicKey = new NTRUPrimeCryptographicKey();
			}
			
			public SafeArrayHandle Stride { get; set; }
			public XmssCryptographicKey ValidatorSignatureCryptographicKey { get; }
			public NTRUPrimeCryptographicKey ValidatorSecretCryptographicKey { get; }
			
			public void Rehydrate(IDataRehydrator rehydrator) {

				this.Stride = (SafeArrayHandle)rehydrator.ReadNonNullableArray();

				this.ValidatorSignatureCryptographicKey.Rehydrate(rehydrator);
				this.ValidatorSecretCryptographicKey.Rehydrate(rehydrator);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.WriteNonNullable(this.Stride);

				this.ValidatorSignatureCryptographicKey.Dehydrate(dehydrator);
				this.ValidatorSecretCryptographicKey.Dehydrate(dehydrator);
			}
		}
	}
}