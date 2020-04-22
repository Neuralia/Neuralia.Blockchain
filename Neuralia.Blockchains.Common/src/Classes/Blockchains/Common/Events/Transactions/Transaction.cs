using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Crypto.Prng;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions {

	public interface ITransactionEssential {

		TransactionId TransactionId { get; set; }
		List<int> AccreditationCertificates { get; }
		KeyUseIndexSet KeyUseIndex { get; set; }
		Dictionary<AccountId, KeyUseIndexSet> MultiSigKeyUseIndices { get; }
	}

	public interface ITransaction : IBlockchainEvent<IDehydratedTransaction, ITransactionRehydrationFactory, TransactionType>, ITransactionEssential, IComparable<ITransaction> {

		HashNodeList GetStructuresArrayMultiSig(AccountId accountId);
		HashNodeList GetCompleteStructuresArray();
		void RehydrateForBlock(IDehydratedTransaction dehydratedTransaction, ITransactionRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp);
		IDehydratedTransaction DehydrateForBlock(BlockChannelUtils.BlockChannelTypes activeChannels);
		ImmutableList<AccountId> TargetAccounts { get; }
		string TargetAccountsSerialized { get; }
	}

	public abstract class Transaction : BlockchainEvent<IDehydratedTransaction, DehydratedTransaction, ITransactionRehydrationFactory, TransactionType>, ITransaction, IComparable<Transaction> {

		public int CompareTo(Transaction other) {
			return this.CompareTo((ITransaction) other);
		}

		public List<int> AccreditationCertificates { get; } = new List<int>();

		// run any sanitations here
		protected virtual void Sanitize() {
			
		}
		
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TransactionId", this.TransactionId);

			jsonDeserializer.SetArray("AccreditationCertificates", this.AccreditationCertificates);
		}

		public TransactionId TransactionId { get; set; } = new TransactionId();
		public KeyUseIndexSet KeyUseIndex { get; set; } = null;
		public Dictionary<AccountId, KeyUseIndexSet> MultiSigKeyUseIndices { get; } = new Dictionary<AccountId, KeyUseIndexSet>();

		public HashNodeList GetStructuresArrayMultiSig(AccountId accountId) {
			HashNodeList nodeList = this.GetStructuresArray();

			if(this.MultiSigKeyUseIndices.ContainsKey(accountId)) {
				nodeList.Add(this.MultiSigKeyUseIndices[accountId]);
			}

			return nodeList;
		}

		public HashNodeList GetCompleteStructuresArray() {
			HashNodeList nodeList = this.GetStructuresArray();

			nodeList.Add(this.MultiSigKeyUseIndices.Count);

			foreach(var entry in this.MultiSigKeyUseIndices) {
				nodeList.Add(entry.Key);
				nodeList.Add(entry.Value);
			}

			return nodeList;
		}
		
		public override HashNodeList GetStructuresArray() {

			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TransactionId);
			nodeList.Add(this.KeyUseIndex);

			nodeList.Add(this.AccreditationCertificates.Count);

			foreach(int entry in this.AccreditationCertificates.OrderBy(c => c)) {

				nodeList.Add(entry);
			}

			return nodeList;
		}

		public int CompareTo(ITransaction other) {
			return this.TransactionId.CompareTo(other.TransactionId);
		}

		protected bool Equals(ITransaction other) {
			return Equals(this.TransactionId, other.TransactionId);
		}

		protected bool Equals(Transaction other) {
			return this.Equals((ITransaction) other);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((ITransaction) obj);
		}

		public override int GetHashCode() {
			return this.TransactionId != (TransactionId) null ? this.TransactionId.GetHashCode() : 0;
		}

		public static bool operator ==(Transaction a, ITransaction b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(Transaction a, ITransaction b) {
			return !(a == b);
		}

	#region Rehydration

		public void RehydrateForBlock(IDehydratedTransaction dehydratedTransaction, ITransactionRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp) {

			this.RehydrateFullTransaction(dehydratedTransaction, rehydrationFactory, accountId, timestamp);
		}

		public override void Rehydrate(IDehydratedTransaction dehydratedTransaction, ITransactionRehydrationFactory rehydrationFactory) {

			this.RehydrateFullTransaction(dehydratedTransaction, rehydrationFactory, null, null);
		}

		public static ComponentVersion<TransactionType> RehydrateTopHeader(IDataRehydrator rehydrator, TransactionId transactionId, AccountId accountId, TransactionTimestamp timestamp) {
			var rehydratedVersion = rehydrator.Rehydrate<ComponentVersion<TransactionType>>();

			if(accountId == null && timestamp == null) {
				transactionId.Rehydrate(rehydrator);
			} else {
				transactionId.RehydrateRelative(rehydrator);
				transactionId.Account = accountId;
				transactionId.Timestamp = timestamp;
			}

			return rehydratedVersion;
		}

		protected virtual void RehydrateFullTransaction(IDehydratedTransaction dehydratedTransaction, ITransactionRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp) {
			using(IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(dehydratedTransaction.Header)) {

				// the header
				var rehydratedVersion = RehydrateTopHeader(rehydrator, this.TransactionId, accountId, timestamp);
				this.Version.EnsureEqual(rehydratedVersion);

				// the rest in the header
				this.RehydrateHeader(rehydrator);

				// and the rest
				var channels = dehydratedTransaction.DataChannels.ConvertAll(DataSerializationFactory.CreateRehydrator, BlockChannelUtils.BlockChannelTypes.Headers);
				this.RehydrateContents(channels, rehydrationFactory);
			}

			// any finalizer we need to do now that we rehydrated
			this.TransactionRehydrated();

			this.Sanitize();
		}

		protected virtual void RehydrateContents(ChannelsEntries<IDataRehydrator> dataChannels, ITransactionRehydrationFactory rehydrationFactory) {

		}

		protected virtual void TransactionRehydrated() {

		}

		/// <summary>
		///     anything else in the header
		/// </summary>
		/// <param name="rehydrator"></param>
		protected virtual void RehydrateHeader(IDataRehydrator rehydrator) {
			
			bool any = rehydrator.ReadBool();

			if(any) {
				this.KeyUseIndex = new KeyUseIndexSet();
				this.KeyUseIndex.Rehydrate(rehydrator);
			}

			this.MultiSigKeyUseIndices.Clear();
			any = rehydrator.ReadBool();
			if(any) {
				var rparameters = new AccountIdGroupSerializer.AccountIdGroupSerializerRehydrateParameters<AccountId>();

				rparameters.RehydrateExtraData = (entry, offset, index, rh) => {

					var keyUseIndexSet = new KeyUseIndexSet();
					keyUseIndexSet.Rehydrate(rh);
					this.MultiSigKeyUseIndices.Add(entry, keyUseIndexSet);
				};

				var results = AccountIdGroupSerializer.Rehydrate(rehydrator, true, rparameters);
			}
			
			this.AccreditationCertificates.Clear();
			any = rehydrator.ReadBool();

			if(any) {
				int count = rehydrator.ReadByte();

				for(int i = 0; i < count; i++) {
					this.AccreditationCertificates.Add(rehydrator.ReadInt());
				}
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			throw new NotImplementedException();
		}

	#endregion

	#region Dehydration

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotImplementedException();
		}

		public IDehydratedTransaction DehydrateForBlock(BlockChannelUtils.BlockChannelTypes activeChannels) {
			return this.DehydrateFullTransaction(activeChannels, true);
		}

		public abstract ImmutableList<AccountId> TargetAccounts { get; }

		public virtual string TargetAccountsSerialized {
			get {
				var targetAccounts = this.TargetAccounts;

				string result = "";

				if(targetAccounts.Any()) {
					result = targetAccounts.Count == 1 ? targetAccounts.Single().ToString() : string.Join(",", targetAccounts);
				}

				return result;
			}
		}

		public override IDehydratedTransaction Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels) {
			
			return this.DehydrateFullTransaction(activeChannels, false);

		}

		protected virtual IDehydratedTransaction DehydrateFullTransaction(BlockChannelUtils.BlockChannelTypes activeChannels, bool forBlocks) {
			
			this.Sanitize();

			var dataChannelDehydrators = new ChannelsEntries<IDataDehydrator>(activeChannels, types => DataSerializationFactory.CreateDehydrator());

			// transactions are simple, we ALWAYS write to the low header. the high header is not used for transactions.

			IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

			dehydratedTransaction.RehydratedTransaction = this;

			dehydratedTransaction.Uuid = this.TransactionId;

			//dehydratedTransaction.External = this is IExternalTransaction && !(this is IMasterTransaction);

			IDataDehydrator headerDehydrator = dataChannelDehydrators[BlockChannelUtils.BlockChannelTypes.LowHeader];

			// the header
			this.Version.Dehydrate(headerDehydrator);

			// this must always be the first thing in the header
			if(forBlocks) {
				this.TransactionId.DehydrateRelative(headerDehydrator);
			} else {
				this.TransactionId.Dehydrate(headerDehydrator);
			}

			// anything else
			this.DehydrateHeader(headerDehydrator);

			// dehydrate the rest
			dehydratedTransaction.DataChannels[BlockChannelUtils.BlockChannelTypes.LowHeader] = headerDehydrator.ToArray();

			// and now anything else
			this.DehydrateContents(dataChannelDehydrators);

			dataChannelDehydrators.RunForAll((flag, original) => {
				dehydratedTransaction.DataChannels[flag] = original.ToArray();
			}, BlockChannelUtils.BlockChannelTypes.Headers);

			return dehydratedTransaction;
		}

		protected virtual void DehydrateContents(ChannelsEntries<IDataDehydrator> dataChannels) {

		}

		/// <summary>
		///     anything else that should go in the header
		/// </summary>
		/// <param name="dehydrator"></param>
		protected virtual void DehydrateHeader(IDataDehydrator dehydrator) {
			
			bool any = this.KeyUseIndex != null;

			dehydrator.Write(any);
			if(any) {
				this.KeyUseIndex.Dehydrate(dehydrator);
			}

			any = this.MultiSigKeyUseIndices.Any();
			dehydrator.Write(any);
			if(any) {
				var dparameters = new AccountIdGroupSerializer.AccountIdGroupSerializerDehydrateParameters<AccountId, AccountId>();

				dparameters.DehydrateExtraData = (entry, accountId, offset, index, dh) => {

					this.MultiSigKeyUseIndices[accountId].Dehydrate(dehydrator);
				};
				
				AccountIdGroupSerializer.Dehydrate(this.MultiSigKeyUseIndices.Keys.ToList(), dehydrator, true, dparameters);
			}
			
			any = this.AccreditationCertificates.Any();
			dehydrator.Write(any);

			if(any) {
				dehydrator.Write((byte) this.AccreditationCertificates.Count);

				foreach(int entry in this.AccreditationCertificates) {

					dehydrator.Write(entry);
				}
			}
		}

	#endregion

	}
}