using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags.Widgets.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes.Signatures.Accounts.Blocks {
	public class BlockSignatureSet : ISerializableCombo {

		public enum BlockSignatureTypes : byte {
			SecretSequential = 1,
			XmssMT = 2,
			Genesis = 3,
			SuperSecret = 4
		}

		private readonly TwoBitArray bitArray = new TwoBitArray(2);

		public BlockSignatureSet() {

		}

		public BlockSignatureSet(BlockSignatureTypes signatureType, BlockSignatureTypes nextSignatureType) {
			if(signatureType == BlockSignatureTypes.Genesis) {
				this.BlockAccountSignature = new GenesisBlockAccountSignature();
				this.AccountSignatureType = BlockSignatureTypes.Genesis;
			} else if(signatureType == BlockSignatureTypes.XmssMT) {
				this.BlockAccountSignature = new XmssBlockAccountSignature();
				this.AccountSignatureType = BlockSignatureTypes.XmssMT;
			} else if(signatureType == BlockSignatureTypes.SecretSequential) {
				this.BlockAccountSignature = new SecretBlockAccountSignature();
				this.AccountSignatureType = BlockSignatureTypes.SecretSequential;
			} else if(signatureType == BlockSignatureTypes.SuperSecret) {
				this.BlockAccountSignature = new SuperSecretBlockAccountSignature();
				this.AccountSignatureType = BlockSignatureTypes.SuperSecret;
			}

			if(nextSignatureType == BlockSignatureTypes.XmssMT) {
				this.NextBlockAccountSignature = new XmssBlockNextAccountSignature();
				this.NextAccountSignatureType = BlockSignatureTypes.XmssMT;
			} else if(nextSignatureType == BlockSignatureTypes.SecretSequential) {
				this.NextBlockAccountSignature = new SecretBlockNextAccountSignature();
				this.NextAccountSignatureType = BlockSignatureTypes.SecretSequential;
			}
		}

		public IBlockAccountSignature BlockAccountSignature { get; private set; }
		public IBlockNextAccountSignature NextBlockAccountSignature { get; private set; }

		public BlockSignatureTypes AccountSignatureType {
			get => (BlockSignatureTypes) this.bitArray[0];
			set => this.bitArray[0] = (byte) value;
		}

		public BlockSignatureTypes NextAccountSignatureType {
			get => (BlockSignatureTypes) this.bitArray[1];
			set => this.bitArray[1] = (byte) value;
		}

		public byte NextModeratorKey => this.NextAccountSignatureType == BlockSignatureTypes.XmssMT ? GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID : GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID;

		public byte ModeratorKey {
			get {
				if(this.AccountSignatureType == BlockSignatureTypes.XmssMT) {
					return GlobalsService.MODERATOR_BLOCKS_KEY_XMSSMT_ID;
				}

				if(this.AccountSignatureType == BlockSignatureTypes.SecretSequential) {
					return GlobalsService.MODERATOR_BLOCKS_KEY_SEQUENTIAL_ID;
				}

				if(this.AccountSignatureType == BlockSignatureTypes.SuperSecret) {
					if(this.BlockAccountSignature is SuperSecretBlockAccountSignature superSecretBlockAccountSignature) {
						return superSecretBlockAccountSignature.KeyAddress.OrdinalId;
					}
				} else if(this.AccountSignatureType == BlockSignatureTypes.Genesis) {
					if(this.BlockAccountSignature is GenesisBlockAccountSignature genesisBlockAccountSignature) {
						return 0;
					}
				}

				throw new InvalidCastException();
			}
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			byte types = rehydrator.ReadByte();

			this.bitArray.SetData((ByteArray) new[] {types}, 2);

			if(this.AccountSignatureType == BlockSignatureTypes.Genesis) {
				this.BlockAccountSignature = new GenesisBlockAccountSignature();
			} else if(this.AccountSignatureType == BlockSignatureTypes.XmssMT) {
				this.BlockAccountSignature = new XmssBlockAccountSignature();
			} else if(this.AccountSignatureType == BlockSignatureTypes.SecretSequential) {
				this.BlockAccountSignature = new SecretBlockAccountSignature();
			} else if(this.AccountSignatureType == BlockSignatureTypes.SuperSecret) {
				this.BlockAccountSignature = new SuperSecretBlockAccountSignature();
			}

			if(this.NextAccountSignatureType == BlockSignatureTypes.XmssMT) {
				this.NextBlockAccountSignature = new XmssBlockNextAccountSignature();
			} else if(this.NextAccountSignatureType == BlockSignatureTypes.SecretSequential) {
				this.NextBlockAccountSignature = new SecretBlockNextAccountSignature();
			}

			this.BlockAccountSignature.Rehydrate(rehydrator);
			this.NextBlockAccountSignature.Rehydrate(rehydrator);
		}

		public void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.bitArray.GetData()[0]);

			this.BlockAccountSignature.Dehydrate(dehydrator);
			this.NextBlockAccountSignature.Dehydrate(dehydrator);
		}

		public HashNodeList GetStructuresArray() {

			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.BlockAccountSignature);
			nodeList.Add(this.NextBlockAccountSignature);

			return nodeList;
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {

			jsonDeserializer.SetProperty("BlockAccountSignature", this.BlockAccountSignature);
			jsonDeserializer.SetProperty("NextBlockAccountSignature", this.NextBlockAccountSignature);
		}

		public ISecretDoubleCryptographicKey ConvertToSecretKey() {
			if(((this.NextAccountSignatureType == BlockSignatureTypes.Genesis) || (this.NextAccountSignatureType == BlockSignatureTypes.SecretSequential)) && this.NextBlockAccountSignature is ISecretBlockNextAccountSignature secretBlockNextAccountSignature) {
				return SignatureUtils.ConvertToSecretKey(secretBlockNextAccountSignature, this.NextModeratorKey);
			}

			throw new ApplicationException("Not a secret key");
		}

		public SafeArrayHandle ConvertToDehydratedKey() {

			if(((this.NextAccountSignatureType == BlockSignatureTypes.Genesis) || (this.NextAccountSignatureType == BlockSignatureTypes.SecretSequential)) && this.NextBlockAccountSignature is ISecretBlockNextAccountSignature) {
				return SignatureUtils.ConvertToDehydratedKey(this.ConvertToSecretKey());
			}

			return null;
		}
	}
}