using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Compositions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Serialization;
using Neuralia.Blockchains.Common.Classes.Tools.Serialization;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions {
	
	public interface ITransactionEssential {

		TransactionId TransactionId { get; set; }
		List<int> AccreditationCertificates { get; }
		TransactionMeta TransactionMeta { get; set; }
	}

	public interface ITransaction : IBlockchainEvent<IDehydratedTransaction, ITransactionRehydrationFactory, TransactionType>, ITransactionEssential, IComparable<ITransaction> {
		
		Enums.TransactionTargetTypes TargetType { get; }
		AccountId[] ImpactedAccounts { get; }
		AccountId[] TargetAccounts { get; }
		
		string TargetAccountsSerialized { get; }
		string ImpactedAccountsSerialized { get; }

		HashNodeList GetStructuresArrayMultiSig(AccountId accountId);
		HashNodeList GetCompleteStructuresArray();
		void RehydrateForBlock(IDehydratedTransaction dehydratedTransaction, ITransactionRehydrationFactory rehydrationFactory, AccountId accountId, TransactionTimestamp timestamp);
		IDehydratedTransaction DehydrateForBlock(BlockChannelUtils.BlockChannelTypes activeChannels);
		HashNodeList GetStructuresArray(Enums.MutableStructureTypes types);
	}

	public abstract class Transaction : BlockchainEvent<IDehydratedTransaction, DehydratedTransaction, ITransactionRehydrationFactory, TransactionType>, ITransaction, IComparable<Transaction> {
		
		public int CompareTo(Transaction other) {
			return this.CompareTo((ITransaction) other);
		}

		public List<int> AccreditationCertificates { get; } = new List<int>();

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//
			jsonDeserializer.SetProperty("TransactionId", this.TransactionId);
			jsonDeserializer.SetProperty("TransactionMeta", this.TransactionMeta);

			if(this.AccreditationCertificates.Any()) {
				jsonDeserializer.SetArray("AccreditationCertificates", this.AccreditationCertificates);
			}
		}

		public TransactionId TransactionId { get; set; } = new TransactionId();

		public TransactionMeta TransactionMeta { get; set; } = new TransactionMeta();
		
		public HashNodeList GetStructuresArrayMultiSig(AccountId accountId) {
			HashNodeList nodeList = this.GetStructuresArray();

			nodeList.Add(this.TransactionMeta.GetStructuresArrayMultiSig(accountId));

			return nodeList;
		}

		public HashNodeList GetCompleteStructuresArray() {
			HashNodeList nodeList = this.GetStructuresArray();

			nodeList.Add(this.TransactionMeta.GetStructuresArrayMultiSig());

			return nodeList;
		}

		public override sealed HashNodeList GetStructuresArray() {

			// hash with everything
			return this.GetStructuresArray(Enums.MutableStructureTypes.All);
		}

		/// <summary>
		/// here we allow to hash only the fixed components or with the dynamic ones
		/// </summary>
		/// <param name="types"></param>
		/// <returns></returns>
		public virtual HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.TransactionId);
			nodeList.Add(this.AccreditationCertificates.Count);

			foreach(int entry in this.AccreditationCertificates.OrderBy(c => c)) {

				nodeList.Add(entry);
			}

			if(types.HasFlag(Enums.MutableStructureTypes.Mutable)) {
				//this property is mutable, so we only set it sometimes
				nodeList.Add(this.TransactionMeta);
			}
			
			return nodeList;
		}

		public int CompareTo(ITransaction other) {
			return this.TransactionId.CompareTo(other.TransactionId);
		}

		// run any sanitations here
		protected virtual void Sanitize() {

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
			ComponentVersion<TransactionType> rehydratedVersion = rehydrator.Rehydrate<ComponentVersion<TransactionType>>();

			if((accountId == default(AccountId)) && (timestamp == null)) {
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
				ComponentVersion<TransactionType> rehydratedVersion = RehydrateTopHeader(rehydrator, this.TransactionId, accountId, timestamp);
				this.Version.EnsureEqual(rehydratedVersion);

				// the rest in the header
				this.RehydrateHeader(rehydrator);

				// and the rest
				ChannelsEntries<IDataRehydrator> channels = dehydratedTransaction.DataChannels.ConvertAll(DataSerializationFactory.CreateRehydrator, BlockChannelUtils.BlockChannelTypes.Headers);
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

			this.TransactionMeta.Rehydrate(rehydrator);

			this.AccreditationCertificates.Clear();
			bool any = rehydrator.ReadBool();

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

		protected AccountId[] GetAccountAndTargetsList(AccountId accountId) {

			return this.GetAccountAndTargetsList(new []{accountId});
		}
		
		protected AccountId[] GetAccountAndTargetsList(IEnumerable<AccountId> accountIds) {
			List<AccountId> accounts = new List<AccountId>();

			accounts.AddRange(accountIds);
			accounts.AddRange(this.GetSenderList());
			
			return accounts.ToArray();
		}
		
		protected AccountId[] GetSenderList() {
			return GetAccountIds(this.TransactionId.Account);
		}
		
		protected AccountId[] GetAccountIds(AccountId accountId) {
			if(accountId == null) {
				return Array.Empty<AccountId>();
			}
			return new[] {accountId};
		}
		
		protected AccountId[] TargetAccountsAndSender() {
			List<AccountId> accountIds = this.TargetAccounts.ToList();
			
			accountIds.Add(this.TransactionId.Account);

			return accountIds.Distinct().ToArray();
		}

		/// <summary>
		/// the accounts targetted by this transaction
		/// </summary>
		public abstract AccountId[] ImpactedAccounts { get; }
		
		/// <summary>
		/// the accounts targetted by this transaction
		/// </summary>
		public abstract AccountId[] TargetAccounts { get; }
		
		/// <summary>
		/// who is targetted by this transaction
		/// </summary>
		public abstract Enums.TransactionTargetTypes TargetType { get; }
		
		public virtual string TargetAccountsSerialized {
			get {
				AccountId[] targetAccounts = this.TargetAccounts;

				string result = "";

				if(targetAccounts.Any()) {
					result = targetAccounts.Length == 1 ? targetAccounts.Single().ToString() : string.Join(",", (IEnumerable<AccountId>)targetAccounts);
				}

				return result;
			}
		}
		
		public virtual string ImpactedAccountsSerialized {
			get {
				AccountId[] impactedAccounts = this.ImpactedAccounts;

				string result = "";

				if(impactedAccounts.Any()) {
					result = impactedAccounts.Length == 1 ? impactedAccounts.Single().ToString() : string.Join(",", (IEnumerable<AccountId>)impactedAccounts);
				}

				return result;
			}
		}

		public override IDehydratedTransaction Dehydrate(BlockChannelUtils.BlockChannelTypes activeChannels) {

			return this.DehydrateFullTransaction(activeChannels, false);

		}

		protected virtual IDehydratedTransaction DehydrateFullTransaction(BlockChannelUtils.BlockChannelTypes activeChannels, bool forBlocks) {

			this.Sanitize();

			ChannelsEntries<IDataDehydrator> dataChannelDehydrators = new ChannelsEntries<IDataDehydrator>(activeChannels, types => DataSerializationFactory.CreateDehydrator());

			// transactions are simple, we ALWAYS write to the low header. the high header is not used for transactions.

			IDehydratedTransaction dehydratedTransaction = new DehydratedTransaction();

			dehydratedTransaction.RehydratedEvent = this;

			dehydratedTransaction.Uuid = this.TransactionId;

			//dehydratedTransaction.External = this is IExternalTransaction && !(this is IKeyTransaction);

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

			this.TransactionMeta.Dehydrate(dehydrator);

			bool any = this.AccreditationCertificates.Any();
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