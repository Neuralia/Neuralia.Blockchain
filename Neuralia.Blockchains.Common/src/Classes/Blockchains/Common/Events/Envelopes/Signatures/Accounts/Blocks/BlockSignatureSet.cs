using System;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public class BlockSignatureSet : ISerializableCombo {
		
		public BlockSignatureSet() {

		}

		public IBlockAccountSignature BlockSignature { get; private set; } = new BlockAccountSignature();
		public byte ExpectedNextSignatureKeyOrdinal { get; set; }
		
		public byte ModeratorKeyOrdinal => this.BlockSignature.KeyUseIndex.Ordinal;

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			this.ExpectedNextSignatureKeyOrdinal = rehydrator.ReadByte();
			this.BlockSignature.Rehydrate(rehydrator);
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.ExpectedNextSignatureKeyOrdinal);
			this.BlockSignature.Dehydrate(dehydrator);
		}

		public HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.ExpectedNextSignatureKeyOrdinal);
			nodeList.Add(this.BlockSignature);

			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("ExpectedNextSignatureKeyOrdinal", this.ExpectedNextSignatureKeyOrdinal);
			jsonDeserializer.SetProperty("BlockSignature", this.BlockSignature);
		}
	}
}