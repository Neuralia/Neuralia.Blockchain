using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {
	public class AccountSnapshot<ACCOUNT_ATTRIBUTE> : IAccountSnapshot<ACCOUNT_ATTRIBUTE>
		where ACCOUNT_ATTRIBUTE : IAccountAttribute {

		public long AccountId { get; set; }
		public long InceptionBlockId { get; set; }
		public byte TrustLevel { get; set; }
		public bool Correlated { get; set; }
		public ImmutableList<IAccountAttribute> AppliedAttributesBase => this.AppliedAttributes.Cast<IAccountAttribute>().ToImmutableList();
		public List<ACCOUNT_ATTRIBUTE> AppliedAttributes { get; } = new List<ACCOUNT_ATTRIBUTE>();

		public void ClearCollection() {
			this.AppliedAttributes.Clear();
		}

		public void CreateNewCollectionEntry(out IAccountAttribute result) {
			TypedCollectionExposureUtil<IAccountAttribute>.CreateNewCollectionEntry(this.AppliedAttributes, out result);
		}

		public virtual void AddCollectionEntry(IAccountAttribute entry) {
			TypedCollectionExposureUtil<IAccountAttribute>.AddCollectionEntry(entry, this.AppliedAttributes);
		}

		public void RemoveCollectionEntry(Func<IAccountAttribute, bool> predicate) {
			TypedCollectionExposureUtil<IAccountAttribute>.RemoveCollectionEntry(predicate, this.AppliedAttributes);
		}

		public IAccountAttribute GetCollectionEntry(Func<IAccountAttribute, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountAttribute>.GetCollectionEntry(predicate, this.AppliedAttributes);
		}

		public List<IAccountAttribute> GetCollectionEntries(Func<IAccountAttribute, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountAttribute>.GetCollectionEntries(predicate, this.AppliedAttributes);
		}

		public ImmutableList<IAccountAttribute> CollectionCopy => TypedCollectionExposureUtil<IAccountAttribute>.GetCollection(this.AppliedAttributes);
	}
}