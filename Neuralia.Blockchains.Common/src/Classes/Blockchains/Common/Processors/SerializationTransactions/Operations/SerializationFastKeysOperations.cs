using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations {
	public class SerializationKeyDictionaryOperations : SerializationTransactionOperation {

		public SerializationKeyDictionaryOperations(IChainDataWriteProvider chainDataWriteProvider) : base(chainDataWriteProvider) {
		}

		public AccountId AccountId { get; set; } = new AccountId();
		public byte Ordinal { get; set; }
		public byte TreeHeight { get; set; }
		public Enums.KeyHashType HashType { get; set; }
		public Enums.KeyHashType BackupHashType { get; set; }
		
		public SafeArrayHandle Key { get; } = SafeArrayHandle.Create();

		protected override void SetType() {
			this.SerializationTransactionOperationType = SerializationTransactionOperationTypes.KeyDictionary;
		}

		public override Task Undo() {

			return this.chainDataWriteProvider.SaveAccountKeyIndex(this.AccountId, this.Key, this.TreeHeight, this.HashType, this.BackupHashType, this.Ordinal);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.AccountId.Rehydrate(rehydrator);
			this.Ordinal = rehydrator.ReadByte();
			this.TreeHeight = rehydrator.ReadByte();
			this.HashType = (Enums.KeyHashType) rehydrator.ReadByte();
			this.BackupHashType = (Enums.KeyHashType) rehydrator.ReadByte();
			this.Key.Entry = rehydrator.ReadNonNullableArray();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.AccountId.Dehydrate(dehydrator);
			dehydrator.Write(this.Ordinal).Write(this.TreeHeight).Write((byte) this.HashType).Write((byte) this.BackupHashType);
			dehydrator.WriteNonNullable(this.Key);
		}
	}
}